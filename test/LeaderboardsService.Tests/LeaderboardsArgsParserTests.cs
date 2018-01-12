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

            [DisplayFact]
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
", output, ignoreLineEndingDifferences: true);
            }
        }
    }
}
