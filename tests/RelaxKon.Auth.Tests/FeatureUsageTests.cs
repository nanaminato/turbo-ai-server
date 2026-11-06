using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RelaxKon.Auth.Contracts.Common;
using RelaxKon.Auth.Contracts.FeatureAccess;
using RelaxKon.Auth.Security.FeatureAccess;

namespace RelaxKon.Auth.Tests;

public class FeatureUsageTests
{
    private const long Now = 1793923805;

    [Test]
    public void Snapshot_uses_current_windows_and_never_reports_negative_remaining_after_policy_reduction()
    {
        var document = FeaturePolicies.Empty with { Revision = 3 };
        document.Features[0].Roles["user"] = new(true, 2, 4, 1);
        var result = FeatureUsageProjection.Create(Now, document, ["user"],
            [new("chat", Now / 60, Now / 86400, 3, 6, 2)]);
        var chat = result.Features.Single(feature => feature.Policy.Key == "chat");
        Assert.Multiple(() =>
        {
            Assert.That(result.PolicyRevision, Is.EqualTo(3));
            Assert.That(result.ObservedAt.ToUnixTimeSeconds(), Is.EqualTo(Now));
            Assert.That(chat.MinuteUsed, Is.EqualTo(3));
            Assert.That(chat.DayUsed, Is.EqualTo(6));
            Assert.That(chat.ActiveRequests, Is.EqualTo(2));
            Assert.That(chat.MinuteRemaining, Is.Zero);
            Assert.That(chat.DayRemaining, Is.Zero);
            Assert.That(chat.ConcurrentRemaining, Is.Zero);
        });
    }

    [Test]
    public void Stale_windows_reset_independently_without_clearing_active_leases()
    {
        var document = FeaturePolicies.Empty;
        document.Features[0].Roles["user"] = new(true, 10, 100, 3);
        var chat = FeatureUsageProjection.Create(Now, document, ["user"],
            [new("chat", Now / 60 - 1, Now / 86400, 9, 60, 1)]).Features[0];
        Assert.That(chat.MinuteUsed, Is.Zero);
        Assert.That(chat.MinuteRemaining, Is.EqualTo(10));
        Assert.That(chat.DayUsed, Is.EqualTo(60));
        Assert.That(chat.DayRemaining, Is.EqualTo(40));
        Assert.That(chat.ConcurrentRemaining, Is.EqualTo(2));
        chat = FeatureUsageProjection.Create(Now, document, ["user"],
            [new("chat", Now / 60 - 1, Now / 86400 - 1, 9, 60, 1)]).Features[0];
        Assert.That(chat.DayUsed, Is.Zero);
        Assert.That(chat.ActiveRequests, Is.EqualTo(1));
    }

    [Test]
    public void Disabled_features_have_zero_remaining_and_enabled_unlimited_features_have_null_remaining()
    {
        var document = FeaturePolicies.Empty;
        document.Features[0].Roles["user"] = new(true, 5, 100, 2);
        document.Features[0].Roles["vip"] = new(true);
        var result = FeatureUsageProjection.Create(Now, document, ["user", "vip", "admin"], []);
        Assert.That(result.Features, Has.Count.EqualTo(FeaturePolicies.Keys.Length));
        var chat = result.Features[0];
        Assert.That(chat.MinuteUsed, Is.Zero);
        Assert.That(chat.MinuteRemaining, Is.Null);
        Assert.That(chat.DayRemaining, Is.Null);
        Assert.That(chat.ConcurrentRemaining, Is.Null);
        var disabled = result.Features[1];
        Assert.That(disabled.Policy.Enabled, Is.False);
        Assert.That(disabled.MinuteRemaining, Is.Zero);
        Assert.That(disabled.DayRemaining, Is.Zero);
        Assert.That(disabled.ConcurrentRemaining, Is.Zero);
    }

    [Test]
    public void Reset_times_are_utc_fixed_window_boundaries_and_exact_boundary_starts_a_new_window()
    {
        var boundary = Now / 86400 * 86400;
        var snapshot = FeatureUsageProjection.Create(boundary, FeaturePolicies.Empty, [],
            [new("chat", boundary / 60 - 1, boundary / 86400 - 1, 20, 30, 0)]);
        Assert.That(snapshot.Features[0].MinuteUsed, Is.Zero);
        Assert.That(snapshot.Features[0].DayUsed, Is.Zero);
        Assert.That(snapshot.MinuteResetsAt.ToUnixTimeSeconds(), Is.EqualTo(boundary + 60));
        Assert.That(snapshot.DayResetsAt.ToUnixTimeSeconds(), Is.EqualTo(boundary + 86400));
        Assert.That(snapshot.ObservedAt.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task Controller_uses_authenticated_account_only_and_prevents_caching()
    {
        var reader = new Reader();
        var controller = Controller(reader, "7");
        controller.HttpContext.Request.QueryString = new QueryString("?accountId=99&userId=99");
        var response = (OkObjectResult)(await controller.Get(default)).Result!;
        Assert.That(reader.Account, Is.EqualTo(7));
        Assert.That(controller.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        var payload = (FeatureUsageResponse)response.Value!;
        Assert.That(payload.Features, Has.Count.EqualTo(8));
        Assert.That(payload.Features[0].MinuteResetsAt, Is.GreaterThan(payload.ObservedAt));
    }

    [TestCase(null)]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("not-an-id")]
    public async Task Controller_rejects_missing_or_invalid_identity_before_reading_storage(string? id)
    {
        var reader = new Reader();
        var controller = Controller(reader, id);
        Assert.That((await controller.Get(default)).Result, Is.TypeOf<UnauthorizedResult>());
        Assert.That(reader.Account, Is.Null);
    }

    [Test]
    public async Task Unavailable_store_returns_503_instead_of_inventing_zero_usage()
    {
        var controller = Controller(new Reader { Error = new InvalidOperationException("unavailable") }, "1");
        var response = (ObjectResult)(await controller.Get(default)).Result!;
        Assert.That(response.StatusCode, Is.EqualTo(503));
        Assert.That(((CodeResponse)response.Value!).Code, Is.EqualTo("feature_access_unavailable"));
    }

    [Test]
    public void Request_cancellation_is_not_reported_as_storage_failure()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var controller = Controller(new Reader { Error = new OperationCanceledException(source.Token) }, "1");
        Assert.ThrowsAsync<OperationCanceledException>(() => controller.Get(source.Token));
    }

    private static FeatureUsageController Controller(Reader reader, string? id)
    {
        var context = new DefaultHttpContext();
        if (id is not null) context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id)], "test"));
        return new(reader, NullLogger<FeatureUsageController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private sealed class Reader : IFeatureUsageReader
    {
        public int? Account { get; private set; }
        public Exception? Error { get; init; }
        public Task<FeatureUsageSnapshot> ReadUsage(int accountId, CancellationToken token)
        {
            Account = accountId;
            return Error is not null ? Task.FromException<FeatureUsageSnapshot>(Error)
                : Task.FromResult(FeatureUsageProjection.Create(Now, FeaturePolicies.Empty, [], []));
        }
    }
}
