using System.Diagnostics.Contracts;
using Serilog;
using XmpCore;
using XmpCore.Options;

namespace AITaggerSDK;

public static class XmpManager
{
    const string DigikamNs = "http://www.digikam.org/ns/1.0/";
    const string DigikamTagsList = "digiKam:TagsList";

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
    
    public static IXmpMeta ApplyTag(this IXmpMeta xmpMeta, string id, string tag)
    {
        xmpMeta.AppendArrayItem(DigikamNs, DigikamTagsList, new PropertyOptions()
        {
            IsArray = true
        }, $"{id}/{tag}", new PropertyOptions());
        return xmpMeta;
    }

    public static IXmpMeta ApplyUniqueTag(this IXmpMeta xmpMeta, string id, string tag)
    {
        return xmpMeta.ApplyUniqueTags(id, [tag]);
    }

    private static bool DoesTagListExists(IXmpMeta xmpMeta)
    {
        return xmpMeta.DoesPropertyExist(DigikamNs, DigikamTagsList);
    }

    public static IXmpMeta ClearTags(this IXmpMeta xmpMeta, string id)
    {
        if (!DoesTagListExists(xmpMeta)) return xmpMeta;
        List<int> removedTags = new();
        for (int i = CountTags(xmpMeta); i >= 1; i--)
        {
            var arrVal = GetTag(xmpMeta, i);
            if (arrVal.Split('/')[0] == id) removedTags.Add(i);
        }
        removedTags.ForEach(x => xmpMeta.DeleteArrayItem(DigikamNs, DigikamTagsList, x));
        return xmpMeta;
    }

    private static string GetTag(IXmpMeta xmpMeta, int i)
    {
        return xmpMeta.GetArrayItem(DigikamNs, DigikamTagsList, i).Value;
    }

    private static int CountTags(IXmpMeta xmpMeta)
    {
        return xmpMeta.CountArrayItems(DigikamNs, DigikamTagsList);
    }

    public static IXmpMeta ApplyTags (this IXmpMeta xmpMeta, string id, params string[] tags)
    {
        foreach (var tag in tags)
        {
            xmpMeta.ApplyTag(id, tag);
        }

        return xmpMeta;
    }
    public static IXmpMeta ApplyUniqueTags(this IXmpMeta xmpMeta, string id,  params string[] tags)
    {
        if (!DoesTagListExists(xmpMeta)) return ApplyTags(xmpMeta, id, tags);
        List<string> uniqueTags = tags.ToList();
        var t = GetAllTaggerTags(xmpMeta, id);
        for (int i = 0; i < t.Length; i++)
        {
            var tagValInMeta = t[i];
            if (tags.Contains(CleanUpTag(tagValInMeta)))
                uniqueTags.Remove(CleanUpTag(tagValInMeta));
        }

        ApplyTags(xmpMeta, id, uniqueTags.ToArray());
        return xmpMeta;
    }

    public static string[] GetAllTaggerTags(this IXmpMeta xmpMeta, string id)
    {
        if (!DoesTagListExists(xmpMeta)) return [];
        List<string> tags = new();
        for (int i = 1; i <= CountTags(xmpMeta); i++)
        {
            var arrVal = GetTag(xmpMeta, i);
            if (arrVal.StartsWith(id+"/")) 
                tags.Add(arrVal);
        }

        return tags.ToArray();
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
    public static string CleanUpTag(string tag) => tag.Remove(0, tag.IndexOf('/')+1);

    [Pure]
    public static string ToXmpFileName(this string filename)
    {
        return $"{filename.Replace(".xmp", "")}.xmp";
    }

    [Pure]
    public static bool IsXmpFile(this string filename) => ExtensionTools.GetClearExtension(Path.GetExtension(filename)) == "xmp";
}