using Newtonsoft.Json;

namespace Turbo.Auth.Controllers.Auth.Body;

public class RefreshTokenBody
{
    [JsonProperty("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonProperty("deviceName")]
    public string? DeviceName { get; set; }
}
