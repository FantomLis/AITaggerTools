using System.Text.Json;
using AITaggerSDK.API.Responses;
using FFmpeg.NET;
using FFmpeg.NET.Events;
using ImageMagick;
using Imazen.WebP;
using InputFile = FFmpeg.NET.InputFile;

internal class Program
{
    // Always change API ID when creating new TaggerAPI
    /// <summary>
    /// Sets API ID for TaggerAPI
    /// </summary>
    const string ApiId = "AITaggerAPI-Example";
    static readonly Engine _Engine = new Engine(Environment.GetEnvironmentVariable(FFMPEGPATH) ?? "./ffmpeg.exe");
    /// <summary>
    /// Sets maximum form files size for request.
    /// </summary>
    public static int MaxFileSizeInMb = 1024;

    /// <summary>
    /// Sets maximum time before uploaded file will be removed.
    /// </summary>
    public static int MaxFileStoreTimeInMin = 60 * 4;

    private static Dictionary<string, DateTime> _FileRemovingStruct = new ();

    #region .env paramaters names

    private const string MAXFILESIZE = "AITAGGERAPI_MAXFILESIZE";
    private const string MAXFILESTORETIME = "AITAGGERAPI_MAXFILESTORETIME";
    private const string FFMPEGPATH = "AITAGGERAPI_FFMPEG_PATH";

    #endregion
    
    private static string _TempFolder => Path.Combine(Directory.GetCurrentDirectory(), "temp");
    public static void Main(string[] args)
    {
        ResourceLimits.Memory = 32;
        _Engine.Progress += _OnProgressFFmpeg;
        _Engine.Error += (sender, e) =>
            Console.WriteLine("[{0} => {1}]: Error: {2}\n{3}", e.Input.Name, e.Output?.Name, e.Exception.ExitCode, e.Exception.InnerException);
        MaxFileSizeInMb = int.Parse(Environment.GetEnvironmentVariable(MAXFILESIZE) ?? MaxFileSizeInMb.ToString());
        MaxFileStoreTimeInMin = int.Parse(Environment.GetEnvironmentVariable(MAXFILESTORETIME) ?? MaxFileSizeInMb.ToString());
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxFileSizeInMb * 1024 * 1024);

        var app = builder.Build();
        // Clearing temp upload folder on startup
        var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        app.UseHttpsRedirection();
        
        app.MapPost("/desc/upload", _Upload)
            .WithName("BulkFileUpload");
        app.MapGet("/desc/fetch", _Fetch)
            .WithName("BulkFileDescription");
        app.MapGet("/info", () => new EndpointInfo(ApiId))
            .WithName("ServerInfo");
        Task.Run(() =>
        {
            while (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
            {
                Thread.Sleep(60 * 1000);
                List<string> remove = new();
                lock (_FileRemovingStruct)
                {
                    foreach (var fileRemDate in _FileRemovingStruct)
                    {
                        if (fileRemDate.Value < DateTime.Now)
                        {
                            try
                            {
                                File.Delete(fileRemDate.Key);
                                remove.Add(fileRemDate.Key);
                                Console.WriteLine($"File {fileRemDate.Key} timeout reached.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to delete {fileRemDate.Key}: {ex.Message}");
                            }
                        }
                    }

                    remove.ForEach(x => _FileRemovingStruct.Remove(x));
                }
            }
        });
        app.Run();
    }
    
    /// <summary>
    /// /desc/bulk/upload path for TaggerAPI bulk file upload. After uploading, files will be stored on server for 4 hours. Returns 
    /// </summary>
    /// <param name="r"></param>
    private static async Task _Upload(HttpContext r)
    {
        IFormFile formFile = r.Request.Form.Files.First();
        
        Log(r.Session.Id, $"Uploaded file {formFile.FileName}. Saving...");
        
        var filePath = await _SaveFileToDrive(formFile);
        
        await r.Response.WriteAsync(Path.GetFileName(filePath));
        
        Log(r.Session.Id, $"Saved file as {filePath}.");

        _FileRemovingStruct.Add(filePath, DateTime.Now.AddMinutes(MaxFileStoreTimeInMin));
    }

    private static void Log(string id, string text)
    {
        Console.WriteLine($"{id}: {text}");
    }

    private static async Task _Fetch(HttpContext r)
    {
        List<string>? input = (await r.Request.ReadFromJsonAsync<List<string>>())?.ToList();
        if (input == null) return;
        List<string> filepaths = new();
        foreach (var filePath in input)
        {
            if (!File.Exists(Path.Combine(_TempFolder, filePath)))
            {
                Log(r.Session.Id, $"Failed to find file {filePath}.");
                continue;
            }
            // Prepare your files
            filepaths.Add(filePath);
            Log(r.Session.Id, $"Found file {filePath}.");
        }

        var output = new List<SingleFile>();
        foreach (var filePath in filepaths)
        {
            var result = "";
            try
            {
                // ... connect to AI model and get results
                Log(r.Session.Id, $"Running model for file {filePath}.");
                result = _RunModel(filePath);
                
                // ... parse results and put into results variable
                Log(r.Session.Id, $"Processing result for file {filePath}.");
                result = _ParseResults(result);
                output.Add(new SingleFile(Path.GetFileName(filePath), result));
            
                // Deleting temp file
                File.Delete(filePath);
            } // when failed, skip file
            catch (Exception ex)
            { 
                Log(r.Session.Id, $"Error for file {filePath}: {ex}");
                
                // Send some error info 
                // Please do not use ex.Message, send informative error message for client, not for developer
                output.Add(new SingleFile(Path.GetFileName(filePath), null, "Failed to process your file."));
                continue;
            }
        }
        Log(r.Session.Id, $"Done.");
        await r.Response.WriteAsJsonAsync(new MultiFileResponse(ApiId)
        {
            Files = output.ToArray()
        });
    }

    private static string _ParseResults(string results)
    {
        // Result should be in format "tag1, tag2, ..."
        return results.Replace(";", ", ");
    }

    private static string _RunModel(string filePath)
    {
        return "Tag;Tag2;Tag3";
    }

    // Use this method if your model cannot tag videos and only works with images
    // This method will create image every "frameTime" second of video
    // Send it into model and then merge every result together
    // You can concat all unique tags into one entry and send it
    private static async Task<string> _PrepareVideo(string filePath, float frameTime, CancellationToken token)
    {
        var path = filePath + ".d";
        Directory.CreateDirectory(path);
        var inputFile = new InputFile (filePath);
        var duration = (await _Engine.GetMetaDataAsync(inputFile, token)).Duration.TotalSeconds;
        int frameNumber = 0;
        for (float j = 0; j < duration; j+=frameTime)
        {
            var outputFile = new OutputFile(Path.Combine(path, $"{frameNumber++}.png"));
            await _Engine.GetThumbnailAsync(inputFile, outputFile, new ConversionOptions()
            {
                Seek = TimeSpan.FromSeconds(j)
            }, token);
        }

        return path;
    }
    
    // Use this method if your model cannot tag animated images (aka gifs) and only works with images
    // This method will get all frames from gif
    // Send it into model and then merge every result together
    // You can concat all unique tags into one entry and send it
    private static async Task<string> _PrepareAnimatedImage(string filePath, CancellationToken token)
    {
        var path = filePath + ".d";
        Directory.CreateDirectory(path);
        using var images = new MagickImageCollection();
        using var file = new FileStream(filePath, FileMode.Open);
        await images.ReadAsync(file, token);
        images.Coalesce(); 
        int frameCount = 0;
        foreach (var image in images)
        {
            await image.WriteAsync(Path.Combine(path, frameCount++ + ".png"), token);
        }

        return path;
    }
    
    // Same as previous, but uses different webp wrapper for situations when ImageMagick crashes your host with memory overflow
    private static async Task<string> _PrepareAnimatedImageWebp(string filePath, CancellationToken token)
    {
        var path = filePath + ".d";
        Directory.CreateDirectory(path);
        byte[] animatedWebP = await File.ReadAllBytesAsync(filePath, token);
        using var decoder = new AnimDecoder(animatedWebP);
        Console.WriteLine($"{decoder.Info.FrameCount} frames, {decoder.Info.Width}x{decoder.Info.Height}");
        int frameCount = 0;
        AnimFrame frame;
        while (decoder.HasMoreFrames())
        {
            frame = decoder.GetNextFrame()!;
            await File.WriteAllBytesAsync(Path.Combine(path, $"{frameCount++}.webp"), 
                WebPEncoder.Encode(frame.Pixels, frame.Width, frame.Height, frame.Width * 4, WebPPixelFormat.Bgra, quality: 80), token);
        }

        return path;
    }
    
    private static void _OnProgressFFmpeg(object sender, ConversionProgressEventArgs e)
    {
        Console.WriteLine("[{0} => {1}]", e.Input.MetaData.FileInfo.Name, e.Output?.Name);
        Console.WriteLine("Bitrate: {0}", e.Bitrate);
        Console.WriteLine("Fps: {0}", e.Fps);
        Console.WriteLine("Frame: {0}", e.Frame);
        Console.WriteLine("ProcessedDuration: {0}", e.ProcessedDuration);
        Console.WriteLine("Size: {0} kb", e.SizeKb);
        Console.WriteLine("TotalDuration: {0}\n", e.TotalDuration);
    }

    private static async Task<string> _SaveFileToDrive(IFormFile formFile)
    {
        byte[] fileBytes = new byte[formFile.Length];
        await formFile.OpenReadStream().ReadExactlyAsync(fileBytes);
        var filePath = await _WriteFileToDrive(formFile, fileBytes);
        return filePath;
    }

    private static async Task<string> _WriteFileToDrive(IFormFile formFile, byte[] file)
    {
        string filePath = Path.Combine(_TempFolder, Path.GetRandomFileName() + "_" + formFile.FileName);
        Directory.CreateDirectory(_TempFolder);
        using (var f = File.Create(filePath)) await f.WriteAsync(file);
        return filePath;
    }
}