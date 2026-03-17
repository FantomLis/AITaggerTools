using System.Diagnostics.Contracts;
using XmpCore;
using XmpCore.Options;

namespace AITaggerSDK.Managers;

public static class TagManager
{
    public static string[] ProcessTags(string tagString)
    {
        return tagString.Split(", ");
    }
    
    public static IXmpMeta ApplyTag(this IXmpMeta xmpMeta, string id, string tag)
    {
        xmpMeta.AppendArrayItem(XmpManager.DigikamNs, XmpManager.DigikamTagsList, new PropertyOptions()
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
        return xmpMeta.DoesPropertyExist(XmpManager.DigikamNs, XmpManager.DigikamTagsList);
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
        removedTags.ForEach(x => xmpMeta.DeleteArrayItem(XmpManager.DigikamNs, XmpManager.DigikamTagsList, x));
        return xmpMeta;
    }

    private static string GetTag(IXmpMeta xmpMeta, int i)
    {
        return xmpMeta.GetArrayItem(XmpManager.DigikamNs, XmpManager.DigikamTagsList, i).Value;
    }

    private static int CountTags(IXmpMeta xmpMeta)
    {
        return xmpMeta.CountArrayItems(XmpManager.DigikamNs, XmpManager.DigikamTagsList);
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

    public static string[] GetAllTaggerTags(this IXmpMeta xmpMeta, string? id)
    {
        if (string.IsNullOrEmpty(id)) return [];
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
    
    [Pure]
    public static string CleanUpTag(string tag) => tag.Remove(0, tag.IndexOf('/')+1);
}