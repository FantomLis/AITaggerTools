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
    
    public static IXmpMeta LoadFile(string name)
    {
        IXmpMeta xmp = XmpMetaFactory.Create();
        
        string file = ToXmpFileName(name);
        if (File.Exists(file))
        {
            Log.Debug($"Found file {file}, loading.");
            using (var stream = File.OpenRead(file))
                xmp = XmpMetaFactory.Parse(stream);
            
        }
        Log.Debug($"Done LoadFile for {name}");
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
        if (!xmpMeta.DoesPropertyExist(DigikamNs, DigikamTagsList)) ApplyTag(xmpMeta, id, tag);
        for (int i = 0; i < xmpMeta.CountArrayItems(DigikamNs, DigikamTagsList); i++)
        {
            if (xmpMeta.GetArrayItem(DigikamNs, DigikamTagsList, i).Value == tag) return xmpMeta;
        }
        ApplyTag(xmpMeta, id, tag);
        return xmpMeta;
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
        if (!xmpMeta.DoesPropertyExist(DigikamNs, DigikamTagsList)) ApplyTags(xmpMeta, id, tags);
        List<string> uniqueTags = tags.ToList();
        for (int i = 0; i < xmpMeta.CountArrayItems(DigikamNs, DigikamTagsList); i++)
        {
            var tagValInMeta = xmpMeta.GetArrayItem(DigikamNs, DigikamTagsList, i).Value;
            if (tags.Contains(tagValInMeta))
                uniqueTags.Remove(tagValInMeta);
        }

        ApplyTags(xmpMeta, id, uniqueTags.ToArray());
        return xmpMeta;
    }

    public static string[] GetAllTaggerTags(this IXmpMeta xmpMeta, string id)
    {
        List<string> tags = new();
        for (int i = 0; i < xmpMeta.CountArrayItems(DigikamNs, DigikamTagsList); i++)
        {
            var arrVal = xmpMeta.GetArrayItem(DigikamNs, DigikamTagsList, i).Value;
            if (arrVal.StartsWith(id+"/")) 
                tags.Add(arrVal);
        }

        return tags.ToArray();
    }

    [Pure]
    public static string CleanUpTag(string tag) => tag.Remove(0, tag.IndexOf('/'));

    public static IXmpMeta SaveFile(this IXmpMeta xmpMeta, string name, bool backupFile = true, string backupPath = "")
    {
        string file = ToXmpFileName(name);
        if (string.IsNullOrWhiteSpace(backupPath)) backupPath = Path.GetDirectoryName(file)!;
        if (File.Exists(file))
        {
            Log.Debug($"Found file {file}, cleaning up.");
            if (!backupFile)
            {
                var destFileName = ToXmpFileName(Path.Combine(backupPath, $"old_{DateTime.Now:yyyy-dd-M-HH-mm-ss}_" + Path.GetFileNameWithoutExtension(file)));
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
        Log.Debug($"Done SaveFile for {name}");
        return xmpMeta;
    }

    public static string ToXmpFileName(this string name)
    {
        return $"{name.Replace(".xmp", "")}.xmp";
    }
    
    [Obsolete("IdStructure is part of ApplyDataInDescription and is deprecated.")]
    const string IdStructure = "\n{0}: ";
    [Obsolete("Putting search data in description is deprecated. Use ApplyTags instead.")]
    public static IXmpMeta ApplyDataInDescription(this IXmpMeta metadata, string id, string data)
    {
        string currentDesc = string.Empty;
        var descPropName = "dc:description[1]";
        if (metadata.DoesPropertyExist(XmpConstants.NsDC, descPropName))
        {
            currentDesc = metadata.GetProperty(XmpConstants.NsDC, descPropName).Value;
        }

        var idStr = string.Format(IdStructure, id);
        if (currentDesc.Contains(idStr))
        {
            var startIndex = currentDesc.IndexOf(idStr, StringComparison.InvariantCulture);
            var endInd = currentDesc.IndexOf("\n", startIndex + 1, StringComparison.InvariantCulture);
            currentDesc = currentDesc.Remove(startIndex, endInd-startIndex+1);
        }

        currentDesc += idStr + data + "\n";
        metadata.SetProperty(XmpConstants.NsDC,descPropName,currentDesc);
        metadata.SetQualifier(XmpConstants.NsDC, descPropName, XmpConstants.NsXml, XmpConstants.XmlLang, XmpConstants.XDefault);
        return metadata;
    }
}