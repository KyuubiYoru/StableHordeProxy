using Newtonsoft.Json.Linq;

namespace StableHordeProxy.Api;

[Serializable]
public class Parameter
{
    public Parameter()
    {
        InitDefault();
    }

    public Dictionary<string, JValue> Data { get; } = new();

    public JObject ToJson()
    {
        JObject requestJson = new JObject();
        foreach ((string key, JValue value) in Data) requestJson.Add(key, value);

        return requestJson;
    }

    private void InitDefault()
    {
        Data.Add("n", new JValue(1));
        Data.Add("width", new JValue(512));
        Data.Add("height", new JValue(512));
        Data.Add("sampler", new JValue("k_euler"));
        Data.Add("cfg_scale", new JValue(7));
        Data.Add("denoising_strength", new JValue(0.6f));
        Data.Add("steps", new JValue(20));
        Data.Add("karras", new JValue(true));
        Data.Add("seed", new JValue((string?)null));
    }
}