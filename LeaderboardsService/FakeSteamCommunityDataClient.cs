using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    [ExcludeFromCodeCoverage]
    internal sealed class FakeSteamCommunityDataClient : ISteamCommunityDataClient
    {
        private static readonly XmlSerializer LeaderboardsEnvelopeSerializer = XmlSerializer.FromTypes(new[] { typeof(LeaderboardsEnvelope) })[0];
        private static readonly XmlSerializer LeaderboardEntriesEnvelopeSerializer = XmlSerializer.FromTypes(new[] { typeof(LeaderboardEntriesEnvelope) })[0];

        public FakeSteamCommunityDataClient()
        {
            var leaderboardsPath = Path.Combine("Data", "SteamCommunityData", "Leaderboards");
            leaderboardsFiles = Directory.GetFiles(leaderboardsPath, "*.xml");
            var entriesPath = Path.Combine("Data", "SteamCommunityData", "Entries");
            entriesFiles = Directory.GetFiles(entriesPath, "*.xml");
        }

        private readonly string[] leaderboardsFiles;
        private readonly string[] entriesFiles;

        public Task<LeaderboardsEnvelope> GetLeaderboardsAsync(
            string communityGameName,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var fileName = $"{communityGameName}.xml";
            var leaderboardFile = leaderboardsFiles.FirstOrDefault(f => Path.GetFileName(f) == fileName);

            using (var fs = File.OpenRead(leaderboardFile))
            {
                var leaderboards = (LeaderboardsEnvelope)LeaderboardsEnvelopeSerializer.Deserialize(fs);

                return Task.FromResult(leaderboards);
            }
        }

        public Task<LeaderboardEntriesEnvelope> GetLeaderboardEntriesAsync(
            string communityGameName,
            int leaderboardId,
            GetLeaderboardEntriesParams @params = null,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            @params = @params ?? new GetLeaderboardEntriesParams();

            var fileName = $"{communityGameName}_{leaderboardId}_{@params.StartRange}.xml";
            var entriesFile = entriesFiles.FirstOrDefault(f => Path.GetFileName(f) == fileName);

            using (var fs = File.OpenRead(entriesFile))
            {
                var entries = (LeaderboardEntriesEnvelope)LeaderboardEntriesEnvelopeSerializer.Deserialize(fs);

                return Task.FromResult(entries);
            }
        }

        public void Dispose() { }
    }
}
