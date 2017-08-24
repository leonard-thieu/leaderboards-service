using System;
using System.CodeDom.Compiler;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Options;
using toofz.NecroDancer.Leaderboards.LeaderboardsService.Properties;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    sealed class ArgsParser
    {
        internal const string DefaultLeaderboardsConnectionString = "Data Source=localhost;Initial Catalog=NecroDancer;Integrated Security=SSPI;";

        static readonly string ExecutingAssemblyName = Path.GetFileName(Assembly.GetExecutingAssembly().Location);

        static string GetDescription(string propName)
        {
            var propertyInfo = typeof(Settings).GetProperty(propName);
            var descAttr = propertyInfo.GetCustomAttribute<SettingsDescriptionAttribute>();

            return descAttr?.Description;
        }

        public ArgsParser(
            TextReader inReader,
            TextWriter outWriter,
            TextWriter errorWriter)
        {
            this.inReader = inReader ?? throw new ArgumentNullException(nameof(inReader));
            this.outWriter = outWriter ?? throw new ArgumentNullException(nameof(outWriter));
            this.errorWriter = errorWriter ?? throw new ArgumentNullException(nameof(errorWriter));
        }

        readonly TextReader inReader;
        readonly TextWriter outWriter;
        readonly TextWriter errorWriter;

        public int Parse(string[] args, ISettings settings)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            TimeSpan? updateInterval = null;
            TimeSpan? delayBeforeGC = null;
            string steamUserName = null;
            string steamPassword = "";
            string leaderboardsConnectionString = "";
            string ikey = null;
            var shouldShowHelp = false;

            var options = new OptionSet
            {
                { "interval=", GetDescription(nameof(Settings.UpdateInterval)), (TimeSpan interval) => updateInterval = interval },
                { "delay=", GetDescription(nameof(Settings.DelayBeforeGC)), (TimeSpan delay) => delayBeforeGC = delay },
                { "username=", GetDescription(nameof(Settings.SteamUserName)), username => steamUserName = username },
                { "password:", GetDescription(nameof(Settings.SteamPassword)), password => steamPassword = password },
                { "connection:", GetDescription(nameof(Settings.LeaderboardsConnectionString)), connection => leaderboardsConnectionString = connection },
                { "ikey=", GetDescription(nameof(Settings.LeaderboardsInstrumentationKey)), key => ikey = key },
                { "help", "Shows help.", h => shouldShowHelp = h != null },
            };

            try
            {
                var extraArgs = options.Parse(args);
                if (extraArgs.Any())
                {
                    var first = extraArgs.First();
                    throw new OptionException($"'{first}' is not a valid option.", first);
                }
            }
            catch (OptionException e)
            {
                errorWriter.WriteLine($"{ExecutingAssemblyName}: {e.Message}");
                WriteUsage(options);

                return 1;
            }

            if (shouldShowHelp)
            {
                WriteUsage(options);

                return 0;
            }

            #region UpdateInterval

            if (updateInterval != null)
            {
                settings.UpdateInterval = updateInterval.Value;
            }

            #endregion

            #region DelayBeforeGC

            if (delayBeforeGC != null)
            {
                settings.DelayBeforeGC = delayBeforeGC.Value;
            }

            #endregion

            #region SteamUserName

            if (!string.IsNullOrEmpty(steamUserName))
            {
                settings.SteamUserName = steamUserName;
            }

            while (string.IsNullOrEmpty(settings.SteamUserName))
            {
                outWriter.Write("Steam user name: ");
                settings.SteamUserName = inReader.ReadLine();
            }

            #endregion

            #region SteamPassword

            if (!string.IsNullOrEmpty(steamPassword))
            {
                settings.SteamPassword = new EncryptedSecret(steamPassword);
            }

            // When steamPassword == null, the user has indicated that they wish to be prompted to enter the password.
            while (settings.SteamPassword == null || steamPassword == null)
            {
                outWriter.Write("Steam password: ");
                steamPassword = inReader.ReadLine();
                if (!string.IsNullOrEmpty(steamPassword))
                {
                    settings.SteamPassword = new EncryptedSecret(steamPassword);
                }
            }

            #endregion

            #region LeaderboardsConnectionString

            if (!string.IsNullOrEmpty(leaderboardsConnectionString))
            {
                settings.LeaderboardsConnectionString = new EncryptedSecret(leaderboardsConnectionString);
            }
            else
            {
                if (leaderboardsConnectionString == "" && settings.LeaderboardsConnectionString == null)
                {
                    settings.LeaderboardsConnectionString = new EncryptedSecret(DefaultLeaderboardsConnectionString);
                }
                else
                {
                    // When leaderboardsConnectionString == null, the user has indicated that they wish to be prompted to enter the connection string.
                    while (leaderboardsConnectionString == null)
                    {
                        outWriter.Write("Leaderboards connection string: ");
                        leaderboardsConnectionString = inReader.ReadLine();
                        if (!string.IsNullOrEmpty(leaderboardsConnectionString))
                        {
                            settings.LeaderboardsConnectionString = new EncryptedSecret(leaderboardsConnectionString);
                        }
                    }
                }
            }

            #endregion

            #region LeaderboardsInstrumentationKey

            if (!string.IsNullOrEmpty(ikey))
            {
                settings.LeaderboardsInstrumentationKey = ikey;
            }

            #endregion

            settings.Save();

            return 0;
        }

        void WriteUsage(OptionSet options)
        {
            using (var indentedTextWriter = new IndentedTextWriter(outWriter, "  "))
            {
                indentedTextWriter.WriteLine();
                indentedTextWriter.WriteLine($"Usage: {ExecutingAssemblyName} [options]");
                indentedTextWriter.WriteLine();

                indentedTextWriter.WriteLine("options:");
                indentedTextWriter.Indent++;

                var maxPrototypeLength = options.Max(option =>
                {
                    switch (option.OptionValueType)
                    {
                        case OptionValueType.None:
                            return option.Prototype.Length;
                        case OptionValueType.Optional:
                            return option.Prototype.Length - 1 + "[=VALUE]".Length;
                        case OptionValueType.Required:
                            return option.Prototype.Length + "VALUE".Length;
                        default:
                            throw new NotSupportedException($"Unknown {nameof(OptionValueType)}: '{option.OptionValueType}'.");
                    }
                });
                foreach (var option in options)
                {
                    switch (option.OptionValueType)
                    {
                        case OptionValueType.None:
                            indentedTextWriter.WriteLine($"--{{0,-{maxPrototypeLength}}}  {option.Description}", option.Prototype);
                            break;
                        case OptionValueType.Optional:
                            indentedTextWriter.WriteLine($"--{{0,-{maxPrototypeLength}}}  {option.Description}", option.Prototype.TrimEnd(':') + "[=VALUE]");
                            break;
                        case OptionValueType.Required:
                            indentedTextWriter.WriteLine($"--{{0,-{maxPrototypeLength}}}  {option.Description}", option.Prototype + "VALUE");
                            break;
                        default:
                            // Unreachable. THe previous code block would have thrown already and OptionValueType should be treated as immutable.
                            break;
                    }
                }

                indentedTextWriter.Indent--;
            }
        }
    }
}
