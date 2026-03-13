namespace AITaggerSDK;

public static class TagManager
{
    public static string[] ProcessTags(string tagString)
    {
        return tagString.Split(", ");
    }
}