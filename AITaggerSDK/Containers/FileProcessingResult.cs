namespace AITaggerSDK.Containers;

public class FileProcessingResult
{
    public FileProcessingResult(string filename, TaggerFileStatus status)
    {
        Filename = filename;
        ProcessingStatus = status;
    }

    public string Filename { get; set; }
    public TaggerFileStatus ProcessingStatus { get; set; }
    public string? Error { get; set; }
    public bool IsError => !string.IsNullOrEmpty(Error) && !ProcessingStatus.IsFine();
    
    public static FileProcessingResult CreateErrorResult
        (string filename, TaggerFileStatus status, string error) =>
        new FileProcessingResult(filename, status)
        {
            Error = error
        };

    public static FileProcessingResult CreateSkipped(string filename) =>
        new FileProcessingResult(filename, TaggerFileStatus.SKIPPED);
    public static FileProcessingResult CreateIgnored(string filename) =>
        new FileProcessingResult(filename, TaggerFileStatus.IGNORE);
    public static FileProcessingResult CreateOk(string filename) =>
        new FileProcessingResult(filename, TaggerFileStatus.OK);
}