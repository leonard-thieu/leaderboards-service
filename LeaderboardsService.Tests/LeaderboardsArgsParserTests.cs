using System;
using System.IO;
using Moq;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.Services;
using Xunit;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    public class LeaderboardsArgsParserTests
    {
        public class Parse
        {
            public Parse()
            {
                inReader = mockInReader.Object;
                parser = new LeaderboardsArgsParser(inReader, outWriter, errorWriter);
            }

            private Mock<TextReader> mockInReader = new Mock<TextReader>(MockBehavior.Strict);
            private TextReader inReader;
            private TextWriter outWriter = new StringWriter();
            private TextWriter errorWriter = new StringWriter();
            private LeaderboardsArgsParser parser;

            [Fact]
            public void HelpFlagIsSpecified_ShowUsageInformation()
            {
                // Arrange
                string[] args = new[] { "--help" };
                ILeaderboardsSettings settings = Settings.Default;
                settings.Reload();

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.Equal(@"
Usage: LeaderboardsService.exe [options]

options:
  --help                Shows usage information.
  --interval=VALUE      The minimum amount of time that should pass between each cycle.
  --delay=VALUE         The amount of time to wait after a cycle to perform garbage collection.
  --ikey=VALUE          An Application Insights instrumentation key.
  --iterations=VALUE    The number of rounds to execute a key derivation function.
  --username=VALUE      The user name used to log on to Steam.
  --password[=VALUE]    The password used to log on to Steam.
  --connection[=VALUE]  The connection string used to connect to the leaderboards database.
  --dailies=VALUE       The maxinum number of daily leaderboards to update per cycle.
  --timeout=VALUE       The amount of time to wait before a request to the Steam Client API times out.
", outWriter.ToString(), ignoreLineEndingDifferences: true);
            }

            #region SteamUserName

            [Fact]
            public void UserNameIsSpecified_SetSteamUserName()
            {
                // Arrange
                string[] args = new[] { "--username=myUserName" };
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.Equal("myUserName", settings.SteamUserName);
            }

            [Fact]
            public void UserNameIsNotSpecifiedAndSteamUserNameIsNotSet_PromptsUserForUserNameAndSetsSteamUserName()
            {
                // Arrange
                string[] args = new string[0];
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = null,
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myUserName");

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.Equal("myUserName", settings.SteamUserName);
            }

            [Fact]
            public void UserNameIsNotSpecifiedAndSteamUserNameIsSet_DoesNotSetSteamUserName()
            {
                // Arrange
                string[] args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", 1))
                    .SetupProperty(s => s.KeyDerivationIterations, 1);
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(s => s.SteamUserName = It.IsAny<string>(), Times.Never);
            }

            #endregion

            #region SteamPassword

            [Fact]
            public void PasswordIsSpecified_SetsSteamPassword()
            {
                // Arrange
                string[] args = new[] { "--password=myPassword" };
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myPassword", 1);
                Assert.Equal(encrypted.Decrypt(), settings.SteamPassword.Decrypt());
            }

            [Fact]
            public void PasswordFlagIsSpecified_PromptsUserForPasswordAndSetsSteamPassword()
            {
                // Arrange
                string[] args = new[] { "--password" };
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myPassword");

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myPassword", 1);
                Assert.Equal(encrypted.Decrypt(), settings.SteamPassword.Decrypt());
            }

            [Fact]
            public void PasswordFlagIsNotSpecifiedAndSteamPasswordIsNotSet_PromptsUserForPasswordAndSetsSteamPassword()
            {
                // Arrange
                string[] args = new string[0];
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = null,
                    KeyDerivationIterations = 1,
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myPassword");

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myPassword", 1);
                Assert.Equal(encrypted.Decrypt(), settings.SteamPassword.Decrypt());
            }

            [Fact]
            public void PasswordFlagIsNotSpecifiedAndSteamPasswordIsSet_DoesNotSetSteamPassword()
            {
                // Arrange
                string[] args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", 1))
                    .SetupProperty(s => s.KeyDerivationIterations, 1);
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(s => s.SteamPassword = It.IsAny<EncryptedSecret>(), Times.Never);
            }

            #endregion

            #region LeaderboardsConnectionString

            [Fact]
            public void ConnectionIsSpecified_SetsLeaderboardsConnectionString()
            {
                // Arrange
                string[] args = new[] { "--connection=myConnectionString" };
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myConnectionString", 1);
                Assert.Equal(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [Fact]
            public void ConnectionFlagIsSpecified_PromptsUserForConnectionAndSetsLeaderboardsConnectionString()
            {
                // Arrange
                string[] args = new[] { "--connection" };
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myConnectionString");

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myConnectionString", 1);
                Assert.Equal(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [Fact]
            public void ConnectionFlagIsNotSpecifiedAndLeaderboardsConnectionStringIsNotSet_SetsLeaderboardsConnectionStringToDefault()
            {
                // Arrange
                string[] args = new string[0];
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret(LeaderboardsArgsParser.DefaultLeaderboardsConnectionString, 1);
                Assert.Equal(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [Fact]
            public void ConnectionFlagIsNotSpecifiedAndLeaderboardsConnectionStringIsSet_DoesNotSetLeaderboardsConnectionString()
            {
                // Arrange
                string[] args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", 1))
                    .SetupProperty(s => s.LeaderboardsConnectionString, new EncryptedSecret("a", 1));
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(s => s.LeaderboardsConnectionString = It.IsAny<EncryptedSecret>(), Times.Never);
            }

            #endregion

            #region DailyLeaderboardsPerUpdate

            [Fact]
            public void DailiesIsSpecified_SetsDailyLeaderboardsPerUpdate()
            {
                // Arrange
                string[] args = new[] { "--dailies=10" };
                ILeaderboardsSettings settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.Equal(10, settings.DailyLeaderboardsPerUpdate);
            }

            [Fact]
            public void DailiesIsNotSpecified_DoesNotSetDailyLeaderboardsPerUpdate()
            {
                // Arrange
                string[] args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", 1))
                    .SetupProperty(s => s.KeyDerivationIterations, 1);
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(s => s.DailyLeaderboardsPerUpdate = It.IsAny<int>(), Times.Never);
            }

            #endregion

            [Fact]
            public void TimeoutIsSpecified_SetsSteamClientTimeout()
            {
                // Arrange
                var args = new[] { "--timeout=00:01:00" };
                var settings = new StubLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", 1),
                    KeyDerivationIterations = 1,
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.Equal(TimeSpan.FromMinutes(1), settings.SteamClientTimeout);
            }

            [Fact]
            public void TimeoutIsNotSpecified_DoesNotSetSteamClientTimeout()
            {
                // Arrange
                var args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", 1))
                    .SetupProperty(s => s.KeyDerivationIterations, 1);
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);


                // Assert
                mockSettings.VerifySet(s => s.SteamClientTimeout = It.IsAny<TimeSpan>(), Times.Never);
            }
        }
    }
}
