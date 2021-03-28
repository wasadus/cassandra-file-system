using System;

using Cassandra.Mapping.Attributes;

namespace CassandraFS
{
    [Table(Name = "FilesContent", Keyspace = "FTPMessageSpace", CaseSensitive = true)]
    public class CQLFileContent : CQLLargeBlobChunk
    {
        //[PartitionKey(0)]
        //[Column("guid")]
        //public Guid? GUID { get; set; }

        //[ClusteringKey(0)]
        //[Column("data")]
        //public byte[] Data { get; set; }
    }
}