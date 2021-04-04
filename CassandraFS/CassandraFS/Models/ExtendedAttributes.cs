using System.Collections.Generic;

namespace CassandraFS.Models
{
    public class ExtendedAttributes
    {
        public Dictionary<string, byte[]> Attributes { get; set; }

        public ExtendedAttributes()
        {
            Attributes = new Dictionary<string, byte[]>();
        }
    }
}