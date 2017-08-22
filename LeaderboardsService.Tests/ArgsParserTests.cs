using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.TestsShared;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService.Tests
{
    class ArgsParserTests
    {
        [TestClass]
        public class Constructor
        {
            [TestMethod]
            public void InReaderIsNull_ThrowsArgumentNullException()
            {
                // Arrange
                TextReader inReader = null;
                TextWriter outWriter = TextWriter.Null;
                TextWriter errorWriter = TextWriter.Null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    new ArgsParser(inReader, outWriter, errorWriter);
                });
            }

            [TestMethod]
            public void OutWriterIsNull_ThrowsArgumentNullException()
            {
                // Arrange
                TextReader inReader = TextReader.Null;
                TextWriter outWriter = null;
                TextWriter errorWriter = TextWriter.Null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    new ArgsParser(inReader, outWriter, errorWriter);
                });
            }

            [TestMethod]
            public void ErrorWriterIsNull_ThrowsArgumentNullException()
            {
                // Arrange
                TextReader inReader = TextReader.Null;
                TextWriter outWriter = TextWriter.Null;
                TextWriter errorWriter = null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    new ArgsParser(inReader, outWriter, errorWriter);
                });
            }

            [TestMethod]
            public void ReturnsInstance()
            {
                // Arrange
                TextReader inReader = TextReader.Null;
                TextWriter outWriter = TextWriter.Null;
                TextWriter errorWriter = TextWriter.Null;

                // Act
                var parser = new ArgsParser(inReader, outWriter, errorWriter);

                // Assert
                Assert.IsInstanceOfType(parser, typeof(ArgsParser));
            }
        }

        [TestClass]
        public class Parse
        {
            public Parse()
            {
                inReader = mockInReader.Object;
                parser = new ArgsParser(inReader, outWriter, errorWriter);

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
            ArgsParser parser;
            Mock<ISettings> mockSettings = new Mock<ISettings>();
            ISettings settings;

            [TestMethod]
            public void ArgsIsNull_ThrowsArgumentNullException()
            {
                // Arrange
                string[] args = null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    parser.Parse(args, settings);
                });
            }

            [TestMethod]
            public void SettingsIsNulll_ThrowsArgumentNullException()
            {
                // Arrange
                string[] args = new string[0];
                ISettings settings = null;

                // Act -> Assert
                Assert.ThrowsException<ArgumentNullException>(() =>
                {
                    parser.Parse(args, settings);
                });
            }

            [TestMethod]
            public void ExtraArg_ShowsError()
            {
                // Arrange
                string[] args = new[] { "myExtraArg" };

                // Act
                parser.Parse(args, settings);
                var error = errorWriter.ToString();

                // Assert
                AssertHelper.NormalizedAreEqual(@"LeaderboardsService.exe: 'myExtraArg' is not a valid option.
", error);
            }

            [TestMethod]
            public void ExtraArg_Returns1()
            {
                // Arrange
                string[] args = new[] { "myExtraArg" };

                // Act
                var exitCode = parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(1, exitCode);
            }

            [TestMethod]
            public void Help_ShowsHelp()
            {
                // Arrange
                string[] args = new[] { "--help" };

                // Act
                parser.Parse(args, settings);
                var output = outWriter.ToString();

                // Assert
                AssertHelper.NormalizedAreEqual(@"
Usage: LeaderboardsService.exe [options]

options:
  --interval=VALUE      The minimum amount of time that should pass between each cycle.
  --delay=VALUE         The amount of time to wait after a cycle to perform garbage collection.
  --username=VALUE      The user name used to log on to Steam.
  --password[=VALUE]    The password used to log on to Steam.
  --connection[=VALUE]  The connection string used to connect to the leaderboards database.
  --ikey=VALUE          An Application Insights instrumentation key.
  --help                Shows help.
", output);
            }

            [TestMethod]
            public void Help_Returns0()
            {
                // Arrange
                string[] args = new[] { "--help" };

                // Act
                var exitCode = parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(0, exitCode);
            }

            [TestMethod]
            public void IntervalIsNotSpecified_SetsUpdateIntervalToItsCurrentValue()
            {
                // Arrange
                string[] args = new string[0];
                var interval = TimeSpan.FromSeconds(20);
                mockSettings.SetupProperty(settings => settings.UpdateInterval, interval);

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(settings => settings.UpdateInterval = interval);
            }

            [TestMethod]
            public void IntervalIsSpecified_SetsUpdateIntervalToInterval()
            {
                // Arrange
                string[] args = new[] { "--interval=00:10:00" };
                var interval = TimeSpan.FromSeconds(20);
                mockSettings.SetupProperty(settings => settings.UpdateInterval, interval);

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(TimeSpan.FromMinutes(10), settings.UpdateInterval);
            }

            [TestMethod]
            public void DelayIsNotSpecified_SetsDelayBeforeGCToItsCurrentValue()
            {
                // Arrange
                string[] args = new string[0];
                var interval = TimeSpan.FromSeconds(20);
                mockSettings.SetupProperty(settings => settings.DelayBeforeGC, interval);

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.VerifySet(settings => settings.DelayBeforeGC = interval);
            }

            [TestMethod]
            public void DelayIsSpecified_SetsDelayBeforeGCToDelay()
            {
                // Arrange
                string[] args = new[] { "--delay=00:10:00" };
                var interval = TimeSpan.FromSeconds(20);
                mockSettings.SetupProperty(settings => settings.DelayBeforeGC, interval);

                // Act
                parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(TimeSpan.FromMinutes(10), settings.DelayBeforeGC);
            }

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
                var encrypted = new EncryptedSecret(ArgsParser.DefaultLeaderboardsConnectionString);

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

            [TestMethod]
            public void SavesSettings()
            {
                // Arrange
                string[] args = new string[0];

                // Act
                parser.Parse(args, settings);

                // Assert
                mockSettings.Verify(s => s.Save());
            }

            [TestMethod]
            public void Returns0()
            {
                // Arrange
                string[] args = new string[0];

                // Act
                var exitCode = parser.Parse(args, settings);

                // Assert
                Assert.AreEqual(0, exitCode);
            }
        }
    }
}
