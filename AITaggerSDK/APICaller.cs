using Flurl;

namespace AITaggerSDK;

public static class APICaller
{
    public static async Task<APIResponse> GenerateDescription(string endpoint, FileStream file) =>
        await GenerateDescription(endpoint, new FakeFileContainer()
        {
            File = file,
            Filename = file.Name
        });
    
    public static async Task<APIResponse> GenerateDescription(string endpointUrl, FakeFileContainer file)
    {
        HttpClient client = new HttpClient();
        var response = await SendRequest(Url.Combine(endpointUrl, "desc"), file.File, file.Filename, client);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        return new APIResponse()
        {
            Data = await response.Content.ReadAsStringAsync(),
            EndpointId = response.Headers.GetValues("Endpoint-ID").First()
        };
    }

    private static async Task<HttpResponseMessage> SendRequest(string endpoint, Stream file, string filename, HttpClient client)
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
    [Obsolete("Use GenerateDescription(string, FileStream) or GenerateDescription(string, FakeFileContainer) instead")]
    public static async Task<APIResponse> GenerateDescription(string filename, string endpoint)
    {
        HttpClient client = new HttpClient();
        var form = new MultipartFormDataContent();
        form.Add(new StreamContent(File.OpenRead(filename)), Path.GetFileNameWithoutExtension(filename), Path.GetFileName(filename));
        HttpResponseMessage response =
            await client.PostAsync(Url.Combine(endpoint, "desc"), form);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode}: {response.ReasonPhrase}");
        }
        return new APIResponse()
        {
            Data = await response.Content.ReadAsStringAsync(),
            EndpointId = response.Headers.GetValues("Endpoint-ID").First()
        };
    }

    public class APIResponse
    {
        public string Data;
        public string EndpointId;
    }
}