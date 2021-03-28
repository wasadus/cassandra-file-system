using Cassandra.Mapping.Attributes;

namespace CassandraFS
{
    [Table(Name = "FilesContentMeta", Keyspace = "FTPMessageSpace", CaseSensitive = true)]
    class CQLFileContentMeta : CQLLargeBlobMeta
    {
    }
}
