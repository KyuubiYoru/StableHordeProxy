using StableHordeProxy.Api.Model;

namespace StableHordeProxy.Message;

public static class MessageUtils
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
        Message msg = new MessageBuilder("model_update")
            .AddArgument(model.Name)
            .AddArgument(model.Description)
            .AddArgument(model.AvailableWorkers.ToString())
            .AddArgument(model.SortIndex.ToString())
            .AddArgument(model.Nsfw.ToString())
            .AddArgument(model.Style)
            .AddArgument(model.Trigger.ToString() ?? string.Empty)
            .AddArgument(model.Showcases.ToString() ?? string.Empty)
            .Build();
        return msg;
    }

    public static Message CreateModelRemoveMessage(Model model)
    {
        Message msg = new MessageBuilder("model_remove")
            .AddArgument(model.Name)
            .Build();
        return msg;
    }

    public static Message CreateJobProgressMessage(Job job)
    {
        //Calculate progress percentage from job.FinishedImages and job.NumberOfImages
        float progress = job.FinishedImages / (float)job.NumberOfImages * 100;
        Message msg = new MessageBuilder("job_progress")
            .AddArgument(job.NumberOfImages.ToString())
            .AddArgument(job.RequestedImages.ToString())
            .AddArgument(job.FinishedImages.ToString())
            .AddArgument(job.Done.ToString())
            .AddArgument(progress.ToString("0.00"))
            .Build();
        return msg;
    }
}