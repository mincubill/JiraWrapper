using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraWrapper.Models
{
    public class LinkedIssueModel
    {
        public string LinkedKey { get; set; }
        public string RelationName { get; set; } = "Related";
        public string Inward { get; set; } = "is related to";
        public string Outward { get; set; } = "is related to";
    }
}
