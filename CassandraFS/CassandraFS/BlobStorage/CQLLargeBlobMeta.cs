using System;

using Cassandra.Mapping.Attributes;

namespace CassandraFS.BlobStorage
{
    public abstract class CQLLargeBlobMeta
    {
        [PartitionKey(0)]
        [Column("blob_id")]
        public string BlobId { get; set; }

        [Column("blob_version")]
        public Guid BlobVersion { get; set; }

        [Column("chunks_count")]
        public short ChunksCount { get; set; }

        public override string ToString()
        {
            return $"blob_id: {BlobId}, blob_version: {BlobVersion}, chunks_count: {ChunksCount}";
        }
    }
}