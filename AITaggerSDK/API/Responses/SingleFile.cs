namespace AITaggerSDK.API.Responses;

public class SingleFile
{
    public string Filename{ get; set; }
    public string Data{ get; set; }
        
    public SingleFile() {}

    public SingleFile(string filename, string data)
    {
        Filename = filename;
        Data = data;
    }
}