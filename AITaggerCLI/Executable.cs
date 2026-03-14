using System.CommandLine;
using AITaggerSDK;
using Microsoft.VisualBasic;
using Serilog;
using XmpCore;

namespace AITaggerCLI;

internal static class Executable
{
    public static int Main(string[] args)
    {
        SetupLogger();
        var rootCommand = CreateRootCommand(out var inputOption, out var endpointOption, out var xmpFileLocationOption, out var backupOption, out var quickOption);
        rootCommand.SetAction(parseResult =>
        {
            string path = parseResult.GetValue(inputOption)!;
            string endpointUrl = parseResult.GetValue(endpointOption)!;
            List<string> files = new();
            if (File.GetAttributes(path).HasFlag(FileAttribute.Directory))
            {
                files.AddRange(Directory.GetFiles(path));
            }
            else files.Add(path);

            foreach (var filepath in files)
            {
                UseFile(filepath, endpointUrl, parseResult.GetValue<string?> (backupOption),parseResult.GetValue<bool>(quickOption));
            }
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static void UseFile(string filename, string endpointUrl, string? backup = null, bool quick = true)
    {
        switch (Path.GetExtension(filename).Replace(".", ""))
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
                Log.Error($"File {filename} is unsupported.");
                return;
        }

        try {
            GenerateDescription(filename, endpointUrl, backup, quick);
        }
        catch (XmpException ex)
        {
            Log.Error($"Failed to open .xmp file: {ex.Message}");
            Log.Debug(ex, "");
            return;
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
            return;
        }
        catch (Exception ex)
        {
            Log.Error($"Unhandled error: {ex}");
            Log.Debug(ex, "");
            return;
        }
        
        Log.Information($"File {filename} done.");
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
        out Option<string> xmpFileLocationOption, out Option<string?> backupOption, out Option<bool> quickOption)
    {
        RootCommand rootCommand = new("CLI-tool for AI tags applying.\n" +
                                      "Original purpose of that app is to allow custom AI models to be used for smart search in Immich. \n" +
                                      "Requires AITagger REST API endpoint to send your images/videos.\n" +
                                      "When using folder, tool will scan all folders inside and scan every file.");

        inputOption = new("--input", "-i")
        {
            Description = "Input file (should be video or image) or folder",
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
        quickOption = new("--quick-apply", "-q", "-quick")
        {
            Description = "Checks if any tag in .xmp file has endpoint id and skips file if so.",
            Required = false,
            DefaultValueFactory = _ => true
        };

        rootCommand.Options.Add(inputOption);
        rootCommand.Options.Add(endpointOption);
        rootCommand.Options.Add(xmpFileLocationOption);
        rootCommand.Options.Add(backupOption);
        rootCommand.Options.Add(quickOption);
        return rootCommand;
    }

    private static void GenerateDescription(string filename, string endpointUrl, string? backupPath = null, bool quick = true)
    {
        var apiResponse = APICaller.GenerateDescription(filename, endpointUrl).Result;

#if DEBUG
        IXmpMeta xmpMeta = XmpManager.LoadFile(filename);
        Log.Debug("All properties: ");
        foreach (var property in xmpMeta.Properties)
            Log.Debug($"Path={property.Path} Namespace={property.Namespace} Value={property.Value}");
        Log.Debug("============================");
#else 
        IXmpMeta xmpMeta;
#endif
            
        xmpMeta = quick ? TagApplier.QuickApplyTagsToFile(filename, apiResponse.EndpointId, apiResponse.Data) 
            : TagApplier.ApplyTagsToFile(filename, apiResponse.EndpointId, apiResponse.Data);
            
#if DEBUG
        Log.Debug("All properties after update: ");
        foreach (var property in xmpMeta.Properties)
            Log.Debug($"Path={property.Path} Namespace={property.Namespace} Value={property.Value}");
        Log.Debug("============================");
#endif
        xmpMeta.SaveFile(filename, (backupPath != null), backupPath ?? "");
    }
}