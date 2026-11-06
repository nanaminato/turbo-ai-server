using RelaxKon.Auth.Contracts.Common;
using System.ComponentModel.DataAnnotations;
using RelaxKon.Auth.Security.FeatureAccess;
namespace RelaxKon.Auth.Contracts.FeatureAccess;

public sealed record FeatureGrantContract(bool Enabled = false,
    [Range(1, int.MaxValue)] int? RequestsPerMinute = null,
    [Range(1, int.MaxValue)] int? RequestsPerDay = null,
    [Range(1, int.MaxValue)] int? MaxConcurrent = null);
public sealed record FeaturePolicyContract([Required] string Key,
    [Required, NonNullItems] Dictionary<string, FeatureGrantContract> Roles);
public sealed record FeaturePolicyContractDocument([Range(0, long.MaxValue)] long Revision,
    [Required, NonNullItems] List<FeaturePolicyContract> Features)
{
    public FeaturePolicyDocument ToApplication() => new(Revision, Features.Select(feature => new FeaturePolicy(feature.Key,
        feature.Roles.ToDictionary(pair => pair.Key, pair => new FeatureGrant(pair.Value.Enabled, pair.Value.RequestsPerMinute,
            pair.Value.RequestsPerDay, pair.Value.MaxConcurrent)))).ToList());
    public static FeaturePolicyContractDocument From(FeaturePolicyDocument document) => new(document.Revision,
        document.Features.Select(feature => new FeaturePolicyContract(feature.Key, feature.Roles.ToDictionary(pair => pair.Key,
            pair => new FeatureGrantContract(pair.Value.Enabled, pair.Value.RequestsPerMinute, pair.Value.RequestsPerDay, pair.Value.MaxConcurrent)))).ToList());
}
public sealed record EffectiveFeatureResponse(string Key, bool Enabled, int? RequestsPerMinute, int? RequestsPerDay, int? MaxConcurrent)
{
    public static EffectiveFeatureResponse From(EffectiveFeature feature) => new(feature.Key, feature.Enabled,
        feature.RequestsPerMinute, feature.RequestsPerDay, feature.MaxConcurrent);
}
public sealed record FeatureAccessResponse(bool CanManagePolicies, IReadOnlyList<EffectiveFeatureResponse> Features);

public sealed record FeatureUsageItemResponse(string Key, bool Enabled,
    int? RequestsPerMinute, int? RequestsPerDay, int? MaxConcurrent,
    long MinuteUsed, long DayUsed, long ActiveRequests,
    long? MinuteRemaining, long? DayRemaining, long? ConcurrentRemaining,
    DateTimeOffset MinuteResetsAt, DateTimeOffset DayResetsAt);
public sealed record FeatureUsageResponse(DateTimeOffset ObservedAt, long PolicyRevision,
    IReadOnlyList<FeatureUsageItemResponse> Features)
{
    public static FeatureUsageResponse From(FeatureUsageSnapshot snapshot) => new(snapshot.ObservedAt,
        snapshot.PolicyRevision, snapshot.Features.Select(feature => new FeatureUsageItemResponse(
            feature.Policy.Key, feature.Policy.Enabled, feature.Policy.RequestsPerMinute,
            feature.Policy.RequestsPerDay, feature.Policy.MaxConcurrent, feature.MinuteUsed,
            feature.DayUsed, feature.ActiveRequests, feature.MinuteRemaining, feature.DayRemaining,
            feature.ConcurrentRemaining, snapshot.MinuteResetsAt, snapshot.DayResetsAt)).ToList());
}
