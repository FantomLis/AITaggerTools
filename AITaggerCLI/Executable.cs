using System.CommandLine;
using AITaggerSDK;
using Serilog;
using XmpCore;

namespace AITaggerCLI;

internal static class Executable
{
    #region Error strings

    private const string FAILED_TO_PROCESS_FILE = "Failed to process file {0}";
    private const string UNHANDLED_ERROR = "Unhandled error";
    private const string SERVER_RESPOND_FAIL = "Server failed to respond";
    private const string INVALID_ENDPOINT = "Invalid endpoint";

    #endregion
    public static int FileSendCount = 10;
    public static int Main(string[] args)
    {
        #region Program setup
        
        _SetupLogger();
        var cmd = _CreateTaggerCommand(out var inputPathsOption, out var endpointUrlOption, out var xmpFileSavePathOption, 
            out var backupPathOption, out var quickOption, out var webuiOption, out var clearTagsOption);
        string? endpointUrl = null, clearTag = null;
        
        #endregion
        
        cmd.Validators.Add(parseResult => {
            clearTag = parseResult.GetValue<string?>(clearTagsOption);
            endpointUrl = parseResult.GetValue<string?>(endpointUrlOption);
            if (endpointUrl == null && clearTag == null) parseResult.AddError("Endpoint should be present.");
        });
        
        cmd.SetAction(parseResult =>
        {
            if (parseResult.GetValue<bool>(webuiOption) == true)
            {
                _StartAsWebUI();
                return;
            }
            
            string[] paths = parseResult.GetValue(inputPathsOption)!;
            string? pathToBackup = parseResult.GetValue<string?>(backupPathOption);

            List<string> fileList = _GetAllFiles(paths);
            
            if (clearTag != null)
            {
                _StartAsCleaner(fileList, clearTag, pathToBackup);
                return;
            }
            _ExcludeTextFiles(fileList);
            
            string? xmpFileLocation = parseResult.GetValue<string?>(xmpFileSavePathOption);
            bool quick = parseResult.GetValue<bool>(quickOption);
            
            _StartAsTagger(fileList, xmpFileLocation, endpointUrl, pathToBackup, quick);
        });

        return cmd.Parse(args).Invoke();
    }
    
    private static RootCommand _CreateTaggerCommand(out Option<string[]> inputPathsOption, out Option<string?> endpointUrlOption,
        out Option<string?> xmpFileSavePathOption, out Option<string?> backupPathOption, out Option<bool> quickOption, out Option<bool> webuiOption,
        out Option<string?> clearTagsOption)
    {
        RootCommand rootCommand = new("CLI-tool for AI tags applying.\n" +
                                      "Original purpose of that app is to allow custom AI models to be used for smart search in Immich. \n" +
                                      "Requires AITagger REST API endpoint to send your images/videos.\n" +
                                      "When using folder, tool will scan all folders inside and scan every file.");

        inputPathsOption = new("--input", "-i")
        {
            Description = "Input file (should be video or image) or folder. Multiple inputs allowed.",
            Required = true,
            AllowMultipleArgumentsPerToken = true
        };
        endpointUrlOption = new("--endpoint", "-e")
        {
            Description = "REST API endpoint, that supports PUT /desc with image uploading.",
            Required = false,
            DefaultValueFactory = _ => null
        };
        xmpFileSavePathOption = new("--output", "-o")
        {
            Description = "Target file for .xmp files. Will be ignored when multiple inputs or directory as input is used.",
            Required = false,
            DefaultValueFactory = _ => null
        };
        backupPathOption = new("--backup", "-b")
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
        clearTagsOption = new("--clear", "-c")
        {
            Description = "Removes all tags with this endpoint id.",
            Required = false,
            DefaultValueFactory = _ => null
        };

        rootCommand.Options.Add(inputPathsOption);
        rootCommand.Options.Add(endpointUrlOption);
        rootCommand.Options.Add(xmpFileSavePathOption);
        rootCommand.Options.Add(backupPathOption);
        rootCommand.Options.Add(quickOption);
        rootCommand.Options.Add(webuiOption);
        rootCommand.Options.Add(clearTagsOption);
        return rootCommand;
    }

    private static void _StartAsTagger(List<string> fileList, string? xmpFileLocation, string? endpointUrl, string? pathToBackup,
        bool quick)
    {
        if (fileList.Count > 1 && xmpFileLocation is not null)
        {
            Log.Error("--output option will be ignored, multiple files supplied.");
        }
        
        var fileStatuses = _UseFiles(fileList.ToArray(), endpointUrl!,
            pathToBackup, quick);
        if (fileStatuses is null)  return;
        _LogFailedFiles(fileStatuses);
    }

    private static void _StartAsCleaner(List<string> files, string clearTag, string? backupFile)
    {
        List<string> xmpFiles = new(files.Count);
        foreach (var file in files)
        {
            if (!file.IsXmpFile()) continue;
            xmpFiles.Add(file);
        }

        int currentFile = 0, fileCount = xmpFiles.Count, fileSkipped = 0;
        foreach (var file in xmpFiles)      
        {
            UITools._LogProgress(currentFile, fileCount, fileSkipped);
            try
            {
                IXmpMeta xmpMeta = XmpManager.LoadFile(file);
                xmpMeta.ClearTags(clearTag).SaveFile(file, backupFile);
            }
            catch (XmpException ex)
            {
                _FormattedError(string.Format(FAILED_TO_PROCESS_FILE, file), ex.Message);
                fileSkipped++;
            }
            finally{currentFile++;}
        }
        UITools._LogProgress(currentFile, fileCount, fileSkipped);
    }

    private static void _StartAsWebUI()
    {
        throw new NotImplementedException("Currently WebUI is not implemented.");
    }
    
    private static Dictionary<string,TagApplierStatus>? _UseFiles(string[] filenames, string endpointUrl, string? backup = null, bool quick = true)
    {
        Dictionary<string, TagApplierStatus> fileStatuses = new Dictionary<string, TagApplierStatus>(filenames.Length);
        List<string> unprocessedFiles = new(filenames.Length);
        foreach (var filename in filenames)
        {
            switch (ExtensionTools.GetClearExtension(filename))
            {
                case "png":
                case "jpg":
                case "jpeg":
                case "webp":
                case "gif":
                case "avi":
                case "mp4":
                case "mkv":
                case "webm":
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
                UITools._LogProgress(currentFile, fileCount, fileSkipped);
                var curProcFiles = unprocessedFiles.Take(new Range(0, FileSendCount)).ToArray();
                var tagApplierStatuses = _GenerateDescriptionForFiles(curProcFiles, endpointUrl, backup, quick);
                for (var i = 0; i < tagApplierStatuses.Length; i++)
                {
                    var tagApplierStatus = tagApplierStatuses[i];
                    if (tagApplierStatus == TagApplierStatus.SKIPPED)
                    {
                        fileSkipped++;
                        Log.Information($"File {curProcFiles[i]} skipped.");
                    }
                    else if (tagApplierStatus == TagApplierStatus.OK)
                    {
                        Log.Information($"File {curProcFiles[i]} done.");
                    }
                    else
                    {
                        Log.Information($"File {curProcFiles[i]} failed.");
                    }
                    fileStatuses.Add(curProcFiles[i], tagApplierStatus);
                    unprocessedFiles.Remove(curProcFiles[i]);
                    currentFile++;
                }
            }
            catch (MultiFileException ex)
            {
                _FormattedError(string.Format(FAILED_TO_PROCESS_FILE, ex.Filename), ex.InnerException.Message);
                _DebugLogError(ex.InnerException);
                unprocessedFiles.Remove(ex.Filename);
                fileStatuses.Add(ex.Filename, TagApplierStatus.INVALID_FILE);
            }
            catch (AggregateException ex)
            {
                var msg = UNHANDLED_ERROR;
                if (ex.InnerException?.GetType() == typeof(InvalidOperationException))
                {
                    msg = INVALID_ENDPOINT;
                }
                else if (ex.InnerException?.GetType() == typeof(HttpRequestException))
                {
                    msg = SERVER_RESPOND_FAIL;
                }

                _FormattedError(msg, ex.InnerException?.Message);
                _DebugLogError(ex.InnerException);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Unhandled error: {ex}");
                _DebugLogError(ex);
                return null;
            }
        }
        UITools._LogProgress(currentFile, fileCount, fileSkipped);
        return fileStatuses;
    }

    private static TagApplierStatus[] _GenerateDescriptionForFiles(string[] filenames, string endpointUrl, string? backupPath = null, bool quick = true)
    {
        List<FileStream> fileStreams = new();
        foreach (var filename in filenames)
        {
            if (!File.Exists(filename)) throw new ArgumentException($"File {filename} does not exist.");
            
            fileStreams.Add(new FileStream(filename, FileMode.Open));
        }
        var apiResponse = APICaller.RequestMultiFileDescription(endpointUrl, fileStreams.ToArray()).Result;
        
        //Close files after processing every file
        fileStreams.ForEach(x => x.Close());
        fileStreams.ForEach(x => x.Dispose());
        fileStreams.Clear();
        
        List<TagApplierStatus> statusList = new(filenames.Length);
        foreach (var filename in filenames)
        {
            IXmpMeta xmpMeta;
            TagApplierStatus tagApplierStatus = TagApplierStatus.OK;
            try
            {
#if DEBUG
                xmpMeta = XmpManager.LoadFile(filename);
                Log.Debug("All properties: ");
                _DrawProperties(xmpMeta);
#endif
                var fileResult = apiResponse.Files.FirstOrDefault(x => x?.Filename == Path.GetFileName(filename), null);
                if (fileResult == null)
                {
                    statusList.Add(TagApplierStatus.BAD_RESPONSE);
                    continue;
                }
                xmpMeta = quick
                    ? TagApplier.QuickApplyTagsToFile(filename.ToXmpFileName(), apiResponse.EndpointId, fileResult.Data,
                        out tagApplierStatus)
                    : TagApplier.ApplyTagsToFile(filename.ToXmpFileName(), apiResponse.EndpointId, fileResult.Data);
#if DEBUG
                Log.Debug("All properties after update: ");
                _DrawProperties(xmpMeta);
#endif
                xmpMeta.SaveFile(filename.ToXmpFileName(), backupPath);
                statusList.Add(tagApplierStatus);
            }
            catch (XmpException e)
            {
                throw new MultiFileException(filename, e);
            }
        }
        return statusList.ToArray();
    }
    
    //TODO: Remove this method
    [Obsolete("Use _UseFiles instead. This method uses old /desc path and will not work with new version.", true)]
    private static TagApplierStatus UseFile(string filename, string endpointUrl, string? backup = null, bool quick = true, string? saveFileName = null)
    {
        switch (ExtensionTools.GetClearExtension(Path.GetExtension(filename)))
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
    
    //TODO: Remove this method
    [Obsolete("Use _GenerateDescriptionForFiles instead. This method uses old /desc path and will not work with new version.", true)]
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
        xmpMeta = quick ? TagApplier.QuickApplyTagsToFile(filename.ToXmpFileName(), apiResponse.EndpointId, apiResponse.Data, out tagApplierStatus) :
            TagApplier.ApplyTagsToFile(filename.ToXmpFileName(), apiResponse.EndpointId, apiResponse.Data);
            
#if DEBUG
        _DrawProperties(xmpMeta, "All properties after update: ");
#endif
        xmpMeta.SaveFile((saveFileName ?? filename).ToXmpFileName(), backupPath);
        return tagApplierStatus;
    }

#if DEBUG
    /// <summary>
    /// Shows properties from xmpMeta
    /// </summary>
    /// <param name="xmpMeta"></param>
    /// <param name="text"></param>
    private static void _DrawProperties(IXmpMeta xmpMeta)
    {
        foreach (var property in xmpMeta.Properties)
            Log.Debug($"Path={property.Path} Namespace={property.Namespace} Value={property.Value}");
        Log.Debug("============================");
    }
#endif
    
    private static void _DebugLogError(Exception ex)
    {
        Log.Debug(ex, "");
    }
    
    private static List<string> _GetAllFiles(string[] paths)
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

    private static void _ExcludeTextFiles(List<string> files)
    {
        List<string> excludeFile = new(files.Count);
        foreach (var file in files)
        {
            switch (ExtensionTools.GetClearExtension(Path.GetExtension(file)))
            {
                case "xmp":
                case "txt":
                    excludeFile.Add(file);
                    break;
            }
        }
        excludeFile.ForEach(x => files.Remove(x));
    }
    
    private static void _LogFailedFiles(Dictionary<string, TagApplierStatus> fileStatuses)
    {
        bool isAnyFailed = false;
        foreach (var (key, value) in fileStatuses)
        {
            if (value != TagApplierStatus.OK && value != TagApplierStatus.SKIPPED)
            {
                if (!isAnyFailed) Log.Error("This files failed to process: ");
                isAnyFailed = true;
                Log.Error($"{key}: {value.ToString()}");
            }
        }
    }
    private static void _FormattedError(string reason, string? message)
    {
        Log.Error($"{reason}{(message is null ? "": ": ")}{message}");
    }
    
    private static void _SetupLogger()
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#endif
            .WriteTo.Console().CreateLogger();
    }
}