using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraWrapper.Models
{
    public class IssueTypeData
    {
        public int IssueTypeId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
