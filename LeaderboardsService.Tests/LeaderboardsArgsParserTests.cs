using System;
using System.IO;
using Moq;
using toofz.Services.LeaderboardsService.Properties;
using Xunit;

namespace toofz.Services.LeaderboardsService.Tests
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

            private readonly Mock<TextReader> mockInReader = new Mock<TextReader>(MockBehavior.Strict);
            private readonly TextReader inReader;
            private readonly TextWriter outWriter = new StringWriter();
            private readonly TextWriter errorWriter = new StringWriter();
            private readonly LeaderboardsArgsParser parser;

            [Fact]
            public void HelpFlagIsSpecified_ShowUsageInformation()
            {
                // Arrange
                string[] args = { "--help" };
                ILeaderboardsSettings settings = Settings.Default;
                settings.Reload();

                // Act
                parser.Parse(args, settings);

                // Assert
                var output = outWriter.ToString();
                Assert.Equal(@"
Usage: LeaderboardsService.exe [options]

options:
  --help                Shows usage information.
  --interval=VALUE      The minimum amount of time that should pass between each cycle.
  --delay=VALUE         The amount of time to wait after a cycle to perform garbage collection.
  --ikey=VALUE          An Application Insights instrumentation key.
  --iterations=VALUE    The number of rounds to execute a key derivation function.
  --connection[=VALUE]  The connection string used to connect to the leaderboards database.
  --username=VALUE      The user name used to log on to Steam.
  --password[=VALUE]    The password used to log on to Steam.
  --dailies=VALUE       The maxinum number of daily leaderboards to update per cycle.
  --timeout=VALUE       The amount of time to wait before a request to the Steam Client API times out.
", output, ignoreLineEndingDifferences: true);
            }

            #region SteamUserName

            [Fact]
            public void UserNameIsSpecified_SetSteamUserName()
            {
                // Arrange
                string[] args = { "--username=myUserName" };
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
            public void UserNameIsNotSpecifiedAndSteamUserNameIsSet_DoesNotSetSteamUserName()
            {
                // Arrange
                string[] args = { };
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
                string[] args = { "--password=myPassword" };
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
                string[] args = { "--password" };
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
            public void PasswordFlagIsNotSpecifiedAndSteamPasswordIsSet_DoesNotSetSteamPassword()
            {
                // Arrange
                string[] args = { };
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

            #region DailyLeaderboardsPerUpdate

            [Fact]
            public void DailiesIsSpecified_SetsDailyLeaderboardsPerUpdate()
            {
                // Arrange
                string[] args = { "--dailies=10" };
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
                string[] args = { };
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

            #region SteamClientTimeout

            [Fact]
            public void TimeoutIsSpecified_SetsSteamClientTimeout()
            {
                // Arrange
                string[] args = { "--timeout=00:01:00" };
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
                string[] args = { };
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

            #endregion
        }
    }
}
