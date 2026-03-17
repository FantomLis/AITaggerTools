namespace AITaggerSDK.API.Responses;

public class SingleFile
{
    public string Filename{ get; set; }
        
    public SingleFile() {}
    public string? TagsInfo{ get; set; }

    public SingleFile(string filename, string data)
    {
        Filename = filename;
        Data = data;
    }
}