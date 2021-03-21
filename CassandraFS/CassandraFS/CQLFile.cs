using System;

using Cassandra.Mapping.Attributes;
using Mono.Unix.Native;

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

        [ClusteringKey(0)]
        [Column("modified")]
        public DateTimeOffset ModifiedTimestamp { get; set; }

        [ClusteringKey(0)]
        [Column("extended_attributes")]
        public byte[] ExtendedAttributes { get; set; }

        [ClusteringKey(0)]
        [Column("data")]
        public byte[] Data { get; set; }

        [ClusteringKey(0)]
        [Column("content_guid")]
        public Guid ContentGuid { get; set; }

        [ClusteringKey(0)]
        [Column("permissions")]
        public FilePermissions FilePermissions { get; set; }

        [ClusteringKey(0)]
        [Column("gid")]
        public uint GID { get; set; }

        [ClusteringKey(0)]
        [Column("uid")]
        public uint UID { get; set; }
    }
}