using System.IO;
using System.Reflection;
using toofz.Services.LeaderboardsService.Properties;

namespace toofz.Services.LeaderboardsService
{
    internal sealed class LeaderboardsArgsParser : ArgsParser<LeaderboardsOptions, ILeaderboardsSettings>
    {
        public LeaderboardsArgsParser(TextReader inReader, TextWriter outWriter, TextWriter errorWriter) : base(inReader, outWriter, errorWriter) { }

        protected override string EntryAssemblyFileName { get; } = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
    }
}
