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
        var rootCommand = CreateRootCommand(out var inputOption, out var endpointOption, out var xmpFileLocationOption, out var backupOption, out var quickOption);
        rootCommand.SetAction(parseResult =>
        {
            string path = parseResult.GetValue(inputOption)!;
            string endpointUrl = parseResult.GetValue(endpointOption)!;
            var files = GetAllFiles(path);

            ExcludeTextFiles(files);
            
            string? xmpFileLocation = parseResult.GetValue<string?>(xmpFileLocationOption);
            if (files.Count > 1 && xmpFileLocation != null)
            {
                Log.Error("--output option will be ignored, multiple files supplied.");
                xmpFileLocation = null;
            }

            var fileStatus = ProceedFiles(files, endpointUrl, 
                parseResult.GetValue<string?>(backupOption), parseResult.GetValue<bool>(quickOption), xmpFileLocation);

            Log.Error("This files failed to process: ");
            fileStatus.ForEach(x =>
            {
                if (x.Item2 != TagApplierStatus.OK) 
                    Log.Error($"{x.Item1}: {x.Item2.ToString()}");
            });
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static List<Tuple<string, TagApplierStatus>> ProceedFiles(List<string> files, string endpointUrl, string? backup, 
        bool quick, string? xmpFileLocation)
    {
        List<Tuple<string, TagApplierStatus>> failedFiles = new List<Tuple<string, TagApplierStatus>>(files.Count);
        int fileCount = files.Count, fileSkipped = 0, currentFile = 0;
        foreach (var filepath in files)
        {
            var progress = (int)Math.Floor(((float)currentFile / fileCount) * 100);
            Log.Information($"{progress}% {string.Concat(Enumerable.Repeat('█', progress/5).Concat(Enumerable.Repeat('_', 20-(progress/5))))}" +
                            $"         {currentFile}/{fileCount} (skipped {fileSkipped} files)");
                
            TagApplierStatus tagApplierStatus = UseFile(filepath, endpointUrl, backup, quick, xmpFileLocation);
                
            switch (tagApplierStatus)
            {
                case TagApplierStatus.SKIPPED:
                case TagApplierStatus.FAILED:
                case TagApplierStatus.INVALID_TYPE:
                case TagApplierStatus.INVALID_FILE:
                case TagApplierStatus.NETWORK_FAILURE:
                    fileSkipped++;
                    break;
            }
            failedFiles.Add(new Tuple<string, TagApplierStatus>(filepath, tagApplierStatus));
            currentFile++;
        }

        return failedFiles;
    }

    private static List<string> GetAllFiles(string path)
    {
        List<string> files = new();
        if (File.GetAttributes(path).Equals(FileAttributes.Directory))
        {
            files.AddRange(Directory.GetFiles(path));
        }
        else files.Add(path);

        return files;
    }

    private static void ExcludeTextFiles(List<string> files)
    {
        List<string> excludeFile = new(files.Count);
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            switch (extension)
            {
                case ".xmp":
                case ".txt":
                    excludeFile.Add(file);
                    break;
            }
        }
        excludeFile.ForEach(x => files.Remove(x));
    }

    private static TagApplierStatus UseFile(string filename, string endpointUrl, string? backup = null, bool quick = true, string? saveFileName = null)
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
            case ".xmp":
            case ".txt":
                return TagApplierStatus.SKIPPED;
            default:
                Log.Error($"File {filename} is unsupported.");
                return TagApplierStatus.INVALID_TYPE;
        }

        try {
            var isSkipped = GenerateDescription(filename, endpointUrl, backup, quick, saveFileName);
            Log.Information(isSkipped == TagApplierStatus.SKIPPED ? $"File {filename} skipped." : $"File {filename} done.");
            return isSkipped;
        }
        catch (XmpException ex)
        {
            Log.Error($"Failed to open .xmp file: {ex.Message}");
            Log.Debug(ex, "");
            return TagApplierStatus.INVALID_FILE;
        }
        catch (AggregateException ex)
        {
            var msg = "Unhandled error";
            var failureStatus = TagApplierStatus.FAILED;
            if (ex.InnerException?.GetType() == typeof(InvalidOperationException))
            {
                msg = "Invalid endpoint";
                failureStatus = TagApplierStatus.NETWORK_FAILURE;

            }
            else if (ex.InnerException?.GetType() == typeof(HttpRequestException))
            {
                msg = "Server failed to respond";
                failureStatus = TagApplierStatus.NETWORK_FAILURE;
            }

            Log.Error($"{msg}: {ex.InnerException?.Message}");
            Log.Debug(ex, "");
            return failureStatus;
        }
        catch (Exception ex)
        {
            Log.Error($"Unhandled error: {ex}");
            Log.Debug(ex, "");
            return TagApplierStatus.FAILED;
        }
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
        out Option<string?> xmpFileLocationOption, out Option<string?> backupOption, out Option<bool> quickOption)
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
            Description = "Target file for .xmp files. Will be ignored when multiple inputs or directory as input is used.",
            Required = false,
            DefaultValueFactory = _ => null
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

    private static TagApplierStatus GenerateDescription(string filename, string endpointUrl, string? backupPath = null, bool quick = true, string? saveFileName = null)
    {
        var apiResponse = APICaller.GenerateDescription(filename, endpointUrl).Result;
        IXmpMeta xmpMeta;
        TagApplierStatus isSkipped = TagApplierStatus.OK;
        
#if DEBUG
        xmpMeta = XmpManager.LoadFile(filename);
        _DrawProperties(xmpMeta, "All properties: ");
#endif
        xmpMeta = quick ? TagApplier.QuickApplyTagsToFile(filename, apiResponse.EndpointId, apiResponse.Data, out isSkipped) :
            TagApplier.ApplyTagsToFile(filename, apiResponse.EndpointId, apiResponse.Data);
            
#if DEBUG
        _DrawProperties(xmpMeta, "All properties after update: ");
#endif
        xmpMeta.SaveFile(saveFileName ?? filename, (backupPath != null), backupPath ?? "");
        return isSkipped;
    }
#if DEBUG
    private static void _DrawProperties(IXmpMeta xmpMeta, string text)
    {
        Log.Debug(text);
        foreach (var property in xmpMeta.Properties)
            Log.Debug($"Path={property.Path} Namespace={property.Namespace} Value={property.Value}");
        Log.Debug("============================");
    }
#endif
}