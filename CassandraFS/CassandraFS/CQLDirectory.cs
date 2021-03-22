using Cassandra.Mapping.Attributes;

namespace CassandraFS
{
    [Table(Name = "Directories", Keyspace = "FTPMessageSpace", CaseSensitive = true)]
    public class CQLDirectory
    {
        [PartitionKey(0)]
        [Column("path")]
        public string Path;

        [ClusteringKey(0)]
        [Column("name")]
        public string Name;

        [Column("permissions")]
        public int FilePermissions;

        [Column("gid")]
        public long GID { get; set; }

        [Column("uid")]
        public long UID { get; set; }
    }
}