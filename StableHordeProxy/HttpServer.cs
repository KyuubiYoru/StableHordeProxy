using System.Net;
using NLog;

namespace StableHordeProxy;

public class HttpServer
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Config _config;

    private readonly HttpListener _server;

    public HttpServer(Config config)
    {
        _config = config;
        _server = new HttpListener();
        _server.Prefixes.Add($"http://{_config.HttpConfig.Address}:{_config.HttpConfig.Port}/");
    }

    public void Start()
    {
        _server.Start();
        _server.BeginGetContext(OnRequest, null);
        Log.Info("HTTP Server started");
    }

    private void OnRequest(IAsyncResult result)
    {
        try
        {
            HttpListenerContext context = _server.EndGetContext(result);
            _server.BeginGetContext(OnRequest, null);
            string? path = context.Request.Url?.AbsolutePath;
            if (path == "/")
            {
                context.Response.Redirect("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
                context.Response.OutputStream.Close();
                return;
            }

            path = _config.HttpConfig.DataPath + path;
            if (File.Exists(path))
            {
                context.Response.StatusCode = 200;
                //context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = new FileInfo(path).Length;
                context.Response.OutputStream.Write(File.ReadAllBytes(path), 0, (int)new FileInfo(path).Length);
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.StatusDescription = "File not found";
            }

            context.Response.OutputStream.Close();
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }
}