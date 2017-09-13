using SteamKit2;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    static class UGCHandleExtensions
    {
        public static long? ToReplayId(this UGCHandle ugcHandle)
        {
            var ugcId = (long)(ulong)ugcHandle;
            switch (ugcId)
            {
                case -1:
                case 0: return null;
                default: return ugcId;
            }
        }
    }
}
