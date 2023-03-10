using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StableHordeProxy;

[Serializable]
public class Config
{
    private const string FilePath = "config.json";

    public Config()
    {
        if (!File.Exists(FilePath))
        {
            HttpConfig = new HttpConfig();
            WebSocketConfig = new WebSocketConfig();
            StableHordeConfig = new StableHordeConfig();
        }
        else
        {
            string json = File.ReadAllText(FilePath);
            JObject jObject = JObject.Parse(json);
            HttpConfig = jObject["HttpConfig"].ToObject<HttpConfig>();
            WebSocketConfig = jObject["WebSocketConfig"].ToObject<WebSocketConfig>();
            StableHordeConfig = jObject["StableHordeConfig"].ToObject<StableHordeConfig>();

            if (string.IsNullOrWhiteSpace(StableHordeConfig.ApiKey)) StableHordeConfig.ApiKey = "0000000000";
        }

        Save();
    }

    public HttpConfig HttpConfig { get; }
    public WebSocketConfig WebSocketConfig { get; }

    public StableHordeConfig StableHordeConfig { get; }

    public void Save()
    {
        string json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }
}

[Serializable]
public class HttpConfig
{
    public string Address { get; set; } = "+";
    public int Port { get; set; } = 8282;
    public string Url { get; set; } = "http://localhost/"; //The URL where the images are hosted from, change this to your domain name or IP address
    public string DataPath { get; set; } = "data/";
}

[Serializable]
public class WebSocketConfig
{
    public string Address { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8181;
}

[Serializable]
public class StableHordeConfig
{
    public string ApiAddress { get; set; } = "https://stablehorde.net/";
    public string ApiKey { get; set; } = "0000000000";

    public string ModelDbUrl { get; set; } = "https://raw.githubusercontent.com/Sygil-Dev/nataili-model-reference/main/db.json";
}