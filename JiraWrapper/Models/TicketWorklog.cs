using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraWrapper.Models
{
    public class TicketWorklog
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
        public DateTime StartedTime { get; set; }
        public long TimeSpentSeconds { get; set; }
    }
}
