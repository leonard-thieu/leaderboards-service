using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class LeaderboardsWorkerTests
    {
        [TestClass]
        public class Constructor
        {
            [TestMethod]
            public void LeaderboardsConnectionStringIsNull_ThrowsArgumentNullException()
            {
                // Arrange
                var appId = 247080U;
                string leaderboardsConnectionString = null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    new LeaderboardsWorker(appId, leaderboardsConnectionString);
                });
            }

            [TestMethod]
            public void ReturnsInstance()
            {
                // Arrange
                var appId = 247080U;
                var leaderboardsConnectionString = "myConnectionString";

                // Act
                var worker = new LeaderboardsWorker(appId, leaderboardsConnectionString);

                // Assert
                Assert.IsInstanceOfType(worker, typeof(LeaderboardsWorker));
            }
        }
    }
}
