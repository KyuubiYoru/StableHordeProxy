using Fleck;
using NLog;
using StableHordeProxy.Api;
using StableHordeProxy.Api.Model;
using StableHordeProxy.Message;

namespace StableHordeProxy;

public class Server
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Commands _commands;
    private readonly Config _config;

    private readonly HashSet<IWebSocketConnection> _debugClients = new();

    private readonly HttpServer _httpServer;
    private readonly HashSet<IWebSocketConnection> _modelClients = new();
    private readonly ModelHelper _modelHelper;

    private readonly HashSet<Job> _waitingJobs = new();
    private readonly WsServer _wsServer;

    public Server()
    {
        _config = new Config();
        _commands = new Commands();
        RegisterCommands();
        _httpServer = new HttpServer(_config);
        _wsServer = new WsServer(_config, _commands);
        RequestHelper = new RequestHelper(_config);
        _modelHelper = new ModelHelper(this, _config);
        _modelHelper.OnModelUpdate += ModelHelperOnOnModelUpdate;
        _modelHelper.OnModelRemove += ModelHelperOnOnModelRemove;


        EventLogTarget.LogEvent += LogEvent;
    }

    public RequestHelper RequestHelper { get; }

    private void LogEvent(object sender, string message)
    {
        //Send log to all clients who requested debug logs
        foreach (IWebSocketConnection client in _debugClients)
        {
            try
            {
                if (!client.IsAvailable) continue;

                client.Send(new Message.Message("debug", message).Serialize());
            }
            catch (Exception e)
            {
                Log.Error($"{e.Message} {e.StackTrace}");
            }
        }
    }


    public void Start()
    {
        _httpServer.Start();
        _wsServer.Start();

        //Start job runner
        Task.Run(() => RunJobs());
    }

    private async Task RunJobs()
    {
        while (true)
        {
            Job[] jobs = _waitingJobs.ToArray();
            Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async job =>
            {
                if (job == null) return;

                if (job.Status == JobStatus.Running)
                {
                    try
                    {
                        lock (this) _waitingJobs.Remove(job);

                        await job.Run();

                        if (job.Status == JobStatus.Running)
                        {
                            lock (this)
                            {
                                _waitingJobs.Add(job);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error while running job");
                    }
                }
                else
                {
                    lock (this)
                    {
                        _waitingJobs.Remove(job);
                    }
                }
            });
            await Task.Delay(2000);
        }
    }

    private void RegisterCommands()
    {
        _commands.RegisterCommand("startJob", StartJobCommand);
        _commands.RegisterCommand("debug", DebugCommand);
        _commands.RegisterCommand("model", ModelCommand);
    }


    public void StartJobCommand(IWebSocketConnection client, string[] args)
    {
        Job job = new Job(this, client, _config, args);
        _waitingJobs.Add(job);

        Log.Info("Job started with prompt: " + job.GenerationData.ApiData["prompt"]);
    }

    public void DebugCommand(IWebSocketConnection client)
    {
        if (_debugClients.Contains(client))
        {
            _debugClients.Remove(client);
        }
        else
        {
            _debugClients.Add(client);
        }
    }

    public void ModelCommand(IWebSocketConnection client)
    {
        if (!_modelClients.Contains(client))
        {
            _modelClients.Add(client);
        }

        
        //Send all available models to client client.Send(MessageUtils.CreateModelMessage(model.Value).Serialize()) in an async task with an delay of 100ms between each message
        Task.Run(async () =>
        {
            foreach (Model model in _modelHelper.AvailableModels.Values)
            {
                if (!client.IsAvailable) return;
                
                string message = MessageUtils.CreateModelMessage(model).Serialize();
                //Log.Info(message);
                await client.Send(message);
                await Task.Delay(10);
            }
        });

    }

    private void ModelHelperOnOnModelUpdate(object sender, Model model)
    {
        foreach (IWebSocketConnection client in _modelClients)
        {
            try
            {
                if (!client.IsAvailable) continue;

                client.Send(MessageUtils.CreateModelMessage(model).Serialize());
            }
            catch (Exception exception)
            {
                Log.Error($"{exception.Message} {exception.StackTrace}");
            }
        }
    }

    private void ModelHelperOnOnModelRemove(object sender, Model model)
    {
        foreach (IWebSocketConnection client in _modelClients)
        {
            try
            {
                if (!client.IsAvailable) continue;

                client.Send(MessageUtils.CreateModelRemoveMessage(model).Serialize());
            }
            catch (Exception exception)
            {
                Log.Error($"{exception.Message} {exception.StackTrace}");
            }
        }
    }
}