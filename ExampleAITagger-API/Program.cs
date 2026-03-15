using System.Text.Json;
using Emgu.CV;
using Emgu.CV.CvEnum;

internal class Program
{
    // Always change API ID when creating new TaggerAPI
    /// <summary>
    /// Sets API ID for TaggerAPI
    /// </summary>
    const string ApiId = "ExampleAITagger-API";
    /// <summary>
    /// Sets maximum form files size for request.
    /// </summary>
    public static int MaxFileSizeInMb = 1024;

    /// <summary>
    /// Sets maximum time before uploaded file will be removed.
    /// </summary>
    public static int MaxFileStoreTimeInMin = 60 * 4;

    private static Dictionary<string, DateTime> FileRemovingStruct = new ();
    
    private static string _TempFolder => Path.Combine(Directory.GetCurrentDirectory(), "temp");
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxFileSizeInMb * 1024 * 1024);

        var app = builder.Build();
        // Clearing temp upload folder on startup
        var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        app.UseHttpsRedirection();
        
        app.MapPost("/desc", Desc)
            .WithName("SingleFileDescription");
        app.MapPost("/desc/bulk/upload", Upload)
            .WithName("BulkFileUpload");
        app.MapGet("/desc/bulk/fetch", Fetch)
            .WithName("BulkFileDescription");
        Task.Run(() =>
        {
            while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
            {
                Thread.Sleep(60 * 1000);
                List<string> remove = new();
                lock (FileRemovingStruct)
                {
                    foreach (var fileRemDate in FileRemovingStruct)
                    {
                        if (fileRemDate.Value < DateTime.Now)
                        {
                            try
                            {
                                File.Delete(fileRemDate.Key);
                                remove.Add(fileRemDate.Key);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to delete {fileRemDate.Key}: {ex.Message}");
                            }
                        }
                    }

                    remove.ForEach(x => FileRemovingStruct.Remove(x));
                }
            }
        });
        app.Run();
    }
    
    /// <summary>
    /// /desc/bulk/upload path for TaggerAPI bulk file upload. After uploading, files will be stored on server for 4 hours. Returns 
    /// </summary>
    /// <param name="r"></param>
    private static async Task Upload(HttpContext r)
    {
        IFormFile formFile = r.Request.Form.Files.First();
        
        var filePath = await SaveFileToDrive(formFile);
        
        await r.Response.WriteAsync(Path.GetFileName(filePath));

        FileRemovingStruct.Add(filePath, DateTime.Now.AddMinutes(MaxFileStoreTimeInMin));
    }
    
    private static async Task Fetch(HttpContext r)
    {
        List<string>? input = (await r.Request.ReadFromJsonAsync<List<string>>())?.ToList();
        if (input == null) return;
        List<string> filepaths = new();
        foreach (var filepath in input)
        {
            if (!File.Exists(Path.Combine(_TempFolder, filepath))) continue;
            // Prepare your files
            filepaths.Add(filepath);
        }

        var output = new List<SingleFileResponse>();
        foreach (var filepath in filepaths)
        {
            var result = "";
            try
            {
                // ... connect to AI model and get results
                result = RunModel(filepath);
                
                // ... parse results and put into results variable
                result = ParseResults(result);
                output.Add(new SingleFileResponse(Path.GetFileName(filepath), result, ApiId));
            
                // Deleting temp file
                File.Delete(filepath);
            } // when failed, skip file
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                continue;
            }
        }
        await r.Response.WriteAsJsonAsync(new MultiFileResponse()
        {
            EndpointId = ApiId,
            Files = output.ToArray()
        });
    }

    /// <summary>
    /// /desc path for TaggerAPI. Process single file from form.
    /// </summary>
    private static async Task Desc(HttpContext r)
    {
        // This variable contains form for file
        IFormFile formFile = r.Request.Form.Files.First();
        
        // This variable container path to file on drive
        var filePath = await SaveFileToDrive(formFile);

        // Variable for results
        string results = string.Empty;
        
        // ... connect to AI model and get results
        try
        {
            results = RunModel(filePath);
        } // when failed, send error status code
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            r.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }
        
        // ... parse results and put into results variable
        results = ParseResults(results);
        
        r.Response.Headers.Append("Endpoint-Id", ApiId);
        await r.Response.WriteAsync(results);
        
        // Deleting temp file
        File.Delete(filePath);
    }

    private static string ParseResults(string results)
    {
        // Result should be in format "tag1, tag2, ..."
        return results.Replace(";", ", ");
    }

    private static string RunModel(string filePath)
    {
        return "Tag;Tag2;Tag3";
    }

    // Use this method if your model cannot tag videos and only works with images
    // This method will create image every ~1 second of video
    // Send it into model and then merge every result together
    // You can concat all unique tags into one entry and send it
    private static void PrepareVideo(string filePath)
    {
        Directory.CreateDirectory(filePath + ".d");
        int i = 0;
        using (var video = new VideoCapture(filePath))
        using (var img = new Mat())
        {
            while (video.Grab())
            {
                video.Retrieve(img);
                var filename = Path.Combine(filePath+".d", $"{i}.jpg");
                CvInvoke.Imwrite(filename, img);
                i+=(int) Math.Floor(video.Get(CapProp.Fps));
            }
        }
    }

    private static async Task<string> SaveFileToDrive(IFormFile formFile)
    {
        byte[] fileBytes = new byte[formFile.Length];
        await formFile.OpenReadStream().ReadExactlyAsync(fileBytes);
        var filePath = await WriteFileToDrive(formFile, fileBytes);
        return filePath;
    }

    private static async Task<string> WriteFileToDrive(IFormFile formFile, byte[] file)
    {
        string filePath = Path.Combine(_TempFolder, Path.GetRandomFileName() + "_" + formFile.FileName);
        Directory.CreateDirectory(_TempFolder);
        using (var f = File.Create(filePath)) await f.WriteAsync(file);
        return filePath;
    }
    
    public class SingleFileResponse
    {
        public string Filename{ get; set; }
        public string Data{ get; set; }
        public string EndpointId{ get; set; }
        
        public SingleFileResponse() {}

        public SingleFileResponse(string filename, string data, string endpointId)
        {
            Filename = filename;
            Data = data;
            EndpointId = endpointId;
        }
    }

    public class MultiFileResponse
    {
        public string EndpointId { get; set; }
        public SingleFileResponse[] Files { get; set; }
    }
}