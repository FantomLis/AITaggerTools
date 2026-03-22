using XmpCore;

namespace AITaggerSDK.Managers;

public static class TagApplier
{
    public static IXmpMeta QuickApplyTagsToFile(string name, string endpointId, string tagData, out TaggerFileStatus status)
    {
        IXmpMeta xmpMeta = XmpManager.LoadFile(name);
        if (xmpMeta.IsTagsAlreadyExists(endpointId))
        {
            status = TaggerFileStatus.SKIPPED;
            return xmpMeta;
        }

        status = TaggerFileStatus.OK;
        return ApplyTagsToXmpMeta(xmpMeta,endpointId, tagData);
    }

    public static bool IsTagsAlreadyExists(this IXmpMeta xmpMeta, string? endpointId)
    {
        return !string.IsNullOrEmpty(endpointId) && xmpMeta.GetAllTaggerTags(endpointId).Length > 0;
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