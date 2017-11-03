namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal static class UInt64Extensions
    {
        public static long? ToReplayId(this ulong ugcHandle)
        {
            var ugcId = (long)ugcHandle;
            switch (ugcId)
            {
                case -1:
                case 0: return null;
                default: return ugcId;
            }
        }
    }
}
