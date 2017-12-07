using System;
using SteamKit2;
using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class SteamIDExtensionsTests
    {
        public class ToInt64Method
        {
            [DisplayFact(nameof(SteamID), nameof(Int64))]
            public void ReturnsSteamIDAsInt64()
            {
                // Arrange
                var steamId = new SteamID(3489758347583);

                // Act
                var int64 = steamId.ToInt64();

                // Assert
                Assert.Equal(3489758347583, int64);
            }
        }
    }
}
