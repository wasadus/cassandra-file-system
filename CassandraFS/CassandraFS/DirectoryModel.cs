using Mono.Unix.Native;

namespace CassandraFS
{
    public class DirectoryModel
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public FilePermissions FilePermissions { get; set; }
        public uint GID { get; set; }
        public uint UID { get; set; }
    }
}