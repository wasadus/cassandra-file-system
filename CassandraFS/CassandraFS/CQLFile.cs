using System;

using Cassandra.Mapping.Attributes;

namespace CassandraFS
{
    [Table(Name = "Files", Keyspace = "FTPMessageSpace", CaseSensitive = true)]
    public class CQLFile
    {
        [PartitionKey(0)]
        [Column("path")]
        public string Path { get; set; }

        [ClusteringKey(0)]
        [Column("name")]
        public string Name { get; set; }

        [Column("modified")]
        public DateTimeOffset ModifiedTimestamp { get; set; }

        [Column("extended_attributes")]
        public byte[] ExtendedAttributes { get; set; }

        [Column("data")]
        public byte[] Data { get; set; }

        [Column("content_guid")]
        public Guid? ContentGuid { get; set; }

        [Column("permissions")]
        public int FilePermissions { get; set; }

        [Column("gid")]
        public long GID { get; set; }

        [Column("uid")]
        public long UID { get; set; }
    }
}