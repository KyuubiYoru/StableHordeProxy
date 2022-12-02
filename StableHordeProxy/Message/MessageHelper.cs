using StableHordeProxy.Api.Model;

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

    public static Message CreateModelMessage(Model model)
    {
        Message msg = new MessageBuilder("model")
            .AddArgument(model.Name)
            .AddArgument(model.Description)
            .AddArgument(model.AvailableWorkers.ToString())
            .AddArgument(model.Nsfw.ToString())
            .AddArgument(model.Style)
            .AddArgument(model.Trigger.ToString()??string.Empty)
            .AddArgument(model.Showcases.ToString()??string.Empty)
            .Build();
        return msg;
    }
}