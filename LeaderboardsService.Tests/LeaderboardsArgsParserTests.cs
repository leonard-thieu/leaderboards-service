using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class LeaderboardsArgsParserTests
    {
        [TestClass]
        public class Parse
        {
            public Parse()
            {
                inReader = mockInReader.Object;
                parser = new LeaderboardsArgsParser(inReader, outWriter, errorWriter);

                mockSettings
                    .SetupAllProperties()
                    .SetupProperty(s => s.SteamUserName, "mySteamUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("mySteamPassword"))
                    .SetupProperty(s => s.LeaderboardsConnectionString, new EncryptedSecret("myLeaderboardsConnectionString"));
                settings = mockSettings.Object;
            }

            Mock<TextReader> mockInReader = new Mock<TextReader>(MockBehavior.Strict);
            TextReader inReader;
            TextWriter outWriter = new StringWriter();
            TextWriter errorWriter = new StringWriter();
            LeaderboardsArgsParser parser;
            Mock<ILeaderboardsSettings> mockSettings = new Mock<ILeaderboardsSettings>();
            ILeaderboardsSettings settings;

            [TestMethod]
            public void UserNameIsSpecified_SetSteamUserNameToUserName()
            {
                // Arrange
                string[] args = new[] { "--username=myUser" };

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual("myUser", settings.SteamUserName);
            }

            [TestMethod]
            public void PasswordIsSpecified_SetsSteamPasswordToEncryptedPassword()
            {
                // Arrange
                string[] args = new[] { "--password=myPassword" };
                var encrypted = new EncryptedSecret("myPassword");

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(encrypted.Decrypt(), settings.SteamPassword.Decrypt());
            }

            [TestMethod]
            public void ConnectionIsSpecified_SetsLeaderboardsConnectionStringToEncryptedConnection()
            {
                // Arrange
                string[] args = new[] { "--connection=myConnectionString" };
                var encrypted = new EncryptedSecret("myConnectionString");

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [TestMethod]
            public void ConnectionIsNotSpecifiedAndLeaderboardsConnectionStringIsNull_SetsLeaderboardsConnectionStringToDefault()
            {
                // Arrange
                mockSettings
                    .SetupProperty(s => s.LeaderboardsConnectionString, null);
                string[] args = new string[0];
                var encrypted = new EncryptedSecret(LeaderboardsArgsParser.DefaultLeaderboardsConnectionString);

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [TestMethod]
            public void IkeyIsSpecified_SetsLeaderboardsInstrumentationKeyToIkey()
            {
                // Arrange
                string[] args = new[] { "--ikey=myInstrumentationKey" };

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual("myInstrumentationKey", settings.LeaderboardsInstrumentationKey);
            }
        }
    }
}
