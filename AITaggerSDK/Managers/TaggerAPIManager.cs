using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AITaggerSDK.API.Responses;
using AITaggerSDK.Containers;
using Flurl;

namespace AITaggerSDK.Managers;

// ReSharper disable once InconsistentNaming
public static class TaggerAPIManager
{
    private static Lazy<HttpClient?> _HttpClient = new Lazy<HttpClient?>(new HttpClient()
    {
        Timeout = TimeSpan.FromMinutes(30)
    });
    public static HttpClient Default
    {
        get
        {
            if (_HttpClient.Value == null) _HttpClient = new Lazy<HttpClient?>();
            return _HttpClient.Value!;
        }
    }
    public static async Task<MultiFileResponse> RequestFilesDescription(HttpClient client, string endpointUrl, params FileStream[] files)
        => await RequestFilesDescription(client, endpointUrl, files.Select(x => new FakeFileContainer (x, x.Name)).ToArray());
    public static async Task<MultiFileResponse> RequestFilesDescription(HttpClient client, string endpointUrl, params FakeFileContainer[] files)
    {
        Dictionary<string, string> fileMap = new();
        foreach (var file in files)
        {
            fileMap.Add(await UploadFile(client, endpointUrl, file),Path.GetFileName(file.Filename));
        }
        
        var multiFileResponse = await FetchDescription(client, endpointUrl, fileMap.Keys);
        try
        {
            foreach (var file in multiFileResponse.Files)
            {
                file.Filename = fileMap[file.Filename];
            }
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Invalid response: {ex.Message}");
        }
        return multiFileResponse;
    }

    public static async Task<MultiFileResponse?> FetchDescription(HttpClient client, string endpointUrl, ICollection<string> endpointFileNames)
    {
        var response = await client.SendAsync (new HttpRequestMessage(HttpMethod.Get, Url.Combine(endpointUrl, "desc", "fetch"))
        {
            Content = new StringContent(JsonSerializer.Serialize(endpointFileNames), Encoding.UTF8, "application/json"),
        });
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        var multiFileResponse = (await response.Content.ReadFromJsonAsync<MultiFileResponse>());
        return multiFileResponse;
    }

    public static async Task<string> UploadFile(HttpClient client, string endpointUrl,
        FileStream file) => await UploadFile(client, endpointUrl, new FakeFileContainer(file, file.Name));
    public static async Task<string> UploadFile(HttpClient client, string endpointUrl,
        FakeFileContainer file)
    {
        Dictionary<string, string> fileMap = new();
        HttpResponseMessage response;
        response = await _SendFile(Url.Combine(endpointUrl, "desc", "upload"), client,
            file.File, Path.GetFileName(file.Filename));
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<EndpointInfo?> RequestEndpointInfo(HttpClient client, string endpointUrl)
    {
        var response = await client.SendAsync (new HttpRequestMessage(HttpMethod.Get, Url.Combine(endpointUrl, "info")));
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        return await response.Content.ReadFromJsonAsync<EndpointInfo>();
    }

    private static async Task<HttpResponseMessage> _SendFile(string endpoint, HttpClient client,
        Stream file, string filename)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StreamContent(file), Path.GetFileNameWithoutExtension(filename), Path.GetFileName(filename));
        HttpResponseMessage response =
            await client.PostAsync(endpoint, form);
        return response;
    }
    
}