using System.Net.Http.Json;
using System.Text.Json;
using Flurl;

namespace AITaggerSDK;

public static class APICaller
{
    public static async Task<MultiFileResponse> RequestMultiFileDescription(string endpointUrl, params FileStream[] files)
        => await RequestMultiFileDescription(endpointUrl, files.Select(x => new FakeFileContainer {File = x, Filename = x.Name}).ToArray());
    public static async Task<MultiFileResponse> RequestMultiFileDescription(string endpointUrl, params FakeFileContainer[] files)
    {
        HttpClient client = new HttpClient();
        Dictionary<string, string> fileMap = new();
        foreach (var file in files)
        {
            fileMap.Add(await (await SendSingleFileRequest(Url.Combine(endpointUrl, "desc", "bulk", "upload"), client,
                file.File, file.Filename)).Content.ReadAsStringAsync(),file.Filename);
        }
        
        var response = await client.SendAsync (new HttpRequestMessage(HttpMethod.Get, Url.Combine(endpointUrl, "desc", "bulk", "fetch"))
        {
            Content = new StringContent(JsonSerializer.Serialize(fileMap.Values))
        });
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }

        var multiFileResponse = (await response.Content.ReadFromJsonAsync<MultiFileResponse>())!;
        foreach (var file in multiFileResponse.Files)
        {
            file.Filename = fileMap[file.Filename];
        }
        return multiFileResponse;
    }

    public static async Task<SingleFileResponse> RequestSingleFileDescription(string endpointUrl, FileStream file) =>
        await RequestSingleFileDescription(endpointUrl, new FakeFileContainer()
        {
            File = file,
            Filename = file.Name
        });
    
    public static async Task<SingleFileResponse> RequestSingleFileDescription(string endpointUrl, FakeFileContainer file)
    {
        HttpClient client = new HttpClient();
        var response = await SendSingleFileRequest(Url.Combine(endpointUrl, "desc"), client, file.File, file.Filename);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        return new SingleFileResponse(file.Filename, await response.Content.ReadAsStringAsync(),response.Headers.GetValues("Endpoint-ID").First());
    }
    
    private static async Task<HttpResponseMessage> SendSingleFileRequest(string endpoint, HttpClient client, Stream file, string filename)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StreamContent(file), Path.GetFileNameWithoutExtension(filename), Path.GetFileName(filename));
        HttpResponseMessage response =
            await client.PostAsync(endpoint, form);
        return response;
    }

    /// <summary>
    /// Generates description for single file by its name
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    [Obsolete("Use RequestSingleFileDescription(string, FileStream) or RequestSingleFileDescription(string, FakeFileContainer) instead")]
    public static async Task<SingleFileResponse> GenerateDescription(string filename, string endpoint)
    {
        return await RequestSingleFileDescription(endpoint, File.OpenRead(filename));
    }

    public class SingleFileResponse
    {
        public string Filename;
        public string Data;
        public string EndpointId;

        public SingleFileResponse(string filename, string data, string endpointId)
        {
            Filename = filename;
            Data = data;
            EndpointId = endpointId;
        }
    }

    public class MultiFileResponse
    {
        public string EndpointId;
        public SingleFileResponse[] Files;
    }
}