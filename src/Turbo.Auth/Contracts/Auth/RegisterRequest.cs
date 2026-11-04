using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo.Auth.Contracts.Auth;

public class RegisterRequest
{
    [Required, EmailAddress, StringLength(50)]
    [JsonProperty("email")]
    public string? Email { get; set; }
    [Required, StringLength(20)]
    [JsonProperty("username")]
    public string? Username { get; set; }

    [Required, StringLength(128, MinimumLength = 6)]
    [JsonProperty("password")]
    public string? Password { get; set; }
    [Required, Compare(nameof(Password))]
    [JsonProperty("confirm")]
    public string? Confirm { get; set; }
}