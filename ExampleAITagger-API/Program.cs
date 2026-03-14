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
    const int MaxFileSizeInMb = 1024;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxFileSizeInMb * 1024 * 1024);

        var app = builder.Build();
        Directory.Delete(Path.Combine(Directory.GetCurrentDirectory(), "temp"), true);
        app.UseHttpsRedirection();
        
        app.MapPost("/desc", Desc)
            .WithName("AIDescription");

        app.Run();
    }

    /// <summary>
    /// /desc path for TaggerAPI. Process single file from form.
    /// </summary>
    private static async Task Desc(HttpContext r)
    {
        // This variable contains form for file
        IFormFile formFile = r.Request.Form.Files.First();

        // This variable contains file bytes
        byte[] file = new byte[formFile.Length];
        await formFile.OpenReadStream().ReadExactlyAsync(file);
        // ...from here
        
        // Variable for results
        string results = string.Empty;
        
        // Writing file to temp folder
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "temp", Path.GetRandomFileName() + "_" + formFile.FileName);
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "temp"));
        using (var f = File.Create(filePath)) await f.WriteAsync(file);
        
        // ... connect to AI model and get results
        
        // ... parse results and put into results variable
        results = "Tag1, Tag2, Tag3";
        
        r.Response.Headers.Append("Endpoint-Id", ApiId);
        await r.Response.WriteAsync(results);
        
        // Deleting temp file
        File.Delete(filePath);
    }
}