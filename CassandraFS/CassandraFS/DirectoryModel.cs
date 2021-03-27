using Mono.Unix.Native;
using System;

namespace CassandraFS
{
    public class DirectoryModel : IFileSystemEntry
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public FilePermissions FilePermissions { get; set; }
        public uint GID { get; set; }
        public uint UID { get; set; }
        public DateTimeOffset ModifiedTimestamp { get ; set; }

        public Stat GetStat() => new Stat()
        {
            st_atim = DateTimeOffset.Now.ToTimespec(),
            st_mtim = ModifiedTimestamp.ToTimespec()),
            st_gid = GID,
            st_uid = UID,
            st_mode = FilePermissions,
            st_nlink = 1,
        };
    }
}