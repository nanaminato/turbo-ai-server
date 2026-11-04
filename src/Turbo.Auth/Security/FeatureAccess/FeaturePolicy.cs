namespace Turbo.Auth.Security.FeatureAccess;

public sealed record FeatureGrant(bool Enabled = false, int? RequestsPerMinute = null,
    int? RequestsPerDay = null, int? MaxConcurrent = null);
public sealed record FeaturePolicy(string Key, Dictionary<string, FeatureGrant> Roles);
public sealed record FeaturePolicyDocument(long Revision, List<FeaturePolicy> Features);
public sealed record EffectiveFeature(string Key, bool Enabled, int? RequestsPerMinute,
    int? RequestsPerDay, int? MaxConcurrent);

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class FeatureAccessAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}

public static class FeaturePolicies
{
    public static readonly string[] Keys = ["chat", "image", "tts", "transcription", "translation", "file-extraction", "image-task", "image-proxy"];
    public static FeaturePolicyDocument Empty => new(0, Keys.Select(key => new FeaturePolicy(key, new())).ToList());
    public static EffectiveFeature Resolve(FeaturePolicyDocument document, string key, IEnumerable<string> roles)
    {
        var policy = document.Features.SingleOrDefault(feature => feature.Key == key);
        var grants = roles.Distinct(StringComparer.Ordinal).Select(role =>
            policy?.Roles.GetValueOrDefault(role)).Where(grant => grant?.Enabled == true).Cast<FeatureGrant>().ToArray();
        int? Limit(Func<FeatureGrant, int?> select) => grants.Any(grant => select(grant) is null)
            ? null : grants.Select(select).Max();
        return grants.Length == 0 ? new(key, false, null, null, null)
            : new(key, true, Limit(g => g.RequestsPerMinute), Limit(g => g.RequestsPerDay), Limit(g => g.MaxConcurrent));
    }
    public static bool IsValid(FeaturePolicyDocument document, ISet<string> roleNames) =>
        document.Features is not null && document.Features.Count == Keys.Length && document.Features.All(f => f is not null) &&
        document.Features.Select(f => f.Key).Distinct().Count() == Keys.Length &&
        document.Features.All(f => Keys.Contains(f.Key) && f.Roles is not null &&
            f.Roles.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == f.Roles.Count &&
            f.Roles.All(pair => roleNames.Contains(pair.Key) && pair.Value is not null &&
                pair.Value.RequestsPerMinute is null or > 0 && pair.Value.RequestsPerDay is null or > 0 &&
                pair.Value.MaxConcurrent is null or > 0));
}
