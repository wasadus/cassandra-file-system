using System;

namespace CassandraFS.CassandraHandler
{
    public interface IGlobalTimestampProvider
    {
        DateTimeOffset UpdateTimestamp();
    }

    public class LocalTimestampProvider : IGlobalTimestampProvider
    {
        public DateTimeOffset UpdateTimestamp()
        {
            return DateTimeOffset.Now;
        }
    }
}
