using System.Collections.Generic;

namespace CassandraFS
{
    public class Config
    {
        public List<NodeSettings> CassandraEndPoints { get; set; }
        public string MessageSpaceName { get; set; }
        public bool DropFilesTable { get; set; }
        public bool DropFilesContentTable { get; set; }
        public bool DropDirectoriesTable { get; set; }
        public int ConnectionAttemptsCount { get; set; }
        public int ReconnectTimeout { get; set; }
        public int? DefaultTTL { get; set; }
        public int? DefaultDataBufferSize { get; set; }
    }

    public class NodeSettings
    {
        public string Host { get; set; }
    }
}