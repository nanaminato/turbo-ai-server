using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo.Auth.Controllers.Auth.Body;

public class ChangePasswordBody
{
    [Required]
    [JsonProperty("currentPassword")]
    public string? CurrentPassword { get; set; }

    [Required]
    [MinLength(8)]
    [JsonProperty("newPassword")]
    public string? NewPassword { get; set; }

    [Required]
    [Compare(nameof(NewPassword))]
    [JsonProperty("confirmPassword")]
    public string? ConfirmPassword { get; set; }
}
