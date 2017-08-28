using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.TestsShared;

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
                parser = new LeaderboardsArgsParser(inReader, outWriter, errorWriter, Constants.Iterations);
            }

            Mock<TextReader> mockInReader = new Mock<TextReader>(MockBehavior.Strict);
            TextReader inReader;
            TextWriter outWriter = new StringWriter();
            TextWriter errorWriter = new StringWriter();
            LeaderboardsArgsParser parser;

            [TestMethod]
            public void HelpFlagIsSpecified_ShowUsageInformation()
            {
                // Arrange
                string[] args = new[] { "--help" };
                ILeaderboardsSettings settings = Settings.Default;
                settings.Reload();

                // Act
                parser.Parse(args, settings);

                // Assert
                AssertHelper.NormalizedAreEqual(@"
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
", outWriter.ToString());
            }

            #region SteamUserName

            [TestMethod]
            public void UserNameIsSpecified_SetSteamUserName()
            {
                // Arrange
                string[] args = new[] { "--username=myUserName" };
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", Constants.Iterations),
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual("myUserName", settings.SteamUserName);
            }

            [TestMethod]
            public void UserNameIsNotSpecifiedAndSteamUserNameIsNotSet_PromptsUserForUserNameAndSetsSteamUserName()
            {
                // Arrange
                string[] args = new string[0];
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = null,
                    SteamPassword = new EncryptedSecret("a", Constants.Iterations),
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myUserName");

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual("myUserName", settings.SteamUserName);
            }

            [TestMethod]
            public void UserNameIsNotSpecifiedAndSteamUserNameIsSet_DoesNotSetSteamUserName()
            {
                // Arrange
                string[] args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", Constants.Iterations));
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(s => s.SteamUserName = It.IsAny<string>(), Times.Never);
            }

            #endregion

            #region SteamPassword

            [TestMethod]
            public void PasswordIsSpecified_SetsSteamPassword()
            {
                // Arrange
                string[] args = new[] { "--password=myPassword" };
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", Constants.Iterations),
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myPassword", Constants.Iterations);
                Assert.AreEqual(encrypted.Decrypt(), settings.SteamPassword.Decrypt());
            }

            [TestMethod]
            public void PasswordFlagIsSpecified_PromptsUserForPasswordAndSetsSteamPassword()
            {
                // Arrange
                string[] args = new[] { "--password" };
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", Constants.Iterations),
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myPassword");

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myPassword", Constants.Iterations);
                Assert.AreEqual(encrypted.Decrypt(), settings.SteamPassword.Decrypt());
            }

            [TestMethod]
            public void PasswordFlagIsNotSpecifiedAndSteamPasswordIsNotSet_PromptsUserForPasswordAndSetsSteamPassword()
            {
                // Arrange
                string[] args = new string[0];
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = null,
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myPassword");

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myPassword", Constants.Iterations);
                Assert.AreEqual(encrypted.Decrypt(), settings.SteamPassword.Decrypt());
            }

            [TestMethod]
            public void PasswordFlagIsNotSpecifiedAndSteamPasswordIsSet_DoesNotSetSteamPassword()
            {
                // Arrange
                string[] args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", Constants.Iterations));
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(s => s.SteamPassword = It.IsAny<EncryptedSecret>(), Times.Never);
            }

            #endregion

            #region LeaderboardsConnectionString

            [TestMethod]
            public void ConnectionIsSpecified_SetsLeaderboardsConnectionString()
            {
                // Arrange
                string[] args = new[] { "--connection=myConnectionString" };
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", Constants.Iterations),
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myConnectionString", Constants.Iterations);
                Assert.AreEqual(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [TestMethod]
            public void ConnectionFlagIsSpecified_PromptsUserForConnectionAndSetsLeaderboardsConnectionString()
            {
                // Arrange
                string[] args = new[] { "--connection" };
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", Constants.Iterations),
                };
                mockInReader
                    .SetupSequence(r => r.ReadLine())
                    .Returns("myConnectionString");

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret("myConnectionString", Constants.Iterations);
                Assert.AreEqual(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [TestMethod]
            public void ConnectionFlagIsNotSpecifiedAndLeaderboardsConnectionStringIsNotSet_SetsLeaderboardsConnectionStringToDefault()
            {
                // Arrange
                string[] args = new string[0];
                ILeaderboardsSettings settings = new SimpleLeaderboardsSettings
                {
                    SteamUserName = "a",
                    SteamPassword = new EncryptedSecret("a", Constants.Iterations),
                };

                // Act
                parser.Parse(args, settings);

                // Assert
                var encrypted = new EncryptedSecret(LeaderboardsArgsParser.DefaultLeaderboardsConnectionString, Constants.Iterations);
                Assert.AreEqual(encrypted.Decrypt(), settings.LeaderboardsConnectionString.Decrypt());
            }

            [TestMethod]
            public void ConnectionFlagIsNotSpecifiedAndLeaderboardsConnectionStringIsSet_DoesNotSetLeaderboardsConnectionString()
            {
                // Arrange
                string[] args = new string[0];
                var mockSettings = new Mock<ILeaderboardsSettings>();
                mockSettings
                    .SetupProperty(s => s.SteamUserName, "myUserName")
                    .SetupProperty(s => s.SteamPassword, new EncryptedSecret("a", Constants.Iterations))
                    .SetupProperty(s => s.LeaderboardsConnectionString, new EncryptedSecret("a", Constants.Iterations));
                var settings = mockSettings.Object;

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(s => s.LeaderboardsConnectionString = It.IsAny<EncryptedSecret>(), Times.Never);
            }

            #endregion
        }
    }
}
