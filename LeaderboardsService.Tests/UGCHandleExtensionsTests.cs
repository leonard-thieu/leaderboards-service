using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamKit2;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class UGCHandleExtensionsTests
    {
        [TestClass]
        public class ToReplayIdMethod
        {
            [TestMethod]
            public void UgcIdIsUInt64MaxValue_ReturnsNull()
            {
                // Arrange
                var ugcId = new UGCHandle(ulong.MaxValue);

                // Act
                var replayId = UGCHandleExtensions.ToReplayId(ugcId);

                // Assert
                Assert.IsNull(replayId);
            }

            [TestMethod]
            public void UgcIdIsZero_ReturnsNull()
            {
                // Arrange
                var ugcId = new UGCHandle(0);

                // Act
                var replayId = UGCHandleExtensions.ToReplayId(ugcId);

                // Assert
                Assert.IsNull(replayId);
            }

            [TestMethod]
            public void ReturnsReplayId()
            {
                // Arrange
                var ugcId = new UGCHandle(3489753984753);

                // Act
                var replayId = UGCHandleExtensions.ToReplayId(ugcId);

                // Assert
                Assert.AreEqual(3489753984753, replayId);
            }
        }
    }
}
