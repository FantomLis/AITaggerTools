namespace AITaggerSDK.API.Responses;

public record EndpointInfo(string EndpointId, string[]? SupportedFiletypes = null)
{
    public string EndpointId { get; } = EndpointId;
    public string[] SupportedFiletypes { get; } = SupportedFiletypes ?? ["png", "jpg", "jpeg", "webp", "gif", "avi", "mp4", "mkv", "webm"];
}