namespace AITaggerSDK;

public enum TagApplierStatus : byte     
{
    OK = 0,
    SKIPPED = 1,
    FAILED = 2,
    INVALID_TYPE = 3,
    INVALID_FILE = 4,
    NETWORK_FAILURE = 5,
    BAD_RESPONSE = 6
}