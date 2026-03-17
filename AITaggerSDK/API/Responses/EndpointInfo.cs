namespace AITaggerSDK.API.Responses;

public record EndpointInfo(string EndpointId)
{
    public string EndpointId { get; } = EndpointId;
}