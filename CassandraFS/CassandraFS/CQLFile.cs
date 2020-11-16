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
    }
}