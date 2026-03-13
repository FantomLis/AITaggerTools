using XmpCore;

namespace AITaggerSDK;

public static class TagApplier
{
    public static IXmpMeta QuickApplyTagsToFile(string name, string endpointId, string tagData)
    {
        IXmpMeta xmpMeta = XmpManager.LoadFile(name);
        if (xmpMeta.GetAllTaggerTags(endpointId).Length > 0) return xmpMeta;
        return ApplyTagsToXmpMeta(xmpMeta,endpointId, tagData);
    }

    public static IXmpMeta ApplyTagsToFile(string name, string endpointId, string tagData)
    {
        IXmpMeta xmpMeta = XmpManager.LoadFile(name);
        return ApplyTagsToXmpMeta(xmpMeta,endpointId, tagData);
    }

    public static IXmpMeta ApplyTagsToXmpMeta(IXmpMeta xmpMeta, string endpointId, string tagData)
    {
        xmpMeta.ApplyUniqueTags(endpointId,
            TagManager.ProcessTags(tagData));
        return xmpMeta;
    }

    public static IXmpMeta ApplyTags(this IXmpMeta xmpMeta, string endpointId, string tagData) =>
        ApplyTagsToXmpMeta(xmpMeta, endpointId, tagData);
}