using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Authorization;
using RelaxKon.Auth.Controllers.Proxy;
using RelaxKon.Auth.Security.FeatureAccess;

namespace RelaxKon.Auth.Tests;

public class FeatureAccessTests
{
    [Test]
    public void Disabled_unlimited_grants_do_not_relax_enabled_role_limits()
    {
        var document = FeaturePolicies.Empty;
        var roles = document.Features.Single(f => f.Key == "chat").Roles;
        roles.Add("user", new(true, 2, 10, 1));
        roles.Add("vip", new(false));
        var result = FeaturePolicies.Resolve(document, "chat", ["user", "user", "vip", "missing"]);
        Assert.That(result, Is.EqualTo(new EffectiveFeature("chat", true, 2, 10, 1)));
        Assert.That(FeaturePolicies.Resolve(document, "chat", ["USER"]).Enabled, Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Rejects_invalid_daily_and_concurrency_limits_even_for_disabled_grants(int limit)
    {
        var document = FeaturePolicies.Empty;
        var roles = document.Features[0].Roles;
        roles["user"] = new(false, RequestsPerDay: limit);
        Assert.That(FeaturePolicies.IsValid(document, new HashSet<string> { "user" }), Is.False);
        roles["user"] = new(false, MaxConcurrent: limit);
        Assert.That(FeaturePolicies.IsValid(document, new HashSet<string> { "user" }), Is.False);
    }

    [Test]
    public void Rejects_duplicate_features_case_colliding_roles_and_null_grants()
    {
        var roleNames = new HashSet<string> { "user", "USER" };
        var document = FeaturePolicies.Empty;
        document.Features[1] = document.Features[0];
        Assert.That(FeaturePolicies.IsValid(document, roleNames), Is.False);
        document = FeaturePolicies.Empty;
        document.Features[0].Roles.Add("user", new(true));
        document.Features[0].Roles.Add("USER", new(true));
        Assert.That(FeaturePolicies.IsValid(document, roleNames), Is.False);
        document.Features[0].Roles.Remove("USER");
        document.Features[0].Roles["user"] = null!;
        Assert.That(FeaturePolicies.IsValid(document, roleNames), Is.False);
    }

    [Test]
    public void Accepts_unlimited_and_maximum_integer_limits_and_returns_independent_empty_documents()
    {
        var document = FeaturePolicies.Empty;
        document.Features[0].Roles.Add("user", new(true, int.MaxValue, null, int.MaxValue));
        Assert.That(FeaturePolicies.IsValid(document, new HashSet<string> { "user" }), Is.True);
        Assert.That(FeaturePolicies.Empty.Features.All(feature => feature.Roles.Count == 0), Is.True);
    }

    [Test]
    public void Every_compute_action_has_feature_and_authentication_metadata()
    {
        var assembly = typeof(ImageProxyController).Assembly;
        var controllers = assembly.GetTypes().Where(type => type.Name.EndsWith("Controller") &&
            (type.Namespace?.StartsWith("RelaxKon.Auth.Controllers.AI") == true ||
             type.Name is "FileExtractorController" or "ImageProxyController"));
        foreach (var type in controllers)
        foreach (var method in type.GetMethods().Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any()))
        {
            // Retired HTTP 410 actions never call a provider and need authentication, not feature quota.
            if (method.GetCustomAttributes<Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute>().Any(attribute => attribute.StatusCode == 410))
            {
                Assert.That(method.GetCustomAttributes<AuthorizeAttribute>().Any() || type.GetCustomAttributes<AuthorizeAttribute>().Any(), Is.True);
                Assert.That(method.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Null);
                continue;
            }
            if (type.Name == "APIMartGPTImageController" && method.Name == "Recover")
            {
                Assert.That(type.GetCustomAttributes<AuthorizeAttribute>().Any(), Is.True);
                continue; // Receipt registration only; signed token verified, no provider call.
            }
            if (method.Name == "GetChatModels") continue; // Authenticated discovery, no provider call.
            var feature = method.GetCustomAttribute<FeatureAccessAttribute>() ?? type.GetCustomAttribute<FeatureAccessAttribute>();
            Assert.That(feature, Is.Not.Null, $"{type.Name}.{method.Name}");
            Assert.That(FeaturePolicies.Keys, Does.Contain(feature!.Key));
            Assert.That(method.GetCustomAttributes<AuthorizeAttribute>().Any() || type.GetCustomAttributes<AuthorizeAttribute>().Any(), Is.True);
            Assert.That(method.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Null);
        }
    }
    [Test]
    public void Null_policy_entries_are_invalid()
    {
        var document = FeaturePolicies.Empty;
        document.Features[0] = null!;
        Assert.That(FeaturePolicies.IsValid(document, new HashSet<string>()), Is.False);
    }
    [Test]
    public void Missing_policy_or_role_denies_even_admin()
    {
        Assert.That(FeaturePolicies.Resolve(FeaturePolicies.Empty, "chat", ["admin"]).Enabled, Is.False);
        Assert.That(FeaturePolicies.Resolve(FeaturePolicies.Empty, "not-real", ["vip"]).Enabled, Is.False);
    }
    [Test]
    public void Enabled_roles_union_uses_most_permissive_limits_without_admin_bypass()
    {
        var document = FeaturePolicies.Empty;
        document.Features.Single(f => f.Key == "chat").Roles.Add("user", new(true, 2, 10, 1));
        document.Features.Single(f => f.Key == "chat").Roles.Add("vip", new(true, 5, null, 3));
        document.Features.Single(f => f.Key == "chat").Roles.Add("admin", new(false));
        var result = FeaturePolicies.Resolve(document, "chat", ["user", "vip", "admin"]);
        Assert.Multiple(() =>
        {
            Assert.That(result.Enabled, Is.True);
            Assert.That(result.RequestsPerMinute, Is.EqualTo(5));
            Assert.That(result.RequestsPerDay, Is.Null);
            Assert.That(result.MaxConcurrent, Is.EqualTo(3));
            Assert.That(FeaturePolicies.Resolve(document, "chat", ["admin"]).Enabled, Is.False);
        });
    }
    [TestCase(0)]
    [TestCase(-1)]
    public void Rejects_invalid_limits(int limit)
    {
        var document = FeaturePolicies.Empty;
        document.Features[0].Roles.Add("user", new(true, limit));
        Assert.That(FeaturePolicies.IsValid(document, new HashSet<string> { "user" }), Is.False);
    }
    [Test]
    public void Rejects_unknown_features_and_roles()
    {
        var document = FeaturePolicies.Empty;
        document.Features[0].Roles.Add("unknown", new(true));
        Assert.That(FeaturePolicies.IsValid(document, new HashSet<string> { "user" }), Is.False);
        Assert.That(FeaturePolicies.IsValid(document with { Features = [] }, new HashSet<string>()), Is.False);
    }
    [TestCase("127.0.0.1", false)]
    [TestCase("10.1.2.3", false)]
    [TestCase("169.254.169.254", false)]
    [TestCase("172.16.0.1", false)]
    [TestCase("192.168.1.1", false)]
    [TestCase("100.64.0.1", false)]
    [TestCase("::1", false)]
    [TestCase("::ffff:127.0.0.1", false)]
    [TestCase("fc00::1", false)]
    [TestCase("fe80::1", false)]
    [TestCase("2001:db8::1", false)]
    [TestCase("8.8.8.8", true)]
    [TestCase("2606:4700:4700::1111", true)]
    public void Image_proxy_rejects_private_and_special_addresses(string address, bool allowed) =>
        Assert.That(ImageProxyController.IsPublicAddress(IPAddress.Parse(address)), Is.EqualTo(allowed));
}
