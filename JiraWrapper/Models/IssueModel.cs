using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraWrapper.Models
{
    public class IssueModel
    {
        private readonly ReadOnlyCollection<JiraFields> _jiraFields;

        public string Key { get; set; }
        public string IssueId { get; set; }
        public string Assignee { get; set; }
        public JiraUserModel AssigneeInfo { get; set; }
        public string Reporter { get; set; }
        public JiraUserModel ReporterInfo { get; set; }
        public string Creator { get; set; }
        public JiraUserModel CreatorInfo { get; set; }
        public int PriorityId { get; set; }
        public string Priority { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string[] Labels { get; set; }
        public string[] Components { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public DateTime? ResolutionDate { get; set; }
        public string Status { get; set; }
        public StatusData StatusData { get; set; }
        public IList<JiraFields> Fields
        {
            get { return _jiraFields; }
        }
        public List<TicketWorklog> Worklogs { get; set; }
        public bool IsSubTask { get; set; }
        public ParentIssue Parent { get; set; }
        public string ProjectKey { get; set; }
        public ProjectData Project { get; set; }
        public string IssueType { get; set; }
        public IssueTypeData IssueTypeData { get; set; }
        public IssueModel()
        {

        }
        public IssueModel(List<JiraFields> fields)
        {
            _jiraFields = fields.AsReadOnly();
        }
    }
}
