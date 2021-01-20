using NLog;
using NLog.Config;
using NLog.Targets;
using System;

namespace Centaurus
{
    public class LogConfigureHelper
    {
        /// <summary>
        /// The verbose option overrides the silent one.
        /// </summary>
        /// <param name="silent">Log only errors</param>
        /// <param name="verbose">Log every message</param>
        public static void Configure(string logsDirectory, bool silent = false, bool verbose = false)
        {
            var config = new LoggingConfiguration();
            var layout = "${longdate} " +
                "| ${uppercase:${level}} " +
                "| ${message} " +
                "${when:when=length('${exception}')>0:Inner=\n}" +
                "${exception:format=toString}";

            var logfile = new FileTarget("logfile")
            {
                FileName = Environment.CurrentDirectory + "/" + logsDirectory.Replace('\\', '/').Trim('/') + "/${shortdate}.log",
                Layout = layout,
                MaxArchiveFiles = 7,//one week
                ArchiveEvery = FileArchivePeriod.Day
            };

            var logconsole = new ConsoleTarget("logconsole") { Layout = layout };

            if (verbose)
            {
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, logconsole);
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, logfile);
            }
            else if (silent)
            {
                config.AddRule(LogLevel.Warn, LogLevel.Fatal, logconsole);
                config.AddRule(LogLevel.Warn, LogLevel.Fatal, logfile);
            }
            else
            {
                config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
                config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);
            }

            LogManager.Configuration = config;
        }
    }
}
