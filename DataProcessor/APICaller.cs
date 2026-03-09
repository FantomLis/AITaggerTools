using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Flurl;

namespace DataProcessor;

public static class APICaller
{
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