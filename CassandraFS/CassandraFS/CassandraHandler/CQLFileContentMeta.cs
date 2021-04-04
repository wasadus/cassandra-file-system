using Cassandra.Mapping.Attributes;
using CassandraFS.BlobStorage;

namespace CassandraFS.CassandraHandler
{
    [Table(Name = "FilesContentMeta", Keyspace = "FTPMessageSpace", CaseSensitive = true)]
    class CQLFileContentMeta : CQLLargeBlobMeta
    {
    }
}
