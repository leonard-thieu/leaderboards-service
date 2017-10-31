using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using log4net.Config;

[assembly: AssemblyVersion("4.1.7.0")]

[assembly: AssemblyCopyright("Copyright © Leonard Thieu 2017")]
[assembly: AssemblyProduct("toofz")]

[assembly: AssemblyTitle("toofz Leaderboards Service")]

[assembly: ComVisible(false)]

[assembly: InternalsVisibleTo("LeaderboardsService.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

[assembly: XmlConfigurator(Watch = true, ConfigFile = "log.config")]
