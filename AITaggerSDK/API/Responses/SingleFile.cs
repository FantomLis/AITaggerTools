using System.Text.Json.Serialization;

namespace AITaggerSDK.API.Responses;

public class SingleFile
{
    public string Filename{ get; set; }
    /// <remarks>
    /// Null when <see cref="IsError"/> is true
    /// </remarks>
    public string? TagsInfo{ get; set; }
    public string? Error { get; set; }
    [JsonIgnore]
    public bool IsError => !string.IsNullOrEmpty(Error);

    public SingleFile(string filename, string? tagsInfo, string? error = null)
    {
        Filename = filename;
        TagsInfo = tagsInfo;
        Error = error;
    }

    public static SingleFile CreateErrorResponse(string filename, string error) => new SingleFile(filename, null, error);
}