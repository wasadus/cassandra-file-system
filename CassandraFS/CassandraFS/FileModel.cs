using System;

namespace CassandraFS
{
    public class FileModel
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
        public DateTimeOffset ModifiedTimestamp { get; set; }
        public ExtendedAttributes ExtendedAttributes { get; set; }
    }
}