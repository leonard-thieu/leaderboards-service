namespace toofz.Services.LeaderboardsService.Properties
{
    internal static class ILeaderboardsSettingsExtensions
    {
        /// <summary>
        /// Gets a value indicating if Steam Client credentials are set.
        /// </summary>
        /// <returns>
        /// true, if both <see cref="ILeaderboardsSettings.SteamUserName"/> and <see cref="ILeaderboardsSettings.SteamPassword"/> 
        /// are set; otherwise; false.
        /// </returns>
        public static bool AreSteamClientCredentialsSet(this ILeaderboardsSettings settings)
        {
            return !string.IsNullOrEmpty(settings.SteamUserName) &&
                   settings.SteamPassword != null;
        }
    }
}
