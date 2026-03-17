using System.Diagnostics.Contracts;
using AITaggerSDK.Tools;
using Serilog;
using XmpCore;
using XmpCore.Options;

namespace AITaggerSDK.Managers;

public static class XmpManager
{
    public const string DigikamNs = "http://www.digikam.org/ns/1.0/";
    public const string DigikamTagsList = "digiKam:TagsList";

    static XmpManager()
    {
        XmpMetaFactory.SchemaRegistry.RegisterNamespace(DigikamNs, "digiKam");
    }
    
    public static IXmpMeta LoadFile(string file)
    {
        IXmpMeta xmp = XmpMetaFactory.Create();
        if (File.Exists(file))
        {
            Log.Debug($"Found file {file}, loading.");
            using (var stream = File.OpenRead(file))
                xmp = XmpMetaFactory.Parse(stream);
        } else Log.Debug($"File {file} not found. Using clear IXmpMeta.");
        Log.Debug($"Done loading file {file}");
        return xmp;
    }

    public static IXmpMeta SaveFile(this IXmpMeta xmpMeta, string file, string? backupPath = null)
    {
        var isBackupEnabled = !string.IsNullOrEmpty(backupPath);
        if (!isBackupEnabled) backupPath = Path.GetDirectoryName(file)!;
        
        if (File.Exists(file))
        {
            Log.Debug($"Found file {file}, cleaning up.");
            if (isBackupEnabled)
            {
                Directory.CreateDirectory(backupPath!);
                var destFileName = ToXmpFileName(Path.Combine(backupPath!, $"old_{DateTime.Now:yyyy-dd-M-HH-mm-ss-ffff}_" + Path.GetFileNameWithoutExtension(file)));
                File.Copy(file, destFileName);
                Log.Debug($"Created file {destFileName} as backup.");
            }
            File.Delete(file);
        }
        Log.Debug($"Writing {file}.");
        using (var fileIo = File.Create(file)) XmpMetaFactory.Serialize(xmpMeta, fileIo, new SerializeOptions()
        {
            // For some reason, padding in this lib works weird when padding is zero 
            Padding = 1
        });
        Log.Debug($"Done saving file {file}.");
        return xmpMeta;
    }

    [Pure]
    public static string ToXmpFileName(this string filename)
    {
        return $"{filename.Replace(".xmp", "")}.xmp";
    }

    [Pure]
    public static bool IsXmpFile(this string filename) => ExtensionTools.GetClearExtension(Path.GetExtension(filename)) == "xmp";
}