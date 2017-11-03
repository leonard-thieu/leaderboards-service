using SteamKit2;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class SteamIDExtensionsTests
    {
        public class ToInt64Method
        {
            [Fact]
            public void ReturnsSteamIdAsInt64()
            {
                // Arrange
                var steamId = new SteamID(3489758347583);

                // Act
                var int64 = SteamIDExtensions.ToInt64(steamId);

                // Assert
                Assert.Equal(3489758347583, int64);
            }
        }
    }
}
