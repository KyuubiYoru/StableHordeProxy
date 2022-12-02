using Newtonsoft.Json.Linq;
using NLog;

namespace StableHordeProxy.Api.Model;

public class ModelHelper
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly Config _config;

    private readonly Server _server;
    public EventHandler<Model>? OnModelRemove;

    public EventHandler<Model>? OnModelUpdate;

    public ModelHelper(Server server, Config config)
    {
        _config = config;
        _server = server;
        UpdateModels().WaitAsync(CancellationToken.None);
        UpdateAvailableModels().WaitAsync(CancellationToken.None);

        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(15 * 60 * 1000);
                await UpdateModels();
                await UpdateAvailableModels();
            }
        });
    }

    public Dictionary<string, Model> Models { get; set; } = new();
    public Dictionary<string, Model> AvailableModels { get; } = new();

    public async Task UpdateModels()
    {
        JObject jsonObject;
        if (File.Exists("db.json") && File.GetLastWriteTime("db.json").AddHours(1) > DateTime.Now)
            jsonObject = JObject.Parse(File.ReadAllText("db.json"));
        else
        {
            try
            {
                var client = new HttpClient();
                var response = await client.GetAsync(_config.StableHordeConfig.ModelDbUrl);
                var content = await response.Content.ReadAsStringAsync();
                jsonObject = JObject.Parse(content);
                File.WriteAllText("db.json", content);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to download model database");
                if (File.Exists("db.json"))
                {
                    jsonObject = JObject.Parse(File.ReadAllText("db.json"));
                    Log.Warn("Using cached model database");
                }
                else
                {
                    Log.Error("No cached model database found");
                    return;
                }
            }
        }

        Dictionary<string, Model> models = new Dictionary<string, Model>();
        foreach (KeyValuePair<string, JToken?> keyValuePair in jsonObject)
        {
            var model = new Model(keyValuePair.Key, keyValuePair.Value);
            models.Add(keyValuePair.Key, model);
        }

        Models = models;
    }

    private async Task UpdateAvailableModels()
    {
        var availableModelsList = await _server.RequestHelper.GetAvailableModels();


        if (availableModelsList != null)
        {
            var availableModels = new Dictionary<string, Model>();
            foreach ((string name, int availableWorker) model in availableModelsList)
            {
                if (Models.ContainsKey(model.name))
                {
                    Models[model.name].AvailableWorkers = model.availableWorker;
                    availableModels.Add(model.name, Models[model.name]);
                }
                else
                {
                    Log.Warn($"Model {model.name} is available but not in the model database");
                    Model newModel = new Model(model.name, null);
                    newModel.AvailableWorkers = model.availableWorker;
                    availableModels.Add(model.name, newModel);
                }
            }

            foreach (KeyValuePair<string, Model> model in AvailableModels.ToArray())
            {
                if (!availableModels.ContainsKey(model.Key))
                    OnModelRemove?.Invoke(this, model.Value);
            }

            foreach (KeyValuePair<string, Model> model in availableModels)
            {
                if (!AvailableModels.ContainsKey(model.Key))
                {
                    AvailableModels.Add(model.Key, model.Value);
                    OnModelUpdate?.Invoke(this, model.Value);
                }
                else if (AvailableModels[model.Key] != model.Value)
                {
                    AvailableModels[model.Key] = model.Value;
                    OnModelUpdate?.Invoke(this, model.Value);
                }
            }
        }
    }
}