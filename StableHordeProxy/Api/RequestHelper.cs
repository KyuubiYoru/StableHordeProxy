using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NLog;
using RestSharp;

namespace StableHordeProxy.Api;

public class RequestHelper
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly RestClient _client;

    private readonly Config _config;


    public RequestHelper(Config config)
    {
        _config = config;
        _client = new RestClient("https://stablehorde.net/");
    }

    public event EventHandler<(Job, string)> OnImageFinished;


    public async Task<string?> StartImageJobAsync(Job job)
    {
        RestRequest request = new RestRequest("/api/v2/generate/async", Method.Post);
        request.AddHeader("Content-Type", "application/json");

        string? apikey = job.GenerationData.ApiData["apikey"] as string;
        request.AddHeader("apikey", apikey);

        string dataString = job.GenerationData.ToJson().ToString();
        request.AddBody(dataString, "application/json");

        return await _client.ExecuteAsync(request).ContinueWith(t =>
        {
            RestResponse response = t.Result;
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                if (response.Content != null)
                {
                    JsonNode? json = JsonNode.Parse(response.Content);
                    string id = json["id"].ToString();

                    return id;
                }

                Log.Error("No content in response");
                return null;
            }

            Log.Error($"Image request failed with status code {response.StatusCode}");
            return null;
        });
    }


    private static string CalculateHash(byte[] inputBytes)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(inputBytes);
        StringBuilder sb = new StringBuilder();
        foreach (byte b in hashBytes) sb.Append(b.ToString("X2"));

        return sb.ToString();
    }


    public async Task<JobStatus> GetImageStatusAsync(string id)
    {
        RestRequest checkRequest = new RestRequest($"/api/v2/generate/check/{id}");
        checkRequest.AddHeader("Content-Type", "application/json");

        RestResponse checkResponse = await _client.ExecuteAsync(checkRequest);
        if (checkResponse.StatusCode != HttpStatusCode.OK)
        {
            if (checkResponse.StatusCode == HttpStatusCode.NotFound) return JobStatus.Error;

            Log.Error($"Check request failed with status code {checkResponse.StatusCode}");
            return JobStatus.Running;
        }

        try
        {
            bool done = checkResponse.Content != null && JsonNode.Parse(checkResponse.Content)["done"].GetValue<bool>();
            Log.Info(checkResponse.Content);
            if (done) return JobStatus.Finished;
        }
        catch (Exception e)
        {
            Log.Error($"{e.Message} | {e.StackTrace}");
            return JobStatus.Running;
        }

        return JobStatus.Running;
    }

    public async Task<(List<string>?, JobStatus)> GetImagesAsync(string id)
    {
        RestRequest statusRequest = new RestRequest($"/api/v2/generate/status/{id}");
        statusRequest.AddHeader("Content-Type", "application/json");
        RestResponse statusResponse = await _client.ExecuteAsync(statusRequest);
        if (statusResponse.StatusCode != HttpStatusCode.OK)
        {
            Log.Error($"Status request failed with status code {statusResponse.StatusCode}");
            if (statusResponse.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Info($"Image {id} not found");
                return (null, JobStatus.Error);
            }
        }

        if (statusResponse.StatusCode == HttpStatusCode.OK)
        {
            JsonNode? status = JsonNode.Parse(statusResponse.Content);
            JsonArray generations = status["generations"].AsArray();

            List<string> images = new List<string>();

            foreach (JsonNode? generation in generations)
            {
                try
                {
                    string base64Image = generation["img"].ToString();
                    byte[] imageBytes = Convert.FromBase64String(base64Image);

                    string hash = CalculateHash(imageBytes);
                    string filename = $"{hash}.webp";
                    await File.WriteAllBytesAsync(_config.HttpConfig.DataPath + "\\" + filename, imageBytes);
                    Log.Debug($"Saved image {filename}");

                    string imageUrl = _config.HttpConfig.Url + filename;
                    images.Add(imageUrl);
                    return (images, JobStatus.Finished);
                }
                catch (Exception e)
                {
                    Log.Error($"{e.Message} | {e.StackTrace}");
                }
            }
        }

        return (null, JobStatus.Running);
    }

    public async Task<List<(string, int)>?> GetAvailableModels()
    {
        RestRequest request = new RestRequest("/api/v2/status/models");

        RestResponse response = await _client.ExecuteAsync(request);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            Log.Error($"Status request failed with status code {response.StatusCode}");
            return null;
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var models = JsonNode.Parse(response.Content);
            List<(string, int)>? modelList = new List<(string, int)>();

            foreach (var model in models.AsArray())
            {
                var modelName = model["name"].ToString();
                var modelCount = model["count"].GetValue<int>();
                modelList.Add((modelName, modelCount));
            }

            return modelList;
        }

        return null;
    }
}