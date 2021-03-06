﻿using System.IO;
using System.Xml.Serialization;
using toofz.Steam.CommunityData;

namespace toofz.Services.LeaderboardsService.Tests
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
