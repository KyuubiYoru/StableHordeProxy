using Newtonsoft.Json.Linq;
using NLog;

namespace StableHordeProxy.Api;

public class GenerationData
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();


    public GenerationData(string[] args, Config? config = null) : this()
    {
        ApiData.Add("apikey", config?.StableHordeConfig.ApiKey ?? "0000000000");

        for (int i = 0; i < args.Length; i += 2)
        {
            if (args.Length > i + 1)
            {
                if (ApiData.ContainsKey(args[i]))
                    //Convert to type of value in dictionary
                {
                    try
                    {
                        ApiData[args[i]] = Convert.ChangeType(args[i + 1], (Type)ApiData[args[i]].GetType());
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Error tried to convert {args[i]}:{args[i+1]} to {ApiData[args[i]].GetType()}");
                    }
                }
                else if (ImageParams.ContainsKey(args[i]))
                    //Convert to type of value in dictionary
                {
                    try
                    {
                        ImageParams[args[i]] = Convert.ChangeType(args[i + 1], (Type)ImageParams[args[i]].GetType());
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Error tried to convert {args[i]}:{args[i+1]} to {ImageParams[args[i]].GetType()}");
                    }
                    
                }
                else
                {
                    Log.Warn($"Unknown parameter: {args[i]}");
                }
            }
        }
    }

    public GenerationData()
    {
        Init();
    }

    public Dictionary<string, dynamic> ApiData { get; } = new();
    public Dictionary<string, dynamic> ImageParams { get; } = new();


    public JObject ToJson()
    {
        JObject requestJson = new JObject();
        foreach (KeyValuePair<string, dynamic> data in ApiData)
        {
            if (data.Key == "models")
            {
                requestJson.Add(data.Key, new JArray(data.Value));
            }
            else
            {
                requestJson.Add(data.Key, data.Value);
            }
        }

        JObject parameters = new JObject();
        foreach (KeyValuePair<string, dynamic> parameter in ImageParams)
        {
            if (parameter.Key == "models")
            {
                parameters.Add(parameter.Key, new JArray(parameter.Value));
            }
            else if (parameter.Key == "seed")
            {
                if (!string.IsNullOrEmpty(parameter.Value))
                {
                    parameters.Add(parameter.Key, parameter.Value);
                }
            }else
            {
                parameters.Add(parameter.Key, parameter.Value);
            }
        }

        requestJson.Add("params", parameters);

        return requestJson;
    }

    private void Init()
    {
        ApiData.Add("prompt", "A cute fox nousr robot");
        ApiData.Add("nsfw", true);
        ApiData.Add("r2", false);
        ApiData.Add("shared", true);
        ApiData.Add("censor_nsfw", false);
        ApiData.Add("trusted_workers", false);
        ApiData.Add("models", "Midjourney Diffusion");

        ImageParams.Add("n", 1);
        ImageParams.Add("width", 512);
        ImageParams.Add("height", 512);
        ImageParams.Add("sampler", "k_dpm_adaptive");
        ImageParams.Add("cfg_scale", 7d);
        //Parameter.Add("denoising_strength", new JValue(0.6f));
        ImageParams.Add("steps", 20);
        ImageParams.Add("karras", false);
        ImageParams.Add("seed_variation", 1);
        ImageParams.Add("seed", "");
    }
}