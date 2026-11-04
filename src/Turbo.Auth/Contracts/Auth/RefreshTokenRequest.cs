using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo.Auth.Contracts.Auth;

public class RefreshTokenRequest
{
    [Required, StringLength(4096)]
    [JsonProperty("refreshToken")]
    public string? RefreshToken { get; set; }

    [StringLength(256)]
    [JsonProperty("deviceName")]
    public string? DeviceName { get; set; }
}
