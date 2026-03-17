namespace AITaggerSDK;

public enum TaggerFileStatus : byte     
{
    OK = 0,
    SKIPPED = 1,
    FAILED = 2,
    INVALID_TYPE = 3,
    INVALID_FILE = 4,
    NETWORK_FAILURE = 5,
    BAD_RESPONSE = 6,
    SERVER_RESPONSE_FILE_NOT_FOUND = 7,
    IGNORE = 8,
    SERVER_ERROR = 9
}

public static class TaggerFileStatusExtension
{
    private static readonly TaggerFileStatus[] _FineStatuses = [TaggerFileStatus.OK, TaggerFileStatus.SKIPPED, TaggerFileStatus.IGNORE];
    public static bool IsFine(this TaggerFileStatus status) => _FineStatuses.Contains(status);
}