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
        if (GenerationData.ApiData["models"] == "stable_diffusion_2.0")
        {
            GenerationData.ImageParams["sampler"] = "dpmsolver";
        }
        
        //Input Validation for the API
        
        //limit the NumberOfImages between 1 and 100
        if (NumberOfImages < 1)
        {
            NumberOfImages = 1;
        }
        else if (NumberOfImages > 100)
        {
            NumberOfImages = 100;
        }
        
        //limit GenerationData.ImageParams["width"] and  GenerationData.ImageParams["height"] between 64 and 1024
        if (GenerationData.ImageParams["width"] is int width)
        {
            if (width < 64) GenerationData.ImageParams["width"] = 64;
            if (width > 1024) GenerationData.ImageParams["width"] = 1024;
        }
        if (GenerationData.ImageParams["height"] is int height)
        {
            if (height < 64) GenerationData.ImageParams["height"] = 64;
            if (height > 1024) GenerationData.ImageParams["height"] = 1024;
        }
        
        //limit steps between 1 and 100
        if (GenerationData.ImageParams["steps"] is int steps)
        {
            if (steps < 1) GenerationData.ImageParams["steps"] = 1;
            if (steps > 100) GenerationData.ImageParams["steps"] = 100;
        }
        
        //limit cfg_scale between -40 and 30 with a step of 0.5
        if (GenerationData.ImageParams["cfg_scale"] is double cfg_scale)
        {
            if (cfg_scale < -40) GenerationData.ImageParams["cfg_scale"] = -40;
            if (cfg_scale > 30) GenerationData.ImageParams["cfg_scale"] = 30;
            GenerationData.ImageParams["cfg_scale"] = Math.Round(cfg_scale * 2) / 2;
        }
        
        //seed is a string and needs to be between 0 and 4294967295 and set it to "" if it is not an int

        if (!UInt32.TryParse(GenerationData.ApiData["seed"], out uint _))
        {
            GenerationData.ApiData["seed"] = "";
        }

        SendJobProgress();
        
        client.OnClose += OnClose;
    }

    private void OnClose()
    {
        if (!_done)
        {
            //Log.Info("Job cancelled");
            Status = JobStatus.Cancelled;
            _done = true;
        }
        foreach (string id in RunningIds)
        {
            Log.Info("Cancelling image {id}", id);
            _server.RequestHelper.CancelJobAsync(id);
        }
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
            if (_done)
            {
                Log.Info("Job finished");
            }
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
        if (Status == JobStatus.Cancelled) return;
        await GetImages();
        if (Status == JobStatus.Cancelled) return;
        await SendImages();
        if (Status == JobStatus.Cancelled) return;
        await RequestImages();
        if (Status == JobStatus.Cancelled) return;
        await CheckStatus();
        
        //Check if Job.Status is Finished and ReadyIds.Count == 0
        if (Status == JobStatus.Finished && ImagesToSend.Count == 0)
        {
            Done = true;
        }
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
        if (RunningIds.Count >= limit * 2)
        {
            //Log.Info("Reached maximum number of running images");
            return;
        }

        for (int i = 0; i < neededImages; i++)
        {
            try
            {
                string? id = await _server.RequestHelper.StartImageJobAsync(this).WaitAsync(CancellationToken.None);
                if (id != null)
                {
                    RunningIds.Add(id);
                    RequestedImages++;
                    //Log.Info($"Requested image {id}");
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