using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Turbo_Auth.Models.Ai.Image.Request;

public class Image2
{
    [JsonProperty("extra")]
    public dynamic? Extra
    {
        get;
        set;
    }

    [Required]
    [JsonProperty("request")]
    public Image2Request? Request
    {
        get;
        set;
    }
}