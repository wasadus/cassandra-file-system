using System;

using Mono.Unix.Native;

namespace CassandraFS
{
    public static class DateTimeExtensions
    {
        public static Timespec ToTimespec(this DateTimeOffset dateTime)
        {
            dateTime = dateTime.ToUniversalTime();
            var ticks = dateTime.Ticks - DateTime.UnixEpoch.Ticks;
            var sec = ticks / TimeSpan.TicksPerSecond;
            ticks -= TimeSpan.TicksPerSecond * sec;
            var nsec = ticks * 100;
            return new Timespec {tv_sec = sec, tv_nsec = (int)nsec};
        }
    }
}