using System.CommandLine;
using AITaggerSDK;
using Serilog;
using XmpCore;

namespace AITaggerCLI;

internal static class Executable
{
    public static int Main(string[] args)
    {
        SetupLogger();
        var rootCommand = CreateRootCommand(out var inputOption, out var endpointOption, out var xmpFileLocationOption, out var backupOption);
        rootCommand.SetAction(parseResult =>
        {
            string name = parseResult.GetValue(inputOption)!;
            string endpoint = parseResult.GetValue(endpointOption)!;
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
            if (GenerateDescription(name, endpoint, parseResult.GetValue<string?> (backupOption))) Log.Information("Done.");
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static void SetupLogger()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .WriteTo.Console().CreateLogger();
    }

    private static RootCommand CreateRootCommand(out Option<string> inputOption, out Option<string> endpointOption,
        out Option<string> xmpFileLocationOption, out Option<string?> backupOption)
    {
        RootCommand rootCommand = new("CLI-tool for AI tags applying.\n" +
                                      "Original purpose of that app is to allow custom AI models to be used for smart search in Immich. \n" +
                                      "Requires AITagger REST API endpoint to send your images/videos.");

        inputOption = new("--input", "-i")
        {
            Description = "Input file (should be video or image)",
            Required = true
        };
        endpointOption = new("--endpoint", "-e")
        {
            Description = "REST API endpoint, that supports PUT /desc with image uploading.",
            Required = true
        };
        xmpFileLocationOption = new("--output", "-o")
        {
            Description = "Target file for .xmp files",
            Required = false
        };
        backupOption = new("--backup", "-b")
        {
            Description = "Move original file to other location with old_[DATE] prefix.",
            Required = false,
            DefaultValueFactory = _ => null
        };

        rootCommand.Options.Add(inputOption);
        rootCommand.Options.Add(endpointOption);
        rootCommand.Options.Add(xmpFileLocationOption);
        rootCommand.Options.Add(backupOption);
        return rootCommand;
    }

    private static bool GenerateDescription(string name, string endpoint, string? backup)
    {
        try
        {
            IXmpMeta xmpMeta = XmpManager.LoadFile(name);
            
            Log.Debug("All properties: ");
            foreach (var property in xmpMeta.Properties)
                Log.Debug($"Path={property.Path} Namespace={property.Namespace} Value={property.Value}");
            Log.Debug("============================");
            
            var apiResponse = APICaller.GenerateDescription(name, endpoint).Result;
            
            xmpMeta.ApplyUniqueTags(apiResponse.EndpointId,
                TagManager.ProcessTags(apiResponse.Data)).SaveFile(name,(backup != null), backup ?? "");
        }
        catch (XmpException ex)
        {
            Log.Error($"Failed to open .xmp file: {ex.Message}");
            Log.Debug(ex, "");
            return false;
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