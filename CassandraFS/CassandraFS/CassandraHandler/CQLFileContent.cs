using Cassandra.Mapping.Attributes;
using CassandraFS.BlobStorage;

namespace CassandraFS.CassandraHandler
{
    [Table(Name = "FilesContent", Keyspace = "FTPMessageSpace", CaseSensitive = true)]
    public class CQLFileContent : CQLLargeBlobChunk
    {
    }
}