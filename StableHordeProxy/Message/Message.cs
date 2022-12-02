namespace StableHordeProxy.Message;

public class Message
{
    public Message(string command, string[] arguments)
    {
        Command = command;
        Arguments = arguments;
    }

    public Message(string command, string argument)
    {
        Command = command;
        Arguments = new[] { argument };
    }

    public string Command { get; }
    public string[] Arguments { get; }

    /// <summary>
    ///     Parse a message string in the format "command,arg0,arg1,...,argN" into
    ///     it's individual components. The command and arguments are URL encoded.
    /// </summary>
    /// <param name="message">The message string to parse</param>
    /// <returns>The command and arguments</returns>
    public static Message Parse(string message)
    {
        var split = message.Trim()
            .Split(',')
            .Select(s => Uri.UnescapeDataString(s))
            .ToArray();

        string command = Uri.UnescapeDataString(split[0]);
        string[] args = split.Skip(1).ToArray();
        return new Message(command, args);
    }

    public string Serialize()
    {
        var elems = Enumerable.Repeat(Command, 1).Concat(Arguments);
        var elemsEncoded = elems.Select(s => Uri.EscapeDataString(s));
        return string.Join(",", elemsEncoded);
    }
}