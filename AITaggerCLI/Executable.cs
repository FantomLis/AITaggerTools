using System.CommandLine;
using AITaggerSDK;
using Serilog;
using XmpCore;
//TODO: Add command for starting web-panel

namespace AITaggerCLI;

internal static class Executable
{
    public static int FileSendCount = 10;
    public static int Main(string[] args)
    {
        SetupLogger();
        var rootCommand = CreateRootCommand(out var inputOption, out var endpointOption, out var xmpFileLocationOption, 
            out var backupOption, out var quickOption, out var webuiOption);
        rootCommand.SetAction(parseResult =>
        {
            if (parseResult.GetValue<bool>(webuiOption) == true)
            {
                throw new NotImplementedException("Currently WebUI is not implemented.");
            }
            string[] paths = parseResult.GetValue(inputOption)!;
            string endpointUrl = parseResult.GetValue(endpointOption)!;
            var files = GetAllFiles(paths);

            ExcludeTextFiles(files);
            
            string? xmpFileLocation = parseResult.GetValue<string?>(xmpFileLocationOption);
            if (files.Count > 1)
            {
                if (xmpFileLocation != null)
                {
                    Log.Error("--output option will be ignored, multiple files supplied.");
                }
                var fileStatuses = UseFiles(files.ToArray(), endpointUrl,
                    parseResult.GetValue<string?>(backupOption), parseResult.GetValue<bool>(quickOption));
                if (fileStatuses is null)  return;
                Log.Error("This files failed to process: ");
                foreach (var (key, value) in fileStatuses)
                {
                    if (value != TagApplierStatus.OK && value != TagApplierStatus.SKIPPED) 
                        Log.Error($"{key}: {value.ToString()}");
                }
            }
            else
            {
                UseFile(files.First(), endpointUrl,
                    parseResult.GetValue<string?>(backupOption), parseResult.GetValue<bool>(quickOption),
                    xmpFileLocation);
            }
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static List<string> GetAllFiles(string[] paths)
    {
        List<string> files = new();
        foreach (var path in paths)
        {
            if (File.GetAttributes(path).Equals(FileAttributes.Directory))
            {
                files.AddRange(Directory.GetFiles(path));
            }
            else files.Add(path);
        }

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

    private static Dictionary<string,TagApplierStatus>? UseFiles(string[] filenames, string endpointUrl, string? backup = null, bool quick = true)
    {
        Dictionary<string, TagApplierStatus> fileStatuses = new Dictionary<string, TagApplierStatus>(filenames.Length);
        List<string> unprocessedFiles = new(filenames.Length);
        foreach (var filename in filenames)
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
                    unprocessedFiles.Add(filename);
                    break;
                case ".xmp":
                case ".txt":
                    fileStatuses.Add(filename, TagApplierStatus.SKIPPED);
                    continue;
                default:
                    Log.Error($"File {filename} is unsupported.");
                    fileStatuses.Add(filename, TagApplierStatus.INVALID_TYPE);
                    continue;
            }
        }
        int fileCount = filenames.Length, fileSkipped = fileCount - unprocessedFiles.Count, currentFile = fileSkipped;
        while (unprocessedFiles.Count > 0)
        {
            try
            {
                var progress = (int)Math.Floor(((float)currentFile / fileCount) * 100);
                Log.Information($"{progress}% {string.Concat(Enumerable.Repeat('█', progress/5).Concat(Enumerable.Repeat('_', 20-(progress/5))))}" +
                                $"         {currentFile}/{fileCount} (skipped {fileSkipped} files)");
                var curProcFiles = unprocessedFiles.Take(new Range(0, FileSendCount)).ToArray();
                var tagApplierStatuses = GenerateDescriptionForFiles(curProcFiles, endpointUrl, backup, quick);
                for (var i = 0; i < tagApplierStatuses.Length; i++)
                {
                    var tagApplierStatus = tagApplierStatuses[i];
                    if (tagApplierStatus == TagApplierStatus.SKIPPED)
                    {
                        fileSkipped++;
                        Log.Information($"File {curProcFiles[i]} skipped.");
                    }
                    else
                    {
                        Log.Information($"File {curProcFiles[i]} done.");
                    }

                    unprocessedFiles.Remove(curProcFiles[i]);
                    fileCount++;
                }
            }
            catch (MultiFileException ex)
            {
                Log.Error($"Failed to process file {ex.Filename}: {ex.InnerException.Message}");
                Log.Debug(ex.InnerException, "");
                unprocessedFiles.Remove(ex.Filename);
                fileStatuses.Add(ex.Filename, TagApplierStatus.INVALID_FILE);
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
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Unhandled error: {ex}");
                Log.Debug(ex, "");
                return null;
            }
        }

        return fileStatuses;
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

        try
        {
            Log.Information($"Processing {filename}.");
            var tagApplierStatus = GenerateDescription(filename, endpointUrl, backup, quick, saveFileName);
            Log.Information(tagApplierStatus == TagApplierStatus.SKIPPED
                ? $"File {filename} skipped."
                : $"File {filename} done.");
            return tagApplierStatus;
        }
        catch (XmpException ex)
        {
            Log.Error($"Failed to open .xmp file: {ex.Message}");
            Log.Debug(ex, "");
            return TagApplierStatus.INVALID_FILE;
        }
        catch (HttpRequestException ex)
        {
            Log.Error($"Error when requesting data: {ex.Message}");
            Log.Debug(ex, "");
            return TagApplierStatus.NETWORK_FAILURE;
        }
        catch (AggregateException ex)
        {
            var msg = "Unhandled error";
            var failureStatus = TagApplierStatus.FAILED;
            if (ex.InnerException?.GetType() == typeof(InvalidOperationException))
            {
                msg = "Invalid endpoint";
                failureStatus = TagApplierStatus.NETWORK_FAILURE;
                return failureStatus;
            }
            else if (ex.InnerException?.GetType() == typeof(HttpRequestException))
            {
                msg = "Server failed to respond";
                failureStatus = TagApplierStatus.NETWORK_FAILURE;
                return failureStatus;
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

    private static RootCommand CreateRootCommand(out Option<string[]> inputOption, out Option<string> endpointOption,
        out Option<string?> xmpFileLocationOption, out Option<string?> backupOption, out Option<bool> quickOption, out Option<bool> webuiOption)
    {
        RootCommand rootCommand = new("CLI-tool for AI tags applying.\n" +
                                      "Original purpose of that app is to allow custom AI models to be used for smart search in Immich. \n" +
                                      "Requires AITagger REST API endpoint to send your images/videos.\n" +
                                      "When using folder, tool will scan all folders inside and scan every file.");

        inputOption = new("--input", "-i")
        {
            Description = "Input file (should be video or image) or folder. Multiple inputs allowed.",
            Required = true,
            AllowMultipleArgumentsPerToken = true
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
        webuiOption = new("--webui", "-ui", "-u")
        {
            Description = "Starts WebUI instead of CLI.",
            Required = false,
            DefaultValueFactory = _ => false
        };

        rootCommand.Options.Add(inputOption);
        rootCommand.Options.Add(endpointOption);
        rootCommand.Options.Add(xmpFileLocationOption);
        rootCommand.Options.Add(backupOption);
        rootCommand.Options.Add(quickOption);
        rootCommand.Options.Add(webuiOption);
        return rootCommand;
    }

    private static TagApplierStatus[] GenerateDescriptionForFiles(string[] filenames, string endpointUrl, string? backupPath = null, bool quick = true)
    {
        List<FileStream> fileStreams = new();
        foreach (var filename in filenames)
        {
            if (!File.Exists(filename)) throw new ArgumentException($"File {filename} does not exist.");
            
            fileStreams.Add(new FileStream(filename, FileMode.Open));
        }
        var apiResponse = APICaller.RequestMultiFileDescription(endpointUrl, fileStreams.ToArray()).Result;
        IXmpMeta xmpMeta;
        TagApplierStatus tagApplierStatus = TagApplierStatus.OK;
            
        List<TagApplierStatus> statusList = new(filenames.Length);
        foreach (var filename in filenames)
        {
            try
            {
#if DEBUG
                xmpMeta = XmpManager.LoadFile(filename);
                _DrawProperties(xmpMeta, "All properties: ");
#endif
                var fileResult = apiResponse.Files.First(x => x.Filename == filename);
                xmpMeta = quick
                    ? TagApplier.QuickApplyTagsToFile(filename, apiResponse.EndpointId, fileResult.Data,
                        out tagApplierStatus)
                    : TagApplier.ApplyTagsToFile(filename, apiResponse.EndpointId, fileResult.Data);
#if DEBUG
                _DrawProperties(xmpMeta, "All properties after update: ");
#endif
                xmpMeta.SaveFile(filename, backupPath != null, backupPath ?? "");
                statusList.Add(tagApplierStatus);
            }
            catch (XmpException e)
            {
                throw new MultiFileException(e, filename);
            }
            finally
            {
                fileStreams.ForEach(x => x.Close());
                fileStreams.ForEach(x => x.Dispose());
            }
        }
        fileStreams.ForEach(x => x.Close());
        fileStreams.ForEach(x => x.Dispose());
        return statusList.ToArray();
    }
    
    private static TagApplierStatus GenerateDescription(string filename, string endpointUrl, string? backupPath = null, bool quick = true, string? saveFileName = null)
    {
        if (!File.Exists(filename)) throw new ArgumentException("File does not exist.");
        APICaller.SingleFileResponse apiResponse;
        using (var fileStream = File.OpenRead(filename)) apiResponse = APICaller.RequestSingleFileDescription(endpointUrl, fileStream).Result;
        IXmpMeta xmpMeta;
        TagApplierStatus tagApplierStatus = TagApplierStatus.OK;
        
#if DEBUG
        xmpMeta = XmpManager.LoadFile(filename);
        _DrawProperties(xmpMeta, "All properties: ");
#endif
        xmpMeta = quick ? TagApplier.QuickApplyTagsToFile(filename, apiResponse.EndpointId, apiResponse.Data, out tagApplierStatus) :
            TagApplier.ApplyTagsToFile(filename, apiResponse.EndpointId, apiResponse.Data);
            
#if DEBUG
        _DrawProperties(xmpMeta, "All properties after update: ");
#endif
        xmpMeta.SaveFile(saveFileName ?? filename, (backupPath != null), backupPath ?? "");
        return tagApplierStatus;
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