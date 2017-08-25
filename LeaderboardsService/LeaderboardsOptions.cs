using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    sealed class LeaderboardsOptions : Options
    {
        public string SteamUserName { get; internal set; }
        public string SteamPassword { get; internal set; } = "";
        public string LeaderboardsConnectionString { get; internal set; } = "";
        public string LeaderboardsInstrumentationKey { get; internal set; }
    }
}
