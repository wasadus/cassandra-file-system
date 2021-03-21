using System;

using Mono.Unix.Native;

namespace CassandraFS
{
    public class FileModel
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
        public DateTimeOffset ModifiedTimestamp { get; set; }
        public ExtendedAttributes ExtendedAttributes { get; set; }
        public FilePermissions FilePermissions { get; set; }
        public uint GID { get; set; }
        public uint UID { get; set; }
        public Guid ContentGUID { get; set; }
    }
}