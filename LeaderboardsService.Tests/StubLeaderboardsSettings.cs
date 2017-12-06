using System;
using toofz.Services.LeaderboardsService.Properties;

namespace toofz.Services.LeaderboardsService.Tests
{
    internal sealed class StubLeaderboardsSettings : ILeaderboardsSettings
    {
        public uint AppId => 247080;

        public string SteamUserName { get; set; }
        public EncryptedSecret SteamPassword { get; set; }
        public EncryptedSecret LeaderboardsConnectionString { get; set; }
        public TimeSpan UpdateInterval { get; set; }
        public TimeSpan DelayBeforeGC { get; set; }
        public string InstrumentationKey { get; set; }
        public int KeyDerivationIterations { get; set; }
        public int DailyLeaderboardsPerUpdate { get; set; }
        public TimeSpan SteamClientTimeout { get; set; }

        public void Reload() { }

        public void Save() { }
    }
}