using SteamKit2;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    static class UGCHandleExtensions
    {
        public static long? ToReplayId(this UGCHandle ugcHandle)
        {
            return ((ulong)ugcHandle).ToReplayId();
        }
    }
}
