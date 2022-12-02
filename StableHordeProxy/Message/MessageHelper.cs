namespace StableHordeProxy.Message;

public class MessageHelper
{
    public static Message CreateDebugMessage(string message)
    {
        Message msg = new Message("debug", new[] { message });
        return msg;
    }

    public static Message CreateImageMessage(string id, string imageUrl)
    {
        Message msg = new MessageBuilder("image")
            .AddArgument(id)
            .AddArgument(imageUrl)
            .Build();
        return msg;
    }
}