namespace toofz.Services.LeaderboardsService.Properties
{
    internal interface ILeaderboardsSettings : ISettings
    {
        /// <summary>
        /// The product's application ID.
        /// </summary>
        uint AppId { get; }
    }
}