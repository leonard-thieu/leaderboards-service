using System.Runtime.CompilerServices;
using log4net.Config;

[assembly: InternalsVisibleTo("LeaderboardsService.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

[assembly: XmlConfigurator(Watch = true, ConfigFile = "log.config")]
