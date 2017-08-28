using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties
{
    interface ILeaderboardsSettings : ISettings
    {
        /// <summary>
        /// The product's application ID.
        /// </summary>
        uint AppId { get; }
        /// <summary>
        /// The user name used to log on to Steam.
        /// </summary>
        string SteamUserName { get; set; }
        /// <summary>
        /// The password used to log on to Steam.
        /// </summary>
        EncryptedSecret SteamPassword { get; set; }
        /// <summary>
        /// The connection string used to connect to the leaderboards database.
        /// </summary>
        EncryptedSecret LeaderboardsConnectionString { get; set; }
    }
}