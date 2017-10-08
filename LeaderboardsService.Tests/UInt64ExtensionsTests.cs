using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class UInt64ExtensionsTests
    {
        [TestClass]
        public class ToReplayIdMethod
        {
            [TestMethod]
            public void UgcIdIsUInt64MaxValue_ReturnsNull()
            {
                // Arrange
                var ugcId = ulong.MaxValue;

                // Act
                var replayId = UInt64Extensions.ToReplayId(ugcId);

                // Assert
                Assert.IsNull(replayId);
            }

            [TestMethod]
            public void UgcIdIsZero_ReturnsNull()
            {
                // Arrange
                var ugcId = 0UL;

                // Act
                var replayId = UInt64Extensions.ToReplayId(ugcId);

                // Assert
                Assert.IsNull(replayId);
            }

            [TestMethod]
            public void ReturnsReplayId()
            {
                // Arrange
                var ugcId = 3489753984753UL;

                // Act
                var replayId = UInt64Extensions.ToReplayId(ugcId);

                // Assert
                Assert.AreEqual(3489753984753, replayId);
            }
        }
    }
}
