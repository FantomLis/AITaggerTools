using System.CommandLine;
using AITaggerSDK;
using Serilog;
using XmpCore;

namespace AITaggerCLI;

internal static class Executable
{
    public static int Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            #if DEBUG
            .MinimumLevel.Debug()
            #endif
            .WriteTo.Console().CreateLogger();
        RootCommand rootCommand = new("CLI-tool for AI tags applying.\n" +
                                      "Original purpose of that app is to allow custom AI models to be used for smart search in Immich. \n" +
                                      "Requires AITagger REST API endpoint to send your images/videos.");

        Option<string> inputOption = new("--input", "-i")
        {
            Description = "Input file (should be video or image)",
            Required = true
        };
        Option<string> endpointOption = new("--endpoint", "-e")
        {
            Description = "REST API endpoint, that supports PUT /desc with image uploading.",
            Required = true
        };
        Option<string> xmpFileLocationOption = new("--output", "-o")
        {
            Description = "Target file for .xmp files",
            Required = false
        };
        Option<string?> backupOption = new("--backup", "-b")
        {
            Description = "Move original file to other location with old_[DATE] prefix.",
            Required = false,
            DefaultValueFactory = _ => null
        };

        rootCommand.Options.Add(inputOption);
        rootCommand.Options.Add(endpointOption);
        rootCommand.Options.Add(xmpFileLocationOption);
        rootCommand.Options.Add(backupOption);
        rootCommand.SetAction(parseResult =>
        {
            string name = parseResult.GetValue(inputOption)!;
            string endpoint = parseResult.GetValue(endpointOption)!;
            IXmpMeta metadata = XmpManager.LoadFile(name);
            switch (Path.GetExtension(name).Replace(".", ""))
            {
                case "png":
                case "jpg":
                case "jpeg":
                case "webp":
                case "gif":
                case "avi":
                case "mp4":
                case "mkv":
                    break;
                default:
                    Log.Warning($"File {name} can be unsupported. Be aware of that.");
                    break;
            }
            if (GenerateDescription(metadata, name, endpoint, parseResult, backupOption)) Log.Information("Done.");
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static bool GenerateDescription(IXmpMeta metadata, string name, string endpoint, ParseResult parseResult,
        Option<string?> backupOption)
    {
        Log.Debug("All properties: ");
        foreach (var property in metadata.Properties)
            Log.Debug($"Path={property.Path} Namespace={property.Namespace} Value={property.Value}");
        Log.Debug("============================");
        
        try
        {
            var apiResponse = APICaller.GenerateDescription(name, endpoint).Result;
            var backupPath = parseResult.GetValue<string?>(backupOption);
            XmpManager.LoadFile(name).ApplyDataInDescription(apiResponse.EndpointId,
                apiResponse.Data).SaveFile(name,(backupPath != null), backupPath ?? "");
        }
        catch (AggregateException ex)
        {
            var msg = "Unhandled error";
            if (ex.InnerException?.GetType() == typeof(InvalidOperationException))
            {
                msg = "Invalid endpoint";

            }
            else if (ex.InnerException?.GetType() == typeof(HttpRequestException))
            {
                msg = "Server failed to respond";
            }

            Log.Error($"{msg}: {ex.InnerException?.Message}");
            Log.Debug(ex, "");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error($"Unhandled error: {ex}");
            Log.Debug(ex, "");
            return false;
        }

        return true;
    }
}