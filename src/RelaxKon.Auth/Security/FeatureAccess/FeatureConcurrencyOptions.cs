namespace RelaxKon.Auth.Security.FeatureAccess;

public sealed class FeatureConcurrencyOptions
{
    public const string Section = "FeatureConcurrency";
    public int? MaxConcurrentPerAccount { get; init; }
    public int? MaxConcurrentPerSourceIp { get; init; }

    public bool IsValid() => MaxConcurrentPerAccount is null or > 0 && MaxConcurrentPerSourceIp is null or > 0;
}
