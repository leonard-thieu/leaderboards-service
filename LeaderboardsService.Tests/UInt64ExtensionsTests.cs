using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class UInt64ExtensionsTests
    {
        public class ToReplayIdMethod
        {
            [Fact]
            public void UgcIdIsUInt64MaxValue_ReturnsNull()
            {
                // Arrange
                var ugcId = ulong.MaxValue;

                // Act
                var replayId = UInt64Extensions.ToReplayId(ugcId);

                // Assert
                Assert.Null(replayId);
            }

            [Fact]
            public void UgcIdIsZero_ReturnsNull()
            {
                // Arrange
                var ugcId = 0UL;

                // Act
                var replayId = UInt64Extensions.ToReplayId(ugcId);

                // Assert
                Assert.Null(replayId);
            }

            [Fact]
            public void ReturnsReplayId()
            {
                // Arrange
                var ugcId = 3489753984753UL;

                // Act
                var replayId = UInt64Extensions.ToReplayId(ugcId);

                // Assert
                Assert.Equal(3489753984753, replayId);
            }
        }
    }
}
