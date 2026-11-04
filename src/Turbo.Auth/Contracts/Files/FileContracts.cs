using System.ComponentModel.DataAnnotations;
namespace Turbo.Auth.Contracts.Files;

public sealed class FileExtractionRequest
{
    [Required, StringLength(255)] public string FileName { get; init; } = "";
    public string? FileType { get; init; }
    [Range(0, long.MaxValue)] public long FileSize { get; init; }
    [Required, Base64String] public string FileContent { get; init; } = "";
}
public sealed record FileExtractionResponse(string Content);
