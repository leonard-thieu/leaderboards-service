using System;
using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
{
    public class UInt64ExtensionsTests
    {
        public class ToReplayIdMethod
        {
            [DisplayFact("UgcId", nameof(UInt64), nameof(UInt64.MaxValue))]
            public void UgcIdIsUInt64MaxValue_ReturnsNull()
            {
                // Arrange
                var ugcId = ulong.MaxValue;

                // Act
                var replayId = ugcId.ToReplayId();

                // Assert
                Assert.Null(replayId);
            }

            [DisplayFact("UgcId")]
            public void UgcIdIsZero_ReturnsNull()
            {
                // Arrange
                var ugcId = 0UL;

                // Act
                var replayId = ugcId.ToReplayId();

                // Assert
                Assert.Null(replayId);
            }

            [DisplayFact]
            public void ReturnsReplayId()
            {
                // Arrange
                var ugcId = 3489753984753UL;

                // Act
                var replayId = ugcId.ToReplayId();

                // Assert
                Assert.Equal(3489753984753, replayId);
            }
        }
    }
}
