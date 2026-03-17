namespace AITaggerSDK.Containers;

public record FakeFileContainer(Stream File, string Filename)
{
    public readonly Stream File = File;
    public readonly string Filename = Filename;
}