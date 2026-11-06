using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace RelaxKon.Auth.Contracts.Auth;

public class LoginRequest
{

    [Required, StringLength(20)]
    [JsonProperty("username")]
    public string? Username { get; set; }

    [Required, StringLength(512)]
    [JsonProperty("password")]
    public string? Password { get; set; }

    [StringLength(256)]
    [JsonProperty("deviceName")]
    public string? DeviceName { get; set; }
}
