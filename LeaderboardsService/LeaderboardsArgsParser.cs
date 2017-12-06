using System;
using System.IO;
using System.Reflection;
using Mono.Options;
using toofz.Services.LeaderboardsService.Properties;

namespace toofz.Services.LeaderboardsService
{
    internal sealed class LeaderboardsArgsParser : ArgsParser<LeaderboardsOptions, ILeaderboardsSettings>
    {
        public LeaderboardsArgsParser(TextReader inReader, TextWriter outWriter, TextWriter errorWriter) : base(inReader, outWriter, errorWriter) { }

        protected override string EntryAssemblyFileName { get; } = Path.GetFileName(Assembly.GetExecutingAssembly().Location);

        protected override void OnParsing(Type settingsType, OptionSet optionSet, LeaderboardsOptions options)
        {
            base.OnParsing(settingsType, optionSet, options);

            optionSet.Add("username=", GetDescription(settingsType, nameof(Settings.SteamUserName)), username => options.SteamUserName = username);
            optionSet.Add("password:", GetDescription(settingsType, nameof(Settings.SteamPassword)), password => options.SteamPassword = password);
            optionSet.Add("dailies=", GetDescription(settingsType, nameof(Settings.DailyLeaderboardsPerUpdate)), (int? dailies) => options.DailyLeaderboardsPerUpdate = dailies);
            optionSet.Add("timeout=", GetDescription(settingsType, nameof(Settings.SteamClientTimeout)), (TimeSpan? timeout) => options.SteamClientTimeout = timeout);
        }

        protected override void OnParsed(LeaderboardsOptions options, ILeaderboardsSettings settings)
        {
            base.OnParsed(options, settings);

            var iterations = settings.KeyDerivationIterations;

            #region SteamUserName

            var steamUserName = options.SteamUserName;
            if (!string.IsNullOrEmpty(steamUserName))
            {
                settings.SteamUserName = steamUserName;
            }

            #endregion

            #region SteamPassword

            var steamPassword = options.SteamPassword;
            if (ShouldPrompt(steamPassword))
            {
                steamPassword = ReadOption("Steam password");
            }

            if (steamPassword != "")
            {
                settings.SteamPassword = new EncryptedSecret(steamPassword, iterations);
            }

            #endregion

            #region DailyLeaderboardsPerUpdate

            var dailyLeaderboardsPerUpdate = options.DailyLeaderboardsPerUpdate;
            if (dailyLeaderboardsPerUpdate != null)
            {
                settings.DailyLeaderboardsPerUpdate = dailyLeaderboardsPerUpdate.Value;
            }

            #endregion

            #region SteamClientTimeout

            var steamClientTimeout = options.SteamClientTimeout;
            if (steamClientTimeout != null)
            {
                settings.SteamClientTimeout = steamClientTimeout.Value;
            }

            #endregion
        }
    }
}
