using System.IO;
using System.Xml.Serialization;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    internal static class DataHelper
    {
        private static readonly XmlSerializer LeaderboardEntriesEnvelopeSerializer = new XmlSerializer(typeof(LeaderboardEntriesEnvelope));

        public static LeaderboardEntriesEnvelope DeserializeLeaderboardEntriesEnvelope(string xml)
        {
            using (var sr = new StringReader(xml))
            {
                return (LeaderboardEntriesEnvelope)LeaderboardEntriesEnvelopeSerializer.Deserialize(sr);
            }
        }
    }
}
