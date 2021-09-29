using System;
using System.Collections.Generic;
using System.Linq;
using Atlassian.Jira;
using Atlassian.Jira.Remote;
using Newtonsoft.Json;
using RestSharp;
using JiraWrapper.Models;
using JiraWrapper.Helpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Dynamic;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace JiraWrapper
{
    public class JiraWrapper
    {
        private readonly string _url;
        private readonly string _username;
        private readonly string _password;
        private Jira _jiraClient;
        private IJiraRestClient _jiraRestClient;
        /// <summary>
        /// Creates connection to Jira using basic auth
        /// </summary>
        /// <param name="url">Jira url</param>
        /// <param name="username">user name</param>
        /// <param name="apiKey">api key</param>
        public JiraWrapper(string url, string username, string apiKey)
        {
            _url = url;
            _username = username;
            _password = apiKey;
            CreateClient();
        }
        /// <summary>
        /// Creates an instance of the jira client
        /// </summary>
        private void CreateClient()
        {
            _jiraClient = Jira.CreateRestClient(_url, _username, _password);
            _jiraRestClient = _jiraClient.RestClient;
        }
        
        /// <summary>
        /// Get an issue by the key
        /// </summary>        
        public IssueModel GetIssue(string key, bool getWorklog = false)
        {
            return GetIssuesJql($"key={key}", 1, getWorklog).FirstOrDefault();
        }
        public List<IssueModel> GetIssuesJql(string jql, int maxResults = 100, bool getWorklog = false)
        {
            try
            {
                var issues = new List<IssueModel>();
                if (maxResults > 100)
                {
                    var response = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"rest/api/2/search?jql={jql}&maxResults=0").Result;
                    int totalIssues = response["total"].ToObject<int>();
                    for (int i = 0; i < maxResults; i += 100)
                    {
                        if (i > totalIssues)
                        {
                            break;
                        }
                        response = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"rest/api/2/search?jql={jql}&startAt={i}&maxResults=100").Result;
                        issues.AddRange(ParseIssues(response, getWorklog));
                    }
                }
                else
                {
                    var response = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"rest/api/2/search?jql={jql}&maxResults={maxResults}").Result;
                    issues = ParseIssues(response, getWorklog);
                }

                return issues;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public string CreateIssueRest(string projectKey, string issueType, Priority priority, string summary,
        string description, string status = "Backlog", string[] labels = null,
        string[] components = null, List<JiraFields> customFields = null, List<LinkedIssueModel> linkedIssues = null)
        {
            try
            {
                dynamic json = new ExpandoObject();
                AddProperty(json, "project", new { key = projectKey });
                AddProperty(json, "summary", summary);
                AddProperty(json, "description", description);
                AddProperty(json, "issuetype", new { name = issueType.ToString() });
                AddProperty(json, "priority", new { name = priority.ToString() });
                if (labels != null)
                {
                    AddProperty(json, "labels", labels.Select(l => l.Replace(" ", "")).ToArray());
                }
                if (components != null)
                {
                    AddProperty(json, "components", components.Select(c => new { name = c }).ToArray());
                }
                if (customFields != null)
                {
                    foreach (var field in customFields)
                    {
                        AddProperty(json, field.Id, field.FieldValue);
                    }
                }

                string jsonBody = JsonConvert.SerializeObject(new { fields = json },
                           new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                var result = _jiraRestClient.ExecuteRequestAsync(Method.POST, "rest/api/2/issue/", jsonBody).Result;
                string key = result["key"].ToObject<string>();
                var transitions = GetTransitionsRest(key);
                if (!(status.ToLower() == "backlog"))
                {
                    ChangeTicketTransition(key, status, transitions);
                }
                if (linkedIssues != null)
                {
                    AddRelated(key, linkedIssues);
                }
                return key;
            }
            catch (Exception)
            {
                throw;
            }

        }
        /// <summary>
        /// Gets the transitions in order to move it through the board columns
        /// </summary>
        public List<(string, string)> GetTransitionsRest(string issueKey)
        {
            var result = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"rest/api/3/issue/{issueKey}/transitions").Result;
            var tokens = result["transitions"];
            List<(string, string)> idTransition = new List<(string, string)>();
            foreach (var token in tokens)
            {
                var id = token["id"].ToObject<string>();
                var name = token["name"].ToObject<string>();
                idTransition.Add((id, name));
            }
            return idTransition;
        }
        /// <summary>
        /// Moves the ticket to a column
        /// </summary>
        /// <param name="destination">destination column</param>
        /// <param name="transitions">List of the available transitions</param>
        /// <returns></returns>
        public bool ChangeTicketTransition(string issueKey, string destination, List<(string, string)> transitions)
        {
            (string, string) transition = transitions.FirstOrDefault(t => t.Item2.Contains(destination));
            if (transition.Item1 != null)
            {
                try
                {
                    var jsonBody = new { transition = new { id = transition.Item1 } };
                    var res = _jiraRestClient.ExecuteRequestAsync(Method.POST, $"rest/api/3/issue/{issueKey}/transitions", jsonBody).Result;
                    return true;
                }
                catch (Exception)
                {
                    throw;
                }
            }
            else
            {
                throw new ArgumentException("Couldn't find the transtion, please check the name");
            }
        }
        /// <summary>
        /// Add a comment on an issue
        /// </summary>
        public bool CommentIssue(string key, string comment)
        {
            try
            {
                string jsonBody = JsonConvert.SerializeObject(
                               new
                               {
                                   update = new { comment = new[] { new { add = new { body = comment } } } },
                               });
                var result = _jiraRestClient.ExecuteRequestAsync(Method.PUT, $"rest/api/2/issue/{key}", jsonBody).Result;
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }
        /// <summary>
        /// Adds resolution to an issue
        /// </summary>
        /// <param name="issueKey">Issue Key</param>
        /// <param name="resolution">Resolution name</param>
        /// <param name="finalColumn">Final column name</param>
        /// <returns></returns>
        public bool ResolveTicket(string issueKey, string resolution, string finalColumn)
        {
            try
            {
                var transtion = GetTransitionsRest(issueKey).Where(t => t.Item2 == finalColumn).FirstOrDefault();
                var json = JsonConvert.SerializeObject(
                    new
                    {
                        transition = new { id = transtion.Item1 },
                        fields = new { resolution = new { name = resolution } }
                    });
                var result = _jiraRestClient.ExecuteRequestAsync(Method.POST, $"rest/api/2/issue/{issueKey}/transitions?expand=transitions.fields", json).Result;
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }
        /// <summary>
        /// Adds labels to an issue
        /// </summary>
        /// <param name="key">Issue Key</param>
        /// <param name="labels">Labels array</param>
        /// <returns></returns>
        public bool AddLabels(string key, string[] labels)
        {

            string jsonBody = JsonConvert.SerializeObject(
                            new
                            {
                                update = new { labels = labels.Select(l => new { add = l }) }
                            });
            var result = _jiraRestClient.ExecuteRequestAsync(Method.PUT, $"rest/api/2/issue/{key}", jsonBody).Result;
            return true;
        }
        /// <summary>
        /// Removes labels to an issue
        /// </summary>
        /// <param name="key">Issue Key</param>
        /// <param name="labels">Labels array</param>
        /// <returns></returns>
        public bool RemoveLabels(string key, string[] labels)
        {

            string jsonBody = JsonConvert.SerializeObject(
                            new
                            {
                                update = new { labels = labels.Select(l => new { remove = l }) }
                            });
            var result = _jiraRestClient.ExecuteRequestAsync(Method.PUT, $"rest/api/2/issue/{key}", jsonBody).Result;
            return true;
        }
        /// <summary>
        /// Gets all the fields available
        /// </summary>
        public List<JiraFields> GetFields()
        {
            try
            {
                var fields = new List<JiraFields>();
                var response = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"rest/api/3/field/search?maxResults=0").Result;
                int totalFields = response["total"].ToObject<int>();
                if (totalFields > 50)
                {
                    for (int i = 0; i < totalFields; i += 50)
                    {
                        if (i > totalFields)
                        {
                            break;
                        }
                        response = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"rest/api/3/field/search?startAt={i}").Result;
                        foreach (var obj in response["values"])
                        {
                            string id = obj["id"].ToObject<string>();
                            string fieldName = obj["name"].ToObject<string>();
                            fields.Add(new JiraFields { Id = id, FieldName = fieldName });
                        }
                    }
                }
                return fields;
            }
            catch (Exception)
            {
                throw;
            }

        }
        public bool AssignTicket(string key, string accountId)
        {
            string jsonBody = JsonConvert.SerializeObject(
                            new
                            {
                                fields = new { assignee = new { accountId = accountId } }
                            });

            var result = _jiraRestClient.ExecuteRequestAsync(Method.PUT, $"rest/api/2/issue/{key}", jsonBody).Result;
            if (result.Count() > 0)
            {
                return false;
            }
            return true;
        }
        public bool AddRelated(string key, List<LinkedIssueModel> linkedIssues)
        {
            try
            {
                foreach (var linked in linkedIssues)
                {
                    var update =
                    new
                    {
                        update = new
                        {
                            issuelinks = new object[]
                            {
                                new
                                {
                                    add = new
                                    {
                                        type = new
                                        {
                                            name = linked.RelationName,
                                            inward = linked.Inward,
                                            outward = linked.Outward,
                                        },
                                        outwardIssue = new
                                        {
                                            key = linked.LinkedKey,
                                        }
                                    }
                                }
                            }
                        }
                    };
                    var jsonBody = JsonConvert.SerializeObject(new { update.update },
                    new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
                    var result = _jiraRestClient.ExecuteRequestAsync(Method.PUT, $"rest/api/2/issue/{key}", jsonBody).Result;
                }
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }
        public List<JiraUserModel> GetProjectUsers(string projectKey)
        {
            var users = new List<JiraUserModel>();
            for (int i = 1; i < 1000000; i += 100)
            {
                var result = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"/rest/api/2/user/assignable/search?project={projectKey}&startAt={i}").Result;
                if (result.Count() <= 0)
                {
                    break;
                }
                foreach (var item in result)
                {
                    string userId = item["accountId"].ToString();
                    string userEmail = item["emailAddress"].ToString();
                    string userName = item["displayName"].ToString() ?? string.Empty;
                    users.Add(new JiraUserModel { UserId = userId, EmailAddress = userEmail, UserName = userName });
                }
            }
            return users;
        }
        public bool UpdateIssuePriority(string issueKey, string priority)
        {
            try
            {
                string jsonBody = JsonConvert.SerializeObject(
                               new
                               {
                                   update = new { priority = new List<object>() { new { set = new { name = priority } } } }
                               });
                var result = _jiraRestClient.ExecuteRequestAsync(Method.PUT, $"rest/api/2/issue/{issueKey}", jsonBody).Result;
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }
        private List<IssueModel> ParseIssues(JToken response, bool getWorklog = false)
        {
            Task[] tasks = new Task[3];
            try
            {
                var issues = new List<IssueModel>();
                var objects = response["issues"] ?? response;
                var customFields = GetFields();
                foreach (var obj in objects)
                {
                    string issueKey = obj["key"].ToObject<string>();
                    string issueId = obj["id"].ToObject<string>();
                    string assignee = obj["fields"]["assignee"].HasValues ? obj["fields"]["assignee"]["displayName"].ToObject<string>() : "Not assigned";
                    string assigneeUserId = obj["fields"]["assignee"].HasValues ? obj["fields"]["assignee"]["accountId"].ToObject<string>() : "";
                    string reporter = obj["fields"]["reporter"].HasValues ? obj["fields"]["reporter"]["displayName"].ToObject<string>() : "Not assigned";
                    string reporterUserId = obj["fields"]["reporter"].HasValues ? obj["fields"]["reporter"]["accountId"].ToObject<string>() : "";
                    string creator = obj["fields"]["creator"].HasValues ? obj["fields"]["creator"]["displayName"].ToObject<string>() : "Not assigned";
                    string creatorUserId = obj["fields"]["creator"].HasValues ? obj["fields"]["creator"]["accountId"].ToObject<string>() : "";
                    string summary = obj["fields"]["summary"].ToObject<string>();
                    string description = obj["fields"]["description"].ToObject<string>();
                    DateTime createdDate = obj["fields"]["created"].ToObject<DateTime>();
                    DateTime? updatedDate = null;
                    DateTime? resolutionDate = null;
                    if (obj["fields"]["updated"].HasValues)
                    {
                        updatedDate = obj["fields"]["updated"].ToObject<DateTime>();
                    }
                    if (obj["fields"]["resolutiondate"].HasValues)
                    {
                        resolutionDate = obj["fields"]["resolutiondate"].ToObject<DateTime>();
                    }
                    string[] labels = obj["fields"]["labels"].ToObject<string[]>();
                    string status = obj["fields"]["status"]["name"].ToObject<string>();
                    int statusId = obj["fields"]["status"]["id"].ToObject<int>();
                    string statusDescription = obj["fields"]["status"]["description"].ToObject<string>();
                    string priority = obj["fields"]["priority"]["name"].ToObject<string>();
                    int priorityId = obj["fields"]["priority"]["id"].ToObject<int>();
                    var componentsRaw = obj["fields"]["components"];
                    bool isSubtask = false;
                    ParentIssue parent = null;
                    var components = new List<string>();
                    var fieldsRaw = (JObject)obj["fields"];
                    var fields = new List<JiraFields>();
                    var issueTypeId = obj["fields"]["issuetype"]["id"].ToObject<int>();
                    var issueTypeName = obj["fields"]["issuetype"]["name"].ToObject<string>();
                    var issueTypeDescription = obj["fields"]["issuetype"]["description"].ToObject<string>();
                    var projectId = obj["fields"]["project"]["id"].ToObject<int>();
                    var projectKey = obj["fields"]["project"]["key"].ToObject<string>();
                    var projectName = obj["fields"]["project"]["name"].ToObject<string>();
                    var worklog = new List<TicketWorklog>();
                    if (obj["fields"]["parent"] != null)
                    {
                        isSubtask = true;
                        parent = new ParentIssue { IssueId = obj["fields"]["parent"]["id"].ToObject<string>(), Key = obj["fields"]["parent"]["key"].ToObject<string>() };
                    }
                    tasks[0] = Task.Run(() =>
                    {
                        foreach (var field in fieldsRaw)
                        {
                            if (field.Key.Contains("custom") && field.Value != null && field.Value.Type == JTokenType.String)
                            {
                                var customFieldsAux = customFields.Clone();
                                var jiraField = customFieldsAux.FirstOrDefault(c => c.Id == field.Key);
                                jiraField.FieldValue = field.Value.ToObject<string>();
                                fields.Add(jiraField);
                            }
                            else if (field.Key.Contains("custom") && field.Value != null && field.Value.Type == JTokenType.Object)
                            {
                                var customFieldsAux = customFields.Clone();
                                var jiraField = customFieldsAux.FirstOrDefault(c => c.Id == field.Key);
                                jiraField.FieldValue = field.Value["value"] != null ? field.Value["value"].ToString() : "";
                                fields.Add(jiraField);
                            }
                            else if (field.Key.Contains("custom") && field.Value != null && field.Value.Type == JTokenType.Float)
                            {
                                var customFieldsAux = customFields.Clone();
                                var jiraField = customFieldsAux.FirstOrDefault(c => c.Id == field.Key);
                                jiraField.FieldValue = field.Value.ToObject<float>();
                                fields.Add(jiraField);
                            }
                            else if (field.Key.Contains("custom") && field.Value != null && field.Value.Type == JTokenType.Integer)
                            {
                                var customFieldsAux = customFields.Clone();
                                var jiraField = customFieldsAux.FirstOrDefault(c => c.Id == field.Key);
                                jiraField.FieldValue = jiraField.FieldValue = field.Value.ToObject<int>();
                                fields.Add(jiraField);
                            }
                        }
                    });
                    tasks[1] = Task.Run(() =>
                    {
                        foreach (var component in componentsRaw)
                        {
                            components.Add(component["name"].ToObject<string>());
                        }
                    });
                    tasks[2] = Task.Run(() =>
                    {
                        if (getWorklog)
                        {
                            worklog = GetWorklog(issueKey).ToList();
                        }
                    });
                    Task.WaitAll(tasks);
                    var issue = new IssueModel(fields)
                    {
                        Key = issueKey,
                        IssueId = issueId,
                        Assignee = assignee,
                        AssigneeInfo = new JiraUserModel { UserId = assigneeUserId, UserName = assignee },
                        Reporter = reporter,
                        ReporterInfo = new JiraUserModel { UserId = reporterUserId, UserName = reporter },
                        Creator = creator,
                        CreatorInfo = new JiraUserModel { UserId = creatorUserId, UserName = creator },
                        PriorityId = priorityId,
                        Priority = priority,
                        Summary = summary,
                        Description = description,
                        Labels = labels,
                        CreatedDate = createdDate,
                        UpdatedDate = updatedDate,
                        Status = status,
                        Components = components.ToArray(),
                        Worklogs = worklog,
                        IsSubTask = isSubtask,
                        Parent = parent,
                        IssueType = issueTypeName,
                        IssueTypeData = new IssueTypeData { IssueTypeId = issueTypeId, Name = issueTypeName, Description = issueTypeDescription },
                        ProjectKey = projectKey,
                        Project = new ProjectData { Key = projectKey, Name = projectName, ProjectId = projectId },
                        StatusData = new StatusData { Id = statusId, Description = statusDescription, Name = status },
                        ResolutionDate = resolutionDate,
                    };
                    issues.Add(issue);
                }
                return issues.ToList();
            }
            catch (Exception)
            {
                throw;
            }
        }
        private IEnumerable<TicketWorklog> GetWorklog(string issueKey)
        {
            var response = _jiraRestClient.ExecuteRequestAsync(Method.GET, $"rest/api/3/issue/{issueKey}/worklog").Result;
            var objects = response["worklogs"];
            foreach (var obj in objects)
            {
                string userId = obj["author"]["accountId"].ToString() ?? string.Empty;
                string userName = obj["author"]["displayName"].ToString() ?? string.Empty;
                DateTime created = obj["created"].ToObject<DateTime>();
                DateTime updated = obj["updated"].ToObject<DateTime>();
                DateTime started = obj["started"].ToObject<DateTime>();
                long timeSpent = obj["timeSpentSeconds"].ToObject<int>();
                yield return new TicketWorklog()
                {
                    UserId = userId,
                    UserName = userName,
                    CreatedDate = created,
                    UpdatedDate = updated,
                    StartedTime = started,
                    TimeSpentSeconds = timeSpent,
                };
            }

        }
        private void AddProperty(ExpandoObject expando, string propertyName, object propertyValue)
        {
            // ExpandoObject supports IDictionary so we can extend it like this
            var expandoDict = expando as IDictionary<string, object>;
            if (expandoDict.ContainsKey(propertyName))
                expandoDict[propertyName] = propertyValue;
            else
                expandoDict.Add(propertyName, propertyValue);
        }
        private List<JiraFields> ParseFields(JObject fieldsRaw)
        {
            var customFields = GetFields();
            var fields = new List<JiraFields>();

            foreach (var field in fieldsRaw)
            {
                if (field.Key.Contains("custom") && field.Value != null && field.Value.Type == JTokenType.String)
                {
                    var jiraField = customFields.FirstOrDefault(c => c.Id == field.Key);
                    jiraField.FieldValue = field.Value.ToObject<string>();
                    fields.Add(jiraField);
                }
                else if (field.Key.Contains("custom") && field.Value != null && field.Value.Type == JTokenType.Object)
                {
                    var jiraField = customFields.FirstOrDefault(c => c.Id == field.Key);
                    jiraField.FieldValue = field.Value["value"] != null ? field.Value["value"].ToString() : "";
                    fields.Add(jiraField);
                }
            }
            return fields;
        }
    }

    public enum Priority
    {
        Trivial, Lowest, Low, Medium, High, Highest
    }

    public enum IssueType
    {
        Task, Bug, OPS_Alert
    }
}
