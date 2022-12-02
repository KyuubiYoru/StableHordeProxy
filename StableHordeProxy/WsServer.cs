using Fleck;
using NLog;

namespace StableHordeProxy;

public class WsServer
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly HashSet<IWebSocketConnection> _clients = new();
    private readonly Commands _commands;
    private readonly Config _config;

    private readonly WebSocketServer _server;

    //Event that is fired when a client disconnects
    public static event EventHandler<IWebSocketConnection>? ClientDisconnected;


    public WsServer(Config config, Commands commands)
    {
        _config = config;
        _commands = commands;
        _server = new WebSocketServer($"http://{_config.WebSocketConfig.Address}:{_config.WebSocketConfig.Port}");
    }

    public void Start()
    {
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                Log.Info("Connection opened!");
                _clients.Add(socket);
            };
            socket.OnClose = () =>
            {
                Log.Info("Connection closed!");
                _clients.Remove(socket);
                ClientDisconnected?.Invoke(this, socket);
            };
            socket.OnMessage = messageText =>
            {
                var message = Message.Message.Parse(messageText);
                try
                {
                    string? response = _commands.TryExecuteCommand(
                        socket,
                        message.Command, message.Arguments);
                    if (response != null)
                        socket.Send(response);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            };
        });

        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(5000);
                foreach (var client in _clients.ToArray())
                {
                    client.Send("ping");
                }
            }
        });

        Log.Info("WebSocket server started!");
    }

    public void SendMessageToAll(Message.Message message)
    {
        foreach (var client in _clients)
            client.Send(message.Serialize());
    }
}