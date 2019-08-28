using System;

namespace Roki.Core.Services
{
    public interface IStatsService : IRService
    {
        string Author { get; }
        long CommandsRan { get; }
        string Heap { get; }
        string Library { get; }
        long MessageCounter { get; }
        double MessagesPerSecond { get; }
        long TextChannels { get; }
        long VoiceChannels { get; }

        TimeSpan GetUptime();
        string GetUptimeString(string separator = ", ");
        void Initialize();
    }
}