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
    public static async Task<MultiFileResponse> RequestFilesDescription(string endpointUrl, params FileStream[] files)
        => await RequestFilesDescription(endpointUrl, files.Select(x => new FakeFileContainer (x, x.Name)).ToArray());
    public static async Task<MultiFileResponse> RequestFilesDescription(string endpointUrl, params FakeFileContainer[] files)
    {
        HttpClient client = new HttpClient();
        Dictionary<string, string> fileMap = new();
        HttpResponseMessage response;
        foreach (var file in files)
        {
            response = await _SendFile(Url.Combine(endpointUrl, "desc", "upload"), client,
                file.File, Path.GetFileName(file.Filename));
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
            }
            fileMap.Add(await response.Content.ReadAsStringAsync(),Path.GetFileName(file.Filename));
        }
        
        response = await client.SendAsync (new HttpRequestMessage(HttpMethod.Get, Url.Combine(endpointUrl, "desc", "fetch"))
        {
            Content = new StringContent(JsonSerializer.Serialize(fileMap.Keys.ToList()), Encoding.UTF8, "application/json"),
        });
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        var multiFileResponse = (await response.Content.ReadFromJsonAsync<MultiFileResponse>());
        if (multiFileResponse?.Files == null) throw new HttpRequestException("Invalid response.");
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

    public static async Task<EndpointInfo?> RequestEndpointInfo(string endpointUrl)
    {
        HttpClient client = new HttpClient();
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