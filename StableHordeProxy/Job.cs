using Fleck;
using NLog;
using StableHordeProxy.Api;
using StableHordeProxy.Message;

namespace StableHordeProxy;

public class Job
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Config _config;
    private readonly Server _server;
    private bool _done;
    private int _finishedImages;
    private int _requestedImages;


    private int _requestRetryCount;

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

        SendJobProgress();
    }

    public List<string> RunningIds { get; } = new();
    public List<string> ReadyIds { get; } = new();


    public GenerationData GenerationData { get; }
    public IWebSocketConnection Client { get; }

    public bool Done
    {
        get => _done;
        private set
        {
            _done = value;
            SendJobProgress();
        }
    }

    public HashSet<(string, string)> ImagesToSend { get; set; } = new();

    public int NumberOfImages { get; set; }

    public int RequestedImages
    {
        get => _requestedImages;
        private set
        {
            _requestedImages = value;
            SendJobProgress();
        }
    }

    public int FinishedImages
    {
        get => _finishedImages;
        private set
        {
            _finishedImages = value;
            SendJobProgress();
        }
    }

    public JobStatus Status { get; set; }

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
            Client.Send(MessageUtils.CreateImageMessage(image.id, image.url).Serialize());
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
                string? id = await _server.RequestHelper.StartImageJobAsync(this).WaitAsync(CancellationToken.None);
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
                JobStatus status = await _server.RequestHelper.GetImageStatusAsync(id);
                if (status == JobStatus.Finished)
                {
                    ReadyIds.Add(id);
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
            foreach (string id in ReadyIds.ToArray())
            {
                (List<string>?, JobStatus) images = await _server.RequestHelper.GetImagesAsync(id);
                if (images.Item2 == JobStatus.Finished)
                {
                    foreach (string url in images.Item1) ImagesToSend.Add((id, url));

                    ReadyIds.Remove(id);
                }
                else if (images.Item2 == JobStatus.Error)
                {
                    ReadyIds.Remove(id);
                    Log.Error($"Error while getting images for {id}");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"{e.Message} | {e.StackTrace}");
        }
    }

    private void SendJobProgress()
    {
        Client.Send(MessageUtils.CreateJobProgressMessage(this).Serialize());
    }
}