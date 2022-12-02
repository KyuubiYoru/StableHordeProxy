using Fleck;
using NLog;

namespace StableHordeProxy;

public delegate string CommandHandler(string[] args);

public class Commands
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly Dictionary<string, Delegate> commands;

    public Commands()
    {
        commands = new Dictionary<string, Delegate>();
    }

    //Register a command
    public void RegisterCommand(string command, Delegate action)
    {
        commands.Add(command, action);
    }

    //Execute a command
    public string? TryExecuteCommand(IWebSocketConnection client, string command, string[] args)
    {
        if (!commands.ContainsKey(command))
        {
            Log.Info($"Command does not exist: '{command}");
            return null;
        }

        Delegate callback = commands[command];
        // if (callback.GetType().GetGenericArguments().Length - 1 != args.Length)
        // {
        //     Log.Error($"Not enough arguments for command: got {args.Length}, expected {callback.GetType().GetGenericArguments().Length - 1}");
        //     return null;
        // }

        IEnumerable<(int i, Type type)> enumerator = callback.GetType()
            .GetGenericArguments()
            .SkipLast(0)
            .Select((type, i) => (i, type));

        List<object> invokeArgs = new List<object>();


        foreach ((int i, Type argType) in enumerator)
            // special handling, if argument is string array, pass all raw
            // arguments to it
            if (argType == typeof(string[]))
                invokeArgs.Add(args);
            else if (argType == typeof(string))
                invokeArgs.Add(args[i]);
            else if (argType == typeof(int))
                invokeArgs.Add(int.Parse(args[i]));
            else if (argType == typeof(IWebSocketConnection))
                invokeArgs.Add(client);
            else
                throw new NotSupportedException();

        string? response = null;
        try
        {
            response = (string?)callback.DynamicInvoke(invokeArgs.ToArray()) ?? "null";
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return response;
    }

    public int GetCommandCount()
    {
        return commands.Count;
    }
}