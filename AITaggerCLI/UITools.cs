using Serilog;

namespace AITaggerCLI;

internal static class UITools
{
    internal static void _LogProgress(int currentFile, int fileCount, int fileSkipped)
    {
        int progress = (int)Math.Floor(((float)currentFile / fileCount) * 100);
        Log.Information($"{progress}%\t{string.Concat(Enumerable.Repeat('█', progress/5).Concat(Enumerable.Repeat('_', 20-(progress/5))))}" +
                        $"         {currentFile}/{fileCount} (skipped {fileSkipped} files)");
    }
}