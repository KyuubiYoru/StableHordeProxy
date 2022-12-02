using Fleck;
using NLog;
using StableHordeProxy.Api;
using StableHordeProxy.Message;

namespace StableHordeProxy;

public class Job
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private Server _server;
    private Config _config;

    public Job(Server server, IWebSocketConnection client, Config config, string[] args)
    {
        _config = config;
        _server = server;
        Client = client;
        GenerationData = new GenerationData(args);
        GenerationData.ApiData["apikey"] = _config.StableHordeConfig.ApiKey;
        NumberOfImages = GenerationData.ImageParams["n"] as int? ?? 1;
        GenerationData.ImageParams["n"] = 1;
        Status = JobStatus.Running;
    }

    public List<string> RunningIds { get; set; } = new List<string>();
    public List<string> ReadyIds { get; set; } = new List<string>();
    public List<string> FinishedIds { get; set; } = new List<string>();


    public GenerationData GenerationData { get; set; }

    public IWebSocketConnection Client { get; set; }
    public bool Done { get; set; }
    public HashSet<(string, string)> ImagesToSend { get; set; } = new();

    public int NumberOfImages { get; set; }
    public int RequestedImages { get; set; }

    public int FinishedImages { get; set; }
    public JobStatus Status { get; set; }


    private int _requestRetryCount = 0;

    public async Task Run()
    {
        await GetImages();
        await SendImages();
        await RequestImages();
        await CheckStatus();
    }

    private async Task SendImages()
    {
        foreach ((string id, string url) image in ImagesToSend.ToArray())
        {
            Client.Send(MessageHelper.CreateImageMessage(image.id, image.url).Serialize());
            ImagesToSend.Remove(image);
        }
    }

    private async Task RequestImages()
    {
        const int limit = 4;
        int neededImages = NumberOfImages - RequestedImages;
        if (neededImages == 0) return;

        neededImages = neededImages > limit ? limit : neededImages;

        for (int i = 0; i < neededImages; i++)
        {
            try
            {
                string? id = await _server.RequestManager.StartImageJobAsync(this).WaitAsync(CancellationToken.None);
                if (id != null)
                {
                    RunningIds.Add(id);
                    RequestedImages++;
                    Log.Info($"Requested image {id}");
                }
                else
                {
                    _requestRetryCount++;
                    if (_requestRetryCount > 20)
                    {
                        Status = JobStatus.Error;
                        Done = true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"{e.Message} | {e.StackTrace}");
            }
        }
    }

    public async Task CheckStatus()
    {
        foreach (string id in RunningIds.ToArray())
        {
            try
            {
                var status = await _server.RequestManager.GetImageStatusAsync(id);
                if (status == JobStatus.Finished)
                {
                    FinishedIds.Add(id);
                    RunningIds.Remove(id);
                    FinishedImages++;
                }
            }
            catch (Exception e)
            {
                Log.Error($"{e.Message} | {e.StackTrace}");
            }
        }
    }

    public async Task GetImages()
    {
        try
        {
            foreach (string id in FinishedIds.ToArray())
            {
                (List<string>?, JobStatus) images = await _server.RequestManager.GetImagesAsync(id);
                if (images.Item2 == JobStatus.Finished)
                {
                    foreach (var url in images.Item1)
                    {
                        ImagesToSend.Add((id, url));
                    }

                    FinishedIds.Remove(id);
                }
                else if (images.Item2 == JobStatus.Error)
                {
                    FinishedIds.Remove(id);
                    Log.Error($"Error while getting images for {id}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"{e.Message} | {e.StackTrace}");
        }
    }
}