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
        string triggerstring;
        string showcase;
        
        //Use the first string in model.Trigger as the triggerstring
        if (model.Trigger.Length > 0)
        {
            triggerstring = model.Trigger[0];
        }
        else
        {
            triggerstring = "";
        }
        //Use the first string in model.Showcase as the showcase
        if (model.Showcases.Length > 0)
        {
            showcase = model.Showcases[0];
        }
        else
        {
            showcase = "";
        }

        Message msg = new MessageBuilder("model_update")
            .AddArgument(model.Name)
            .AddArgument(model.Description)
            .AddArgument(model.AvailableWorkers.ToString())
            .AddArgument(model.SortIndex.ToString())
            .AddArgument(model.Nsfw.ToString())
            .AddArgument(model.Style)
            .AddArgument(triggerstring)
            .AddArgument(showcase)
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

    public static Message CreateJobProgressMessage(Job job, string message = "")
    {
        //Calculate progress percentage from job.FinishedImages and job.NumberOfImages
        float progress = job.FinishedImages / (float)job.NumberOfImages * 100;
        Message msg = new MessageBuilder("job_progress")
            .AddArgument(job.NumberOfImages.ToString())
            .AddArgument(job.RequestedImages.ToString())
            .AddArgument(job.FinishedImages.ToString())
            .AddArgument(job.Done.ToString())
            .AddArgument(progress.ToString("0.00"))
            .AddArgument(message)
            .Build();
        return msg;
    }
}