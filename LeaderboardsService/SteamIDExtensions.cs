﻿using SteamKit2;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal static class SteamIDExtensions
    {
        public static long ToInt64(this SteamID steamId)
        {
            return (long)(ulong)steamId;
        }
    }
}
