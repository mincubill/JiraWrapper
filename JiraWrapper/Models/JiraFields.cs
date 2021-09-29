using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraWrapper.Models
{
    public class JiraFields : ICloneable
    {
        public string Id { get; set; }
        public string FieldName { get; set; }
        public object FieldValue { get; set; }

        public object Clone()
        {
            return new JiraFields { FieldName = this.FieldName, FieldValue = this.FieldValue, Id = this.Id };
        }
    }
}
