using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyVersion("4.0.1.0")]

[assembly: AssemblyCopyright("Copyright © Leonard Thieu 2017")]
[assembly: AssemblyProduct("toofz")]

[assembly: AssemblyTitle("toofz Leaderboards Service")]

[assembly: ComVisible(false)]

[assembly: InternalsVisibleTo("LeaderboardsService.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

[assembly: log4net.Config.XmlConfigurator(Watch = true, ConfigFile = "log.config")]
