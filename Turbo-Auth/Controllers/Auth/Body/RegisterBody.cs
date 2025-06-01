using Newtonsoft.Json;

namespace Turbo_Auth.Controllers.Auth.Body;

public class RegisterBody
{
    [JsonProperty("email")]
    public string? Email { get; set; }
    [JsonProperty("username")]
    public string? Username { get; set; }

    [JsonProperty("password")]
    public string? Password { get; set; }
    [JsonProperty("confirm")]
    public string? Confirm { get; set; }
}