using Mono.Unix.Native;
using System;

namespace CassandraFS.Models
{
    public interface IFileSystemEntry
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public DateTimeOffset ModifiedTimestamp { get; set; }
        public FilePermissions FilePermissions { get; set; }
        public uint GID { get; set; }
        public uint UID { get; set; }

        public Stat GetStat();
    }
}
