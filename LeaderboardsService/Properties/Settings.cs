using System.Configuration;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties
{
    [SettingsProvider(typeof(ServiceSettingsProvider))]
    partial class Settings : ILeaderboardsSettings { }
}
