using System;
using SteamKit2;
using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class UGCHandleExtensionsTests
    {
        public class ToReplayIdMethod
        {
            [DisplayFact("UgcId", nameof(UInt64), nameof(UInt64.MaxValue))]
            public void UgcIdIsUInt64MaxValue_ReturnsNull()
            {
                // Arrange
                var ugcId = new UGCHandle(ulong.MaxValue);

                // Act
                var replayId = ugcId.ToReplayId();

                // Assert
                Assert.Null(replayId);
            }

            [DisplayFact("UgcId")]
            public void UgcIdIsZero_ReturnsNull()
            {
                // Arrange
                var ugcId = new UGCHandle(0);

                // Act
                var replayId = ugcId.ToReplayId();

                // Assert
                Assert.Null(replayId);
            }

            [DisplayFact]
            public void ReturnsReplayId()
            {
                // Arrange
                var ugcId = new UGCHandle(3489753984753);

                // Act
                var replayId = ugcId.ToReplayId();

                // Assert
                Assert.Equal(3489753984753, replayId);
            }
        }
    }
}
