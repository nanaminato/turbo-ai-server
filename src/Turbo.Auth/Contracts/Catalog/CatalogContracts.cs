using Turbo.Auth.Contracts.Common;
using System.ComponentModel.DataAnnotations;
using Turbo.Auth.Application.Providers;
using Turbo.Auth.Models.Suppliers;

namespace Turbo.Auth.Contracts.Catalog;

public sealed class ModelWriteRequest
{
    [Range(0, int.MaxValue)] public int ModelId { get; init; }
    [Required, StringLength(200)] public string Name { get; init; } = "";
    [Required, StringLength(200)] public string ModelValue { get; init; } = "";
    public bool IsChatModel { get; init; } = true;
    public bool Vision { get; init; } = true;
    public Model ToEntity() => new() { ModelId = ModelId, Name = Name, ModelValue = ModelValue, IsChatModel = IsChatModel, Vision = Vision };
}
public sealed record ModelResponse(int ModelId, string? Name, string? ModelValue, bool IsChatModel, bool Vision)
{
    public static ModelResponse From(Model model) => new(model.ModelId, model.Name, model.ModelValue, model.IsChatModel, model.Vision);
}
public sealed record PublicModelResponse(int ModelId, bool Enable, string? Name, string? ModelValue, bool IsChatModel, bool Vision);
public sealed class ModelStatusRequest
{
    [Range(1, int.MaxValue)] public int ModelId { get; init; }
    public bool Enable { get; init; }
}
public sealed class ModelBindingRequest
{
    [Range(0, int.MaxValue)] public int ModelKeyBindId { get; init; }
    [Range(1, int.MaxValue)] public int ModelId { get; init; }
    public bool Enable { get; init; }
    [Range(0, double.MaxValue)] public double Fee { get; init; }
    [Range(0d, 1000000d)] public double? RoutingWeight { get; init; }
    [StringLength(200)] public string ProviderModelValue { get; init; } = "";
    public int Priority { get; init; }
}
public sealed class KeyWriteRequest : IValidatableObject
{
    [Range(0, int.MaxValue)] public int SupplierKeyId { get; init; }
    [Required, StringLength(200), Url] public string BaseUrl { get; init; } = "";
    [Required, StringLength(200)] public string ApiKey { get; init; } = "";
    public int RequestIdentifier { get; init; }
    public bool Enable { get; init; }
    [NonNullItems] public List<ModelBindingRequest> ModelKeyBinds { get; init; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ModelKeyBinds.Where(binding => binding != null).GroupBy(binding => binding.ModelId).Any(group => group.Count() > 1))
            yield return new("Duplicate model bindings.", [nameof(ModelKeyBinds)]);
        if (!Enum.IsDefined((HandlerType)RequestIdentifier)) yield return new("Unknown provider.", [nameof(RequestIdentifier)]);
        if (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is not ("http" or "https"))
            yield return new("Use an HTTP or HTTPS provider URL.", [nameof(BaseUrl)]);

    }
    public SupplierKey ToEntity(int? routeId = null) => new()
    {
        SupplierKeyId = routeId ?? SupplierKeyId,
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        RequestIdentifier = RequestIdentifier,
        Enable = Enable,
        // Never bind nested Model/SupplierKey graphs or database relationship IDs supplied by clients.
        ModelKeyBinds = ModelKeyBinds.Select(binding => new ModelKeyBind
        {
            ModelId = binding.ModelId,
            Enable = binding.Enable,
            Fee = binding.Fee,
            RoutingWeight = binding.RoutingWeight,
            ProviderModelValue = binding.ProviderModelValue,
            Priority = binding.Priority
        }).ToList()
    };
}
public sealed record ModelBindingResponse(int ModelKeyBindId, int SupplierKeyId, int ModelId, bool Enable,
    double Fee, string ProviderModelValue, int Priority, ModelResponse? Model, double? RoutingWeight = null);
public sealed record KeyResponse(int SupplierKeyId, string? BaseUrl, string? ApiKey, int RequestIdentifier,
    bool Enable, IReadOnlyList<ModelBindingResponse> ModelKeyBinds)
{
    public override string ToString() => $"KeyResponse {{ SupplierKeyId = {SupplierKeyId}, RequestIdentifier = {RequestIdentifier} }}";
    public static KeyResponse From(SupplierKey key) => new(key.SupplierKeyId, key.BaseUrl, key.ApiKey,
        key.RequestIdentifier, key.Enable, (key.ModelKeyBinds ?? []).Select(binding => new ModelBindingResponse(
            binding.ModelKeyBindId, key.SupplierKeyId, binding.ModelId, binding.Enable, binding.Fee,
            binding.ProviderModelValue, binding.Priority, binding.Model is null ? null : ModelResponse.From(binding.Model), binding.RoutingWeight)).ToList());
}
public sealed record ProviderTypeResponse(string Type, int RequestIdentifier);
