using System.Text;

const string ApiId = "ExampleAPI";

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapPost("/desc", async (r) =>
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
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "temp", formFile.FileName);
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "temp"));
        using (var f = File.Create(filePath)) await f.WriteAsync(file);
        
        // ... connect to AI model and get results
        
        // ... parse results and put into results variable
        results = "Example description ;)";
        
        r.Response.Headers.Append("Endpoint-Id", ApiId);
        await r.Response.WriteAsync(results);
        
        // Deleting temp file
        //File.Delete(filePath);
    })
    .WithName("AIDescription");

app.Run();