using System;
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

        public LeaderboardsArgsParser(TextReader inReader, TextWriter outWriter, TextWriter errorWriter) : base(inReader, outWriter, errorWriter) { }

        protected override string EntryAssemblyFileName { get; } = Path.GetFileName(Assembly.GetExecutingAssembly().Location);

        protected override void OnParsing(Type settingsType, OptionSet optionSet, LeaderboardsOptions options)
        {
            base.OnParsing(settingsType, optionSet, options);

            optionSet.Add("username=", GetDescription(settingsType, nameof(Settings.SteamUserName)), username => options.SteamUserName = username);
            optionSet.Add("password:", GetDescription(settingsType, nameof(Settings.SteamPassword)), password => options.SteamPassword = password);
            optionSet.Add("connection:", GetDescription(settingsType, nameof(Settings.LeaderboardsConnectionString)), connection => options.LeaderboardsConnectionString = connection);
            optionSet.Add("ikey=", GetDescription(settingsType, nameof(Settings.LeaderboardsInstrumentationKey)), key => options.LeaderboardsInstrumentationKey = key);
        }

        protected override void OnParsed(LeaderboardsOptions options, ILeaderboardsSettings settings)
        {
            base.OnParsed(options, settings);

            #region SteamUserName

            if (!string.IsNullOrEmpty(options.SteamUserName))
            {
                settings.SteamUserName = options.SteamUserName;
            }

            while (string.IsNullOrEmpty(settings.SteamUserName))
            {
                OutWriter.Write("Steam user name: ");
                settings.SteamUserName = InReader.ReadLine();
            }

            #endregion

            #region SteamPassword

            if (!string.IsNullOrEmpty(options.SteamPassword))
            {
                settings.SteamPassword = new EncryptedSecret(options.SteamPassword);
            }

            // When steamPassword == null, the user has indicated that they wish to be prompted to enter the password.
            while (settings.SteamPassword == null || options.SteamPassword == null)
            {
                OutWriter.Write("Steam password: ");
                options.SteamPassword = InReader.ReadLine();
                if (!string.IsNullOrEmpty(options.SteamPassword))
                {
                    settings.SteamPassword = new EncryptedSecret(options.SteamPassword);
                }
            }

            #endregion

            #region LeaderboardsConnectionString

            if (!string.IsNullOrEmpty(options.LeaderboardsConnectionString))
            {
                settings.LeaderboardsConnectionString = new EncryptedSecret(options.LeaderboardsConnectionString);
            }
            else
            {
                if (options.LeaderboardsConnectionString == "" && settings.LeaderboardsConnectionString == null)
                {
                    settings.LeaderboardsConnectionString = new EncryptedSecret(DefaultLeaderboardsConnectionString);
                }
                else
                {
                    // When leaderboardsConnectionString == null, the user has indicated that they wish to be prompted to enter the connection string.
                    while (options.LeaderboardsConnectionString == null)
                    {
                        OutWriter.Write("Leaderboards connection string: ");
                        options.LeaderboardsConnectionString = InReader.ReadLine();
                        if (!string.IsNullOrEmpty(options.LeaderboardsConnectionString))
                        {
                            settings.LeaderboardsConnectionString = new EncryptedSecret(options.LeaderboardsConnectionString);
                        }
                    }
                }
            }

            #endregion

            #region LeaderboardsInstrumentationKey

            if (!string.IsNullOrEmpty(options.LeaderboardsInstrumentationKey))
            {
                settings.LeaderboardsInstrumentationKey = options.LeaderboardsInstrumentationKey;
            }

            #endregion
        }
    }
}
