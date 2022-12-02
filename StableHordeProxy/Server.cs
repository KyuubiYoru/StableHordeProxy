using Fleck;
using NLog;
using StableHordeProxy.Api;

namespace StableHordeProxy;

public class Server
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Commands _commands;
    private readonly Config _config;

    //WeakReference list of all connected clients who requested debug logs
    private readonly List<WeakReference<IWebSocketConnection>> _debugClients = new();
    private readonly HttpServer _httpServer;

    private readonly HashSet<Job> _waitingJobs = new();
    private readonly WsServer _wsServer;

    public Server()
    {
        _config = new Config();
        _commands = new Commands();
        RegisterCommands();
        _httpServer = new HttpServer(_config);
        _wsServer = new WsServer(_config, _commands);
        RequestManager = new RequestManager(_config);


        EventLogTarget.LogEvent += LogEvent;
    }

    public RequestManager RequestManager { get; }

    private void LogEvent(object sender, string e)
    {
        //Send log to all clients who requested debug logs
        foreach (WeakReference<IWebSocketConnection> client in _debugClients)
            if (client.TryGetTarget(out IWebSocketConnection? target))
                try
                {
                    if (!target.IsAvailable) continue;

                    target.Send(new Message.Message("debug", e).Serialize());
                }
                catch (Exception exception)
                {
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
                    try
                    {
                        lock (this)
                        {
                            _waitingJobs.Remove(job);
                        }

                        await job.Run();

                        if (job.Status == JobStatus.Running)
                            lock (this)
                            {
                                _waitingJobs.Add(job);
                            }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error while running job");
                    }
                else
                    lock (this)
                    {
                        _waitingJobs.Remove(job);
                    }
            });

            //Wait 1 second
            await Task.Delay(2000);
        }
    }

    private void RegisterCommands()
    {
        // _commands.RegisterCommand("test", TestCommand);
        _commands.RegisterCommand("startJob", StartJobCommand);
        _commands.RegisterCommand("debug", DebugCommand);
    }


    public void StartJobCommand(IWebSocketConnection client, string[] args)
    {
        Job job = new Job(this, client, _config, args);
        _waitingJobs.Add(job);

        Log.Info("Job started");
    }

    public void DebugCommand(IWebSocketConnection client)
    {
        if (_debugClients.Any(x => x.TryGetTarget(out IWebSocketConnection? target) && target == client))
        {
            _debugClients.RemoveAll(x => x.TryGetTarget(out IWebSocketConnection? target) && target == client);
            //Remaining clients
            Log.Info($"Remaining debug clients: {_debugClients.Count}");
        }
        else
        {
            _debugClients.Add(new WeakReference<IWebSocketConnection>(client));
            Log.Info("Debug command called, added client");
        }
    }
}