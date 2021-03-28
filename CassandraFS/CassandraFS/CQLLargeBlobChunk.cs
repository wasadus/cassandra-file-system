using Cassandra.Mapping;
using Cassandra.Mapping.Attributes;
using System;

namespace CassandraFS
{
    public abstract class CQLLargeBlobChunk
    {
        [PartitionKey(0)]
        [Column("blob_version")]
        public Guid BlobVersion { get; set; }

        [ClusteringKey(0, SortOrder.Ascending)]
        [Column("chunk_id")]
        public short ChunkId { get; set; }

        [Column("chunk_bytes")]
        public byte[] ChunkBytes { get; set; }

        public override string ToString()
        {
            return $"blob_version: {BlobVersion}, chunk_id: {ChunkId}, chunk_bytes.Length: {(ChunkBytes ?? new byte[0]).Length}";
        }
    }
}
