namespace AITaggerSDK.API.Responses;

public class MultiFileResponse
{
    public MultiFileResponse(string endpointId)
    {
        EndpointId = endpointId;
    }

    public string EndpointId { get; set; }
    public SingleFile[] Files { get; set; } = [];
}