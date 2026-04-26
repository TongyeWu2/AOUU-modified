namespace AOUU.Services;

public sealed record RecognitionResult(
    bool Matched,
    string? RegionName,
    string Message,
    string? DebugSessionPath = null);
