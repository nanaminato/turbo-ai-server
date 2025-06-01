using Newtonsoft.Json;

namespace NovitaModels;

class ModelsExtractor
{
    public NovitaExtractor[]? Models { get; set; }
}

class NovitaExtractor
{
    [JsonProperty("sd_name_in_api")] 
    public string? SdNameInApi { get; set; }

    [JsonProperty("cover_url")]
    public string? Cover
    {
        get;
        set;
    }

    [JsonProperty("type")]
    public ModelType? ModelType
    {
        get;
        set;
    }
    [JsonProperty("is_nsfw")]
    public bool IsNsfw
    {
        get;
        set;
    }
    [JsonProperty("is_sdxl")]
    public bool IsSdxl
    {
        get;
        set;
    }
}

public class ModelType
{
    [JsonProperty("name")]
    public string? Name
    {
        get;
        set;
    }
    [JsonProperty("display_name")]
    public string? DisplayName
    {
        get;
        set;
    }
}