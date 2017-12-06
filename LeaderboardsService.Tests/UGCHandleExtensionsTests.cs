using SteamKit2;
using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class UGCHandleExtensionsTests
    {
        public class ToReplayIdMethod
        {
            [Fact]
            public void UgcIdIsUInt64MaxValue_ReturnsNull()
            {
                // Arrange
                var ugcId = new UGCHandle(ulong.MaxValue);

                // Act
                var replayId = UGCHandleExtensions.ToReplayId(ugcId);

                // Assert
                Assert.Null(replayId);
            }

            [Fact]
            public void UgcIdIsZero_ReturnsNull()
            {
                // Arrange
                var ugcId = new UGCHandle(0);

                // Act
                var replayId = UGCHandleExtensions.ToReplayId(ugcId);

                // Assert
                Assert.Null(replayId);
            }

            [Fact]
            public void ReturnsReplayId()
            {
                // Arrange
                var ugcId = new UGCHandle(3489753984753);

                // Act
                var replayId = UGCHandleExtensions.ToReplayId(ugcId);

                // Assert
                Assert.Equal(3489753984753, replayId);
            }
        }
    }
}
