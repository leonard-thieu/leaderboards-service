using System.Collections.Generic;
using System.Collections.ObjectModel;
using SteamKit2;
using static SteamKit2.SteamUserStats;
using static SteamKit2.SteamUserStats.LeaderboardEntriesCallback;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    internal class LeaderboardEntriesCallback : ILeaderboardEntriesCallback
    {
        public EResult Result { get; set; }
        public int EntryCount { get; set; }
        ReadOnlyCollection<ILeaderboardEntry> ILeaderboardEntriesCallback.Entries => Entries.AsReadOnly();
        public JobID JobID { get; set; }

        public List<ILeaderboardEntry> Entries { get; } = new List<ILeaderboardEntry>();
    }

    internal class LeaderboardEntry : ILeaderboardEntry
    {
        public SteamID SteamID { get; set; } = new SteamID();
        public int GlobalRank { get; set; }
        public int Score { get; set; }
        public UGCHandle UGCId { get; set; } = new UGCHandle();
        public ReadOnlyCollection<int> Details
        {
            get => details.AsReadOnly();
        }
        private readonly List<int> details = new List<int> { 0, 0 };

        public int Zone
        {
            get { return details[0]; }
            set { details[0] = value; }
        }
        public int Level
        {
            get { return details[1]; }
            set { details[1] = value; }
        }
    }
}
