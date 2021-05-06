#nullable enable
using System;
using System.IO;
using System.Linq;

using Cassandra;
using Cassandra.Data.Linq;
using CassandraFS.CassandraHandler;
using MoreLinq;

namespace CassandraFS.BlobStorage
{
    public class CqlLargeBlobStorage<TBlobMeta, TBlobChunk>
        where TBlobMeta : CQLLargeBlobMeta, new()
        where TBlobChunk : CQLFileContent, new()
    {
        private readonly int chunkSize;
        private readonly int maxBlobSize;

        public CqlLargeBlobStorage(ISession session, int chunkSize)
        {
            blobMetaTable = new Table<TBlobMeta>(session);
            blobChunksTable = new Table<TBlobChunk>(session);
            this.chunkSize = chunkSize;
            maxBlobSize = 64 * chunkSize;
        }

        public bool Exists(string blobId) => TryReadBlobMeta(blobId) != null;

        public byte[]? TryRead(string blobId)
        {
            var blobMeta = TryReadBlobMeta(blobId);
            if (blobMeta == null)
                return null;
            var chunks = ReadBlobChunks(blobMeta);
            using var ms = new MemoryStream();
            foreach (var chunk in chunks)
                ms.Write(chunk.ChunkBytes, 0, chunk.ChunkBytes.Length);
            return ms.ToArray();
        }

        public void Write(string blobId, byte[] blob, DateTimeOffset timestamp, TimeSpan? ttl)
        {
            if (blob.Length > maxBlobSize)
                throw new ArgumentException($"Blob is too large for blobId: {blobId}, blob.Length: {blob.Length}, maxBlobSize: {maxBlobSize}");
            if (ttl.HasValue && ttl.Value < TimeSpan.FromSeconds(1))
                throw new ArgumentException($"Ttl is too small for blobId: {blobId}, ttl: {ttl}");
            var oldBlobMeta = TryReadBlobMeta(blobId);
            var newBlobVersion = Guid.NewGuid();
            var chunks = blob.Batch(chunkSize).Select((chunk, chunkId) => new TBlobChunk
            {
                BlobVersion = newBlobVersion,
                ChunkId = (short)chunkId,
                ChunkBytes = chunk.ToArray(),
            }).ToArray();
            var newChunksCount = (short)chunks.Length;
            foreach (var chunk in chunks)
            {
                var insertChunkCqlCommand = blobChunksTable.Insert(chunk).SetTimestamp(timestamp);
                if (ttl.HasValue)
                    insertChunkCqlCommand = insertChunkCqlCommand.SetTTL((int)ttl.Value.TotalSeconds);
                insertChunkCqlCommand.Execute();
            }
            var updateMetaCqlCommand = blobMetaTable
                                       .Where(x => x.BlobId == blobId)
                                       .Select(x => new TBlobMeta { BlobVersion = newBlobVersion, ChunksCount = newChunksCount })
                                       .Update()
                                       .SetTimestamp(timestamp);
            if (ttl.HasValue)
                updateMetaCqlCommand = updateMetaCqlCommand.SetTTL((int)ttl.Value.TotalSeconds);
            updateMetaCqlCommand.Execute();
            if (oldBlobMeta != null)
                MakeChunksObsolete(oldBlobMeta, timestamp);
        }

        public bool TryDelete(string blobId, DateTimeOffset timestamp)
        {
            var oldBlobMeta = TryReadBlobMeta(blobId);
            if (oldBlobMeta == null)
                return false;
            blobMetaTable.Where(x => x.BlobId == blobId)
                         .Delete()
                         .SetTimestamp(timestamp)
                         .Execute();
            MakeChunksObsolete(oldBlobMeta, timestamp);
            return true;
        }

        private TBlobMeta? TryReadBlobMeta(string blobId)
            => blobMetaTable.Where(x => x.BlobId == blobId).Execute().SingleOrDefault();

        private TBlobChunk[] ReadBlobChunks(TBlobMeta blobMeta)
        {
            var chunks = blobChunksTable.Where(x => x.BlobVersion == blobMeta.BlobVersion).SetPageSize(chunksReadingPageSize).Execute().ToArray();
            if (chunks.Length != blobMeta.ChunksCount)
                throw new Exception($"CqlLargeBlobStorage is corrupted (chunks.Length ({chunks.Length}) != blobMeta.ChunksCount ({blobMeta.ChunksCount})) when reading with pageSize = {chunksReadingPageSize} for: {blobMeta}");
            return chunks;
        }

        private void MakeChunksObsolete(TBlobMeta blobMeta, DateTimeOffset timestamp)
        {
            var chunks = ReadBlobChunks(blobMeta);
            foreach (var chunk in chunks)
            {
                blobChunksTable.Where(x => x.BlobVersion == blobMeta.BlobVersion && x.ChunkId == chunk.ChunkId)
                               .Select(x => new TBlobChunk { ChunkBytes = chunk.ChunkBytes })
                               .Update()
                               .SetTimestamp(timestamp)
                               .SetTTL((int)obsoleteBlobTtl.TotalSeconds)
                               .Execute();
            }
        }

        private const int chunksReadingPageSize = 10;
        private readonly TimeSpan obsoleteBlobTtl = TimeSpan.FromMinutes(10);
        private readonly Table<TBlobMeta> blobMetaTable;
        private readonly Table<TBlobChunk> blobChunksTable;
    }
}