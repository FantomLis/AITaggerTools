using Serilog;

namespace AITaggerCLI.Tools;

internal static class UITools
{
    internal static void _LogFileProgress(int currentFile, int fileCount, int fileSkipped)
    {
        int progress = (int)Math.Floor(((float)currentFile / fileCount) * 100);
        Log.Information(_BuildProgressBar(progress) +
                        $"         {currentFile}/{fileCount} (skipped {fileSkipped} files)");
    }

    internal static string _BuildProgressBar(int progress)
    {
        return $"{progress}%\t{string.Concat(Enumerable.Repeat('█', progress/5).Concat(Enumerable.Repeat('_', 20-(progress/5))))}";
    }
}