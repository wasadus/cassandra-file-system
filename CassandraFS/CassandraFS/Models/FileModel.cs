using System;

using Mono.Unix.Native;

namespace CassandraFS.Models
{
    public class FileModel : IFileSystemEntry
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
        public DateTimeOffset ModifiedTimestamp { get; set; }
        public ExtendedAttributes ExtendedAttributes { get; set; }
        public FilePermissions FilePermissions { get; set; }
        public uint GID { get; set; }
        public uint UID { get; set; }
        public Guid? ContentGUID { get; set; }

        public Stat GetStat() => new Stat()
        {
            st_nlink = 1,
            st_mode = FilePermissions,
            st_size = Data?.LongLength ?? 0,
            st_blocks = Data?.LongLength / 512 ?? 0,
            st_blksize = Data?.LongLength ?? 0, // Optimal size for buffer in I/O operations
            st_atim = DateTimeOffset.Now.ToTimespec(), // access
            st_mtim = ModifiedTimestamp.ToTimespec(), // modified
            st_gid = GID,
            st_uid = UID,
        };
    }
}