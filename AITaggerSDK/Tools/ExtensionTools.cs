namespace AITaggerSDK.Tools;

public static class ExtensionTools
{
    public static string GetClearExtension(string filename)
    {
        return Path.GetExtension(filename).Replace(".", "");
    }
}