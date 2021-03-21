using Cassandra.Mapping.Attributes;
using Mono.Unix.Native;

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

        [ClusteringKey(0)]
        [Column("permissions")]
        public FilePermissions FilePermissions;

        [ClusteringKey(0)]
        [Column("gid")]
        public uint GID { get; set; }

        [ClusteringKey(0)]
        [Column("uid")]
        public uint UID { get; set; }
    }
}