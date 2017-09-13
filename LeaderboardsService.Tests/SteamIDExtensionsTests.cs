using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamKit2;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class SteamIDExtensionsTests
    {
        [TestClass]
        public class ToInt64Method
        {
            [TestMethod]
            public void ReturnsSteamIdAsInt64()
            {
                // Arrange
                var steamId = new SteamID(3489758347583);

                // Act
                var int64 = SteamIDExtensions.ToInt64(steamId);

                // Assert
                Assert.AreEqual(3489758347583, int64);
            }
        }
    }
}
