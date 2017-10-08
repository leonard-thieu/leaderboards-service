using System.IO;
using System.Xml.Serialization;
using toofz.NecroDancer.Leaderboards.Steam.CommunityData;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    static class DataHelper
    {
        static readonly XmlSerializer LeaderboardsEnvelopeSerializer = new XmlSerializer(typeof(LeaderboardsEnvelope));

        public static LeaderboardsEnvelope DeserializeLeaderboardsEnvelope(string xml)
        {
            using (var sr = new StringReader(xml))
            {
                return (LeaderboardsEnvelope)LeaderboardsEnvelopeSerializer.Deserialize(sr);
            }
        }

        static readonly XmlSerializer LeaderboardEntriesEnvelopeSerializer = new XmlSerializer(typeof(LeaderboardEntriesEnvelope));

        public static LeaderboardEntriesEnvelope DeserializeLeaderboardEntriesEnvelope(string xml)
        {
            using (var sr = new StringReader(xml))
            {
                return (LeaderboardEntriesEnvelope)LeaderboardEntriesEnvelopeSerializer.Deserialize(sr);
            }
        }
    }
}
