using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public static class CDSAPI
{
    private static string _url = "";
    private static string _key = "";

    /// <summary>
    /// Attempt to find CDS credentials using different methods:
    /// 1. Direct credentials provided through SetCredentials
    /// 2. Environmental variables CDSAPI_URL and CDSAPI_KEY
    /// 3. Credential file in home directory ~/.cdsapirc
    /// </summary>
    public static (string url, string key) GetCredentials()
    {
        string url = "";
        string key = "";

        // Attempt to retrieve from dotfile
        string dotrc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cdsapirc");
        if (File.Exists(dotrc))
        {
            (url, key) = GetCredentialsFromFile(dotrc);
        }

        // Overwrite with env values
        url = Environment.GetEnvironmentVariable("CDSAPI_URL") ?? url;
        key = Environment.GetEnvironmentVariable("CDSAPI_KEY") ?? key;

        // Overwrite with direct values
        url = string.IsNullOrEmpty(_url) ? url : _url;
        key = string.IsNullOrEmpty(_key) ? key : _key;

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        {
            throw new Exception($"\"Missing credentials. Either add the CDSAPI_URL and CDSAPI_KEY env variables or create a .cdsapirc file (default location: '{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}').\"");
        }

        return (url, key);
    }

    /// <summary>
    /// Parse the CDS credentials from a provided file
    /// </summary>
    private static (string url, string key) GetCredentialsFromFile(string file)
    {
        var creds = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(file))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                creds[parts[0].Trim()] = parts[1].Trim();
            }
        }

        if (!creds.ContainsKey("url") || !creds.ContainsKey("key"))
        {
            throw new Exception("""
            The credentials' file must have both a `url` value and a `key` value in the following format:
            url: https://yourendpoint
            key: your-personal-api-token
            """);
        }

        return (creds["url"], creds["key"]);
    }

    /// <summary>
    /// Set credentials directly
    /// </summary>
    public static void SetCredentials(string url, string key)
    {
        _url = url;
        _key = key;
    }

    /// <summary>
    /// Retrieves dataset with given name from the Climate Data Store
    /// with the specified params (Dictionary) and stores it in the
    /// given filename.
    /// </summary>
    public static async Task<Dictionary<string, object>> Retrieve(string name, Dictionary<string, object> parameters, string filename, double waitSeconds = 1.0)
    {
        var (url, key) = GetCredentials();
        using var httpClient = new HttpClient();

        try {

            httpClient.DefaultRequestHeaders.Add("PRIVATE-TOKEN", key);
            
            var requestData = new Dictionary<string, object> { ["inputs"] = parameters };
            var response = await httpClient.PostAsync(
                $"{url}/retrieve/v1/processes/{name}/execute",
                new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json")
            );

            if (!response.IsSuccessStatusCode) {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
                    throw new ArgumentException($"The requested dataset {name} was not found.");
                }
                else if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500) {
                    throw new ArgumentException($"The request is in a bad format: {parameters}");
                }
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
            var endpoint = response?.Headers?.Location?.ToString();

            string? lastStatus = null;
            while (data?["status"].ToString() != "successful") {

                response = await httpClient.GetAsync(endpoint);
                responseContent = await response.Content.ReadAsStringAsync();
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

                var status = Convert.ToString(data?["status"]);

                if (status != lastStatus) lastStatus = status;
                
                if (status == "failed") {
                    throw new Exception($"""
                    Request to dataset {name} failed.
                    Check https://cds.climate.copernicus.eu/requests
                    for more information (after login).
                    """);
                }
                
                if (status != "successful") {
                    waitSeconds = Math.Min(waitSeconds*2, 60*10);
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                }
            }

            response = await httpClient.GetAsync($"{endpoint}/results");
            responseContent = await response.Content.ReadAsStringAsync();
            var body = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);

            var asset = (JsonElement)body["asset"];
            var value = asset.GetProperty("value");
            var downloadUrl = value.GetProperty("href").GetString();

            using var downloadResponse = await httpClient.GetAsync(downloadUrl);
            using var fileStream = File.Create(filename);
            await downloadResponse.Content.CopyToAsync(fileStream);

            return data;
        }
        catch {
            throw;
        }
    }

    /// <summary>
    /// Equivalent to JsonSerializer.Deserialize
    /// </summary>
    public static Dictionary<string, object>? Parse(string jsonString)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
    }
}