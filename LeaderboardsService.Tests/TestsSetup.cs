using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    [TestClass]
    public class TestsSetup
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            // The default value can cause tests to execute slowly. Changing the number of iterations 
            // should not affect the results of tests but will allow them to execute quickly.
            Secrets.Iterations = 1000;
        }
    }
}
