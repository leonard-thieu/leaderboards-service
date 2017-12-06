using System.Configuration;

namespace toofz.Services.LeaderboardsService.Properties
{
    [SettingsProvider(typeof(ServiceSettingsProvider))]
    partial class Settings : ILeaderboardsSettings { }
}
