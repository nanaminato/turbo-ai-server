using Newtonsoft.Json;

namespace Turbo_Auth.Controllers.Auth.Body;

public class SignBody
{

    [JsonProperty("username")]
    public string? Username { get; set; }

    [JsonProperty("password")]
    public string? Password { get; set; }
}