using System.CommandLine;
using AITaggerCLI.Exceptions;
using AITaggerCLI.Tools;
using AITaggerSDK;
using AITaggerSDK.API.Responses;
using AITaggerSDK.Containers;
using AITaggerSDK.Managers;
using AITaggerSDK.Tools;
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
            out var backupPathOption, out var quickOption, out var webuiOption, out var clearTagsOption, out var ignoreInvalidExtensionsOption, out var limitFileCount);
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
            bool ignoreExt = parseResult.GetValue(ignoreInvalidExtensionsOption);
            FileSendCount = parseResult.GetValue<int>(limitFileCount);

            List<string> fileList = _GetAllFiles(paths);
            
            if (clearTag != null)
            {
                _StartAsCleaner(fileList, clearTag, pathToBackup);
                return;
            }
            _ExcludeTextFiles(fileList);
            
            string? xmpFileLocation = parseResult.GetValue<string?>(xmpFileSavePathOption);
            bool quick = parseResult.GetValue<bool>(quickOption);
            
            _StartAsTagger(fileList, xmpFileLocation, endpointUrl, pathToBackup, quick, ignoreExt);
        });

        return cmd.Parse(args).Invoke();
    }
    
    private static RootCommand _CreateTaggerCommand(out Option<string[]> inputPathsOption, out Option<string?> endpointUrlOption,
        out Option<string?> xmpFileSavePathOption, out Option<string?> backupPathOption, out Option<bool> quickOption, out Option<bool> webuiOption,
        out Option<string?> clearTagsOption, out Option<bool> ignoreInvalidExtensionsOption, out Option<int> limitFileCount)
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
        ignoreInvalidExtensionsOption = new("--ignore-endpoint-extensions", "-iee")
        {
            Description =
                "Ignores file extension from endpoint and forces to send all files. Be aware that this will not process your file if it isn't supported.",
            Required = false,
            DefaultValueFactory = _ => false
        };
        limitFileCount = new("--limit-filecount", "-lf", "--limit")
        {
            Description =
                "Limits how many files will be uploaded to endpoint before requesting result.",
            Required = false,
            DefaultValueFactory = _ => -1
        };

        rootCommand.Options.Add(inputPathsOption);
        rootCommand.Options.Add(endpointUrlOption);
        rootCommand.Options.Add(xmpFileSavePathOption);
        rootCommand.Options.Add(backupPathOption);
        rootCommand.Options.Add(quickOption);
        rootCommand.Options.Add(webuiOption);
        rootCommand.Options.Add(clearTagsOption);
        rootCommand.Options.Add(ignoreInvalidExtensionsOption);
        rootCommand.Options.Add(limitFileCount);
        return rootCommand;
    }
    
    private static int _taggerModeRetryCount = 0;
    private static void _StartAsTagger(List<string> fileList, string? xmpFileLocation, string? endpointUrl, string? pathToBackup,
        bool quick, bool ignoreExt)
    {
        if (fileList.Count > 1 && xmpFileLocation is not null)
        {
            Log.Error("--output option will be ignored, multiple files supplied.");
        }

        List<string> currentFileList = new List<string>(fileList);
        while (_taggerModeRetryCount < 3 && currentFileList.Count > 0)
        {
            if (_taggerModeRetryCount != 0) Log.Information("Retrying to process files...");
            var fileStatuses = _UseFiles(currentFileList.ToArray(), endpointUrl!,
                pathToBackup, quick, ignoreExt);
            if (fileStatuses is null)
            {
                _taggerModeRetryCount++;
                continue;
            }
            bool isAnyFailed = false;
            currentFileList.Clear();
            foreach (var fileStatus in fileStatuses)
            {
                if (!fileStatus.ProcessingStatus.IsFine())
                {
                    if (!isAnyFailed) Log.Error("This files failed to process: ");
                    isAnyFailed = true;
                    Log.Error($"{fileStatus.Filename}: {fileStatus.Error} ({fileStatus.ProcessingStatus})");
                    currentFileList.Add(fileStatus.Filename);
                }
            }

            if (isAnyFailed)
            {
                _taggerModeRetryCount++;
                continue;
            }
            return;
        }
        Log.Error("Failed to proceed all files.");
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
            UITools._LogFileProgress(currentFile, fileCount, fileSkipped);
            try
            {
                IXmpMeta xmpMeta = XmpManager.LoadFile(file);
                xmpMeta.ClearTags(clearTag).SaveFile(file, backupFile);
                Log.Information($"File {file} cleared.");
            }
            catch (XmpException ex)
            {
                _FormattedError(string.Format(FAILED_TO_PROCESS_FILE, file), ex.Message);
                fileSkipped++;
            }
            finally{currentFile++;}
        }
        UITools._LogFileProgress(currentFile, fileCount, fileSkipped);
    }

    private static void _StartAsWebUI()
    {
        throw new NotImplementedException("Currently WebUI is not implemented.");
    }
    private static List<FileProcessingResult>? _UseFiles(string[] filenames, string endpointUrl, string? backup = null, bool quick = true, bool ignoreExt = false)
    {
        List<FileProcessingResult> fileStatuses = new (filenames.Length);
        List<string> unprocessedFiles;
        try
        {
            unprocessedFiles = _CreateFileList(filenames, endpointUrl, quick,ignoreExt, fileStatuses);
        }
        catch (AggregateException ex)
        {
            return _NetworkAggregateException(ex);
        }

        int fileCount = filenames.Length, fileSkipped = fileCount - unprocessedFiles.Count, currentFile = fileSkipped;
        int fileSendCount = FileSendCount == -1 ? unprocessedFiles.Count : FileSendCount;
        while (unprocessedFiles.Count > 0)
        {
            try
            {
                UITools._LogFileProgress(currentFile, fileCount, fileSkipped);
                Log.Information($"Uploading {fileSendCount} files...");
                var currentFilesArray = unprocessedFiles.Take(new Range(0, fileSendCount)).ToArray();
                var fileProcessingResults = 
                    _GenerateDescriptionForFiles(currentFilesArray, endpointUrl, backup);
                foreach (var result in fileProcessingResults)
                {
                    fileStatuses.Add(result);
                    unprocessedFiles.Remove(result.Filename);
                    currentFile++;
                }
            }
            catch (AggregateException ex)
            {
                return _NetworkAggregateException(ex);
            }
            catch (Exception ex)
            {
                Log.Error($"Unhandled error: {ex}");
                _DebugLogError(ex);
                return null;
            }
        }
        UITools._LogFileProgress(currentFile, fileCount, fileSkipped);
        return fileStatuses;
    }

    private static List<FileProcessingResult>? _NetworkAggregateException(AggregateException ex)
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
    
    private static void _LogFileProcessingResult(FileProcessingResult tagApplierStatus)
    {
        if (tagApplierStatus.ProcessingStatus.IsFine())
        {
            Log.Information($"File {tagApplierStatus.Filename} done.");
        }
        else if (tagApplierStatus.ProcessingStatus == TaggerFileStatus.SKIPPED)
        {
            Log.Information($"File {tagApplierStatus.Filename} skipped.");
        }
        else
        {
            Log.Information($"File {tagApplierStatus.Filename} failed: {tagApplierStatus.Error!}");
        }
    }

    private static List<string> _CreateFileList(string[] filenames, string endpointUrl, bool quick, bool ignoreInvalidExtensions, List<FileProcessingResult> fileStatuses)
    {
        List<string> unprocessedFiles = new(filenames.Length);
        var endpointInfo = TaggerAPIManager.RequestEndpointInfo(TaggerAPIManager.Default, endpointUrl).Result;
        if (endpointInfo is null) throw new AggregateException(new HttpRequestException("Server info is null."));
        foreach (var filename in filenames)
        {
            var ext = ExtensionTools.GetClearExtension(filename);
            switch (ext)
            {
                case "xmp":
                case "txt":
                    fileStatuses.Add(FileProcessingResult.CreateIgnored(filename));
                    continue;
                default:
                    if (!endpointInfo.IsFiletypeSupported(ext))
                    {
                        if (!ignoreInvalidExtensions)
                        {
                            fileStatuses.Add(FileProcessingResult.CreateErrorResult(filename,
                                TaggerFileStatus.INVALID_TYPE,
                                $"File {filename} is unsupported."));
                            continue;
                        }
                        Log.Warning($"File {filename} is unsupported, but --ignore-endpoint-extensions was used.");
                    }
                    break;
            }

            if (!quick)
            {
                unprocessedFiles.Add(filename);
                continue;
            }
            
            if (!XmpManager.LoadFile(filename.ToXmpFileName())
                    .IsTagsAlreadyExists(endpointInfo?.EndpointId))
            {
                unprocessedFiles.Add(filename);
            }
            else fileStatuses.Add(FileProcessingResult.CreateSkipped(filename));
        }

        return unprocessedFiles;
    }

    private static FileProcessingResult[] _GenerateDescriptionForFiles(string[] filenames, string endpointUrl, string? backupPath = null)
    {
        var apiResponse = _GetDescriptionResults(filenames, endpointUrl);

        List<FileProcessingResult> statusList = new(filenames.Length);
        foreach (var filename in filenames)
        {
            var fileProcessResult = new FileProcessingResult(filename,
                TaggerFileStatus.OK);
            try
            {
#if DEBUG
                Log.Debug("All properties: ");
                _DrawProperties(XmpManager.LoadFile(filename));
#endif
                var fileResult = apiResponse.Files.FirstOrDefault(x => x?.Filename == Path.GetFileName(filename), null);
                if (fileResult == null)
                {
                    fileProcessResult = FileProcessingResult.CreateErrorResult(filename,
                        TaggerFileStatus.SERVER_RESPONSE_FILE_NOT_FOUND, "File was not found in server response.");
                    continue;
                }

                if (fileResult.IsError)
                {
                    fileProcessResult = FileProcessingResult.CreateErrorResult(filename,
                        TaggerFileStatus.SERVER_ERROR, fileResult.Error!);
                    continue;
                }
#if DEBUG
IXmpMeta xmpMeta =
#endif
                TagApplier.ApplyTagsToFile(filename.ToXmpFileName(), apiResponse.EndpointId, fileResult.TagsInfo!)
#if DEBUG
    ; xmpMeta
#endif
                    .SaveFile(filename.ToXmpFileName(), backupPath);
#if DEBUG
                Log.Debug("All properties after update: ");
                _DrawProperties(xmpMeta);
#endif
            }
            catch (XmpException e)
            {
                statusList.Add(FileProcessingResult.CreateErrorResult(filename,
                    TaggerFileStatus.INVALID_FILE, e.Message));
            }
            finally
            {
                statusList.Add(fileProcessResult);
                _LogFileProcessingResult(fileProcessResult);
            }
        }
        return statusList.ToArray();
    }

    private static MultiFileResponse _GetDescriptionResults(string[] filenames, string endpointUrl)
    {
        Dictionary<string, string> fileMap = new();
        float progress = 0, progressStep = 100f / filenames.Length;
        foreach (var filename in filenames)
        {
            if (!File.Exists(filename)) throw new ArgumentException($"File {filename} does not exist.");
            using var file = new FileStream(filename, FileMode.Open);
            Log.Information("Uploading files: " + UITools._BuildProgressBar((int)Math.Floor(progress)));
            fileMap.Add(TaggerAPIManager.UploadFile(TaggerAPIManager.Default, endpointUrl, file).Result,Path.GetFileName(file.Name));
            Log.Information($"File {filename} uploaded.");
            progress += progressStep;
        }
        Log.Information("Uploading files: " + UITools._BuildProgressBar((int)Math.Floor(progress)));
        
        var multiFileResponse = TaggerAPIManager.FetchDescription(TaggerAPIManager.Default, endpointUrl, fileMap.Keys).Result;
        try
        {
            foreach (var file in multiFileResponse.Files)
            {
                file.Filename = fileMap[file.Filename];
            }
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Invalid response: {ex.Message}");
        }
        return multiFileResponse;
    }
    
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