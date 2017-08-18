using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyVersion("2.1.2.0")]

[assembly: AssemblyCopyright("Copyright © Leonard Thieu 2017")]
[assembly: AssemblyProduct("toofz")]

[assembly: AssemblyTitle("toofz Leaderboards Service")]

[assembly: ComVisible(false)]

[assembly: InternalsVisibleTo("LeaderboardsService.Tests")]

[assembly: log4net.Config.XmlConfigurator(Watch = true, ConfigFile = "log.config")]
