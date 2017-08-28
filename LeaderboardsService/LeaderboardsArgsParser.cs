﻿using System;
using System.IO;
using System.Reflection;
using Mono.Options;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;
using toofz.Services;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    sealed class LeaderboardsArgsParser : ArgsParser<LeaderboardsOptions, ILeaderboardsSettings>
    {
        internal const string DefaultLeaderboardsConnectionString = "Data Source=localhost;Initial Catalog=NecroDancer;Integrated Security=SSPI;";

        public LeaderboardsArgsParser(TextReader inReader, TextWriter outWriter, TextWriter errorWriter, int iterations) :
            base(inReader, outWriter, errorWriter)
        {
            this.iterations = iterations;
        }

        readonly int iterations;

        protected override string EntryAssemblyFileName { get; } = Path.GetFileName(Assembly.GetExecutingAssembly().Location);

        protected override void OnParsing(Type settingsType, OptionSet optionSet, LeaderboardsOptions options)
        {
            base.OnParsing(settingsType, optionSet, options);

            optionSet.Add("username=", GetDescription(settingsType, nameof(Settings.SteamUserName)), username => options.SteamUserName = username);
            optionSet.Add("password:", GetDescription(settingsType, nameof(Settings.SteamPassword)), password => options.SteamPassword = password);
            optionSet.Add("connection:", GetDescription(settingsType, nameof(Settings.LeaderboardsConnectionString)), connection => options.LeaderboardsConnectionString = connection);
        }

        protected override void OnParsed(LeaderboardsOptions options, ILeaderboardsSettings settings)
        {
            base.OnParsed(options, settings);

            #region SteamUserName

            var steamUserName = options.SteamUserName;
            if (!string.IsNullOrEmpty(steamUserName))
            {
                settings.SteamUserName = steamUserName;
            }
            else if (string.IsNullOrEmpty(settings.SteamUserName))
            {
                settings.SteamUserName = ReadOption("Steam user name");
            }

            #endregion

            #region SteamPassword

            var steamPassword = options.SteamPassword;
            if (ShouldPromptForRequiredSetting(steamPassword, settings.SteamPassword))
            {
                steamPassword = ReadOption("Steam password");
            }

            if (steamPassword != "")
            {
                settings.SteamPassword = new EncryptedSecret(steamPassword, iterations);
            }

            #endregion

            #region LeaderboardsConnectionString

            var leaderboardsConnectionString = options.LeaderboardsConnectionString;
            if (!string.IsNullOrEmpty(leaderboardsConnectionString))
            {
                settings.LeaderboardsConnectionString = new EncryptedSecret(leaderboardsConnectionString, iterations);
            }
            else if (settings.LeaderboardsConnectionString == null)
            {
                leaderboardsConnectionString = leaderboardsConnectionString == "" ?
                    DefaultLeaderboardsConnectionString :
                    ReadOption("Leaderboards connection string");
                settings.LeaderboardsConnectionString = new EncryptedSecret(leaderboardsConnectionString, iterations);
            }

            #endregion
        }
    }
}
