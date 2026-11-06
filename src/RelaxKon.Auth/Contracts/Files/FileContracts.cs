using System.ComponentModel.DataAnnotations;
namespace RelaxKon.Auth.Contracts.Files;

public sealed class FileExtractionRequest
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    private const int MaxBase64Length = (MaxFileBytes + 2) / 3 * 4;

    [Required, StringLength(255)] public string FileName { get; init; } = "";
    public string? FileType { get; init; }
    [Range(0, long.MaxValue)] public long FileSize { get; init; }
    [Required, StringLength(MaxBase64Length), Base64String] public string FileContent { get; init; } = "";
}
public sealed record FileExtractionResponse(string Content);
