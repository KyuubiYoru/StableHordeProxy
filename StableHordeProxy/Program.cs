using Fleck;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace StableHordeProxy;

internal static class Program
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private static void Main()
    {
        InitLogger();

        Server server = new Server();
        server.Start();

        Log.Info("Server started");


#if DEBUG
        Log.Info("Type 'exit' to stop the server");
        //Because in docker we can't use console input, we only check for exit in Debug mode (when running locally) otherwise we just wait forever
        //in docker ReadLine() is not blocking, so it would consume 100% CPU
        while (Console.ReadLine() != "exit") Log.Info("Type 'exit' to stop the server");

        return;
#endif
        //Release mode, just wait forever
        while (true) Thread.Sleep(int.MaxValue);
    }

    private static void InitLogger()
    {
        LoggingConfiguration config = new LoggingConfiguration();

        // Targets where to log to: File and Console
        FileTarget logfile = new FileTarget("logfile") { FileName = "log.txt" };
        ColoredConsoleTarget logconsole = new ColoredConsoleTarget("logconsole")
        {
            Layout = Layout.FromString("${longdate} [${logger}] (${level:uppercase=true}): ${message:withexception=true}")
        };

        EventLogTarget eventlog = new EventLogTarget
        {
            Layout = Layout.FromString("${longdate} [${logger}] (${level:uppercase=true}): ${message:withexception=true}")
        };


        // Rules for mapping loggers to targets            
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);
        config.AddRule(LogLevel.Warn, LogLevel.Fatal, eventlog);

        // Apply config           
        LogManager.Configuration = config;

        Logger? logger = LogManager.GetLogger(typeof(FleckLog).FullName);
        FleckLog.LogAction = (level, message, ex) =>
        {
            switch (level)
            {
                case Fleck.LogLevel.Debug:
                    logger.Debug(message, ex);
                    break;
                case Fleck.LogLevel.Info:
                    logger.Info(message, ex);
                    break;
                case Fleck.LogLevel.Warn:
                    logger.Warn(message, ex);
                    break;
                case Fleck.LogLevel.Error:
                    logger.Error(message, ex);
                    break;
            }
        };
    }
}

public class EventLogTarget : TargetWithLayout
{
    public static event EventHandler<string> LogEvent;

    protected override void Write(LogEventInfo logEvent)
    {
        string? message = Layout.Render(logEvent);
        LogEvent?.Invoke(this, message);
    }
}