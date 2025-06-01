using Newtonsoft.Json;

namespace NovitaModels;

public class NovitaController
{
    private const string Key = "your novita ai key";
    public async Task<List<NovitaModel>?> GetVaeModels()
    {
        var client = new HttpClient();
        var query = new NovitaQuery()
        {
            Source = NovitaQuery.Civitai,
            Types = NovitaQuery.Vae,
            Limit = 20
        };
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.novita.ai/v3/model?{query.GetQuery()}");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var response = await client.SendAsync(request);
        var res = await response.Content.ReadAsStringAsync();
        // Console.WriteLine(res);
        var vaes = JsonConvert.DeserializeObject<ModelsExtractor>(res);
        return vaes!.Models!.Select(c => new NovitaModel() { Model = c.SdNameInApi,Cover = c.Cover,Nsfw = c.IsNsfw,Sdxl = c.IsNsfw,Type = c.ModelType!.Name}).ToList();
    }

    public async Task<List<NovitaModel>?> GetImageModels()
    {
        var client = new HttpClient();
        var query = new NovitaQuery()
        {
            Source = NovitaQuery.Civitai,
            Types = NovitaQuery.Checkpoint,
            Limit = 100
        };
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.novita.ai/v3/model?{query.GetQuery()}");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var response = await client.SendAsync(request);
        var res = await response.Content.ReadAsStringAsync();
        // Console.WriteLine(res);
        var models = JsonConvert.DeserializeObject<ModelsExtractor>(res);
        return models!.Models!.Select(c => new NovitaModel() { Model = c.SdNameInApi,Cover = c.Cover,Nsfw = c.IsNsfw,Sdxl = c.IsNsfw,Type = c.ModelType!.Name}).ToList();
    }

    public async Task<List<NovitaModel>?> GetLoras()
    {
        var client = new HttpClient();
        var query = new NovitaQuery()
        {
            Source = NovitaQuery.Civitai,
            Types = NovitaQuery.Lora,
            Limit = 100
        };
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.novita.ai/v3/model?{query.GetQuery()}");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var response = await client.SendAsync(request);
        // response.EnsureSuccessStatusCode();
        var res = await response.Content.ReadAsStringAsync();
        // Console.WriteLine(res);
        var loras = JsonConvert.DeserializeObject<ModelsExtractor>(res);
        return loras!.Models!.Select(c => new NovitaModel() { Model = c.SdNameInApi,Cover = c.Cover,Nsfw = c.IsNsfw,Sdxl = c.IsNsfw,Type = c.ModelType!.Name}).ToList();
    }
    
    public async Task<List<NovitaModel>?> GetEmbeddings()
    {
        var client = new HttpClient();
        var query = new NovitaQuery()
        {
            Source = NovitaQuery.Civitai,
            Types = NovitaQuery.Textualinversion,
            Limit = 100
        };
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.novita.ai/v3/model?{query.GetQuery()}");
        request.Headers.Add("Authorization", $"Bearer {Key}");
        var response = await client.SendAsync(request);
        // response.EnsureSuccessStatusCode();
        var res = await response.Content.ReadAsStringAsync();
        // Console.WriteLine(res);
        var embeddings = JsonConvert.DeserializeObject<ModelsExtractor>(res);
        return embeddings!.Models!.Select(c => new NovitaModel() { Model = c.SdNameInApi,Cover = c.Cover,Nsfw = c.IsNsfw,Sdxl = c.IsNsfw,Type = c.ModelType!.Name}).ToList();
    }
}
public class Novita
{
    [JsonProperty("model_name")]
    public string? ModelName
    {
        get;
        set;
    }
}
public class NovitaQuery
{
    public const string Open = "public";
    public const string Close = "private";
    public const string Civitai = "civitai";
    public const string Training = "training";
    public const string Uploading = "uploading";
    public const string Checkpoint = "checkpoint";
    public const string Lora = "lora";
    public const string Vae = "vae";
    public const string Controlnet = "controlnet";
    public const string Upscaler = "upscaler";
    public const string Textualinversion = "textualinversion";

    public string? Visibility { get; set; } = Open;

    public string? Source { get; set; } = Civitai;

    public string? Types { get; set; } = Checkpoint;

    public int Limit { get; set; } = 10;

    public string? Cursor { get; set; } = "c_0";

    public string? GetQuery()
    {
        return
            $"filter.visibility={Visibility}&filter.types={Types}&filter.source={Source}&pagination.limit={Limit}&pagination.cursor={Cursor}";
    }
}