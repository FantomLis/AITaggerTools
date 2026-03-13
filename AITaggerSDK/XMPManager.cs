using Serilog;
using XmpCore;

namespace AITaggerSDK;

public static class XmpManager
{
    public static IXmpMeta LoadFile(string name)
    {
        // I don't want to create it from scratch.
        IXmpMeta xmp = XmpMetaFactory.ParseFromString(
            @"<?xpacket begin=""﻿"" id=""W5M0MpCehiHzreSzNTczkc9d""?>
<x:xmpmeta xmlns:x=""adobe:ns:meta/"" x:xmptk=""Adobe XMP Core 6.1.10"">
  <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
    <rdf:Description rdf:about=""""
        xmlns:dc=""http://purl.org/dc/elements/1.1/"">
      <dc:description>
        <rdf:Alt>
        
        </rdf:Alt>
      </dc:description>
    </rdf:Description>
  </rdf:RDF>
</x:xmpmeta>");
        
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
        using (var fileIo = File.Create(file)) XmpMetaFactory.Serialize(xmpMeta, fileIo);
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