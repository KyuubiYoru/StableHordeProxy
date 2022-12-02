namespace StableHordeProxy.Message;

public class MessageBuilder
{
    private readonly List<string> _arguments;
    private readonly string _command;

    public MessageBuilder(string command)
    {
        _command = command;
        _arguments = new List<string>();
    }

    public MessageBuilder AddArgument(string argument)
    {
        _arguments.Add(argument);
        return this;
    }

    public Message Build()
    {
        return new Message(_command, _arguments.ToArray());
    }
}