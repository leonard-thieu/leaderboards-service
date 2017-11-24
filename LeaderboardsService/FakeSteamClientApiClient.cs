using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;
using SteamKit2;
using toofz.NecroDancer.Leaderboards.Steam.ClientApi;
using static SteamKit2.SteamUserStats;
using static SteamKit2.SteamUserStats.LeaderboardEntriesCallback;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    internal sealed class FakeSteamClientApiClient : ISteamClientApiClient
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FakeSteamClientApiClient));
        private static readonly Random random = new Random();
        private static readonly ILeaderboardEntryConverter LeaderboardEntryConverter = new ILeaderboardEntryConverter();

        public FakeSteamClientApiClient()
        {
            Log.Warn("Using test data for calls to Steam Client API. Set your Steam user name and password to use the actual Steam Client API.");
            Log.Warn("Run this application with --help to find out how to set your Steam user name and password.");

            var entriesPath = Path.Combine("Data", "SteamClientApi", "Entries");
            entriesFiles = Directory.GetFiles(entriesPath, "*.json");
        }

        private readonly string[] entriesFiles;

        public IProgress<long> Progress { get; set; }
        public TimeSpan Timeout { get; set; }

        public Task ConnectAndLogOnAsync() => Task.CompletedTask;
        public void Disconnect() { }
        public void Dispose() { }

        public Task<IFindOrCreateLeaderboardCallback> FindLeaderboardAsync(
            uint appId,
            string name,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            IFindOrCreateLeaderboardCallback callback = new FakeFindOrCreateLeaderboardCallback();

            return Task.FromResult(callback);
        }

        public Task<ILeaderboardEntriesCallback> GetLeaderboardEntriesAsync(
            uint appId,
            int lbid,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var i = random.Next(entriesFiles.Length);

            using (var sr = File.OpenText(entriesFiles[i]))
            {
                ILeaderboardEntriesCallback callback = JsonConvert.DeserializeObject<FakeLeaderboardEntriesCallback>(sr.ReadToEnd(), LeaderboardEntryConverter);
                Progress?.Report(sr.BaseStream.Length);

                return Task.FromResult(callback);
            }
        }

        private abstract class FakeCallbackMsg : ICallbackMsg
        {
            public JobID JobID { get; set; }
        }

        private sealed class FakeFindOrCreateLeaderboardCallback : FakeCallbackMsg, IFindOrCreateLeaderboardCallback
        {
            public EResult Result { get; set; }
            public int ID { get; set; }
            public int EntryCount { get; set; }
            public ELeaderboardSortMethod SortMethod { get; set; }
            public ELeaderboardDisplayType DisplayType { get; set; }
        }

        private sealed class FakeLeaderboardEntriesCallback : FakeCallbackMsg, ILeaderboardEntriesCallback
        {
            public EResult Result { get; set; }
            public int EntryCount { get; set; }
            public ReadOnlyCollection<ILeaderboardEntry> Entries { get; set; }
        }

        private sealed class FakeLeaderboardEntry : ILeaderboardEntry
        {
            public SteamID SteamID { get; set; }
            public int GlobalRank { get; set; }
            public int Score { get; set; }
            public UGCHandle UGCId { get; set; }
            public ReadOnlyCollection<int> Details { get; set; }
        }

        private sealed class ILeaderboardEntryConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(ILeaderboardEntry);
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => serializer.Deserialize<FakeLeaderboardEntry>(reader);
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();
        }
    }
}
