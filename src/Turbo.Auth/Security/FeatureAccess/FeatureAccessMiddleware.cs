using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Turbo.Auth.Data.Contexts;

namespace Turbo.Auth.Security.FeatureAccess;

public sealed class FeatureIdentity(AuthContext database)
{
    public async Task<string[]> Roles(int accountId, CancellationToken token) => await database.AccountRoles!
        .AsNoTracking().Where(link => link.AccountId == accountId && link.Role != null && link.Role.Name != null)
        .Select(link => link.Role!.Name!).Distinct().ToArrayAsync(token);
    public static int? AccountId(ClaimsPrincipal principal) => principal.Identity?.IsAuthenticated == true &&
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : null;
}

public sealed class FeatureAccessMiddleware(RequestDelegate next, ILogger<FeatureAccessMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, FeatureIdentity identity, FeatureAccessStore store)
    {
        var feature = context.GetEndpoint()?.Metadata.GetMetadata<FeatureAccessAttribute>();
        if (feature is null) { await next(context); return; }
        var account = FeatureIdentity.AccountId(context.User);
        if (account is null) { await Error(context, 401, "authentication_required"); return; }
        Admission admission;
        try
        {
            var roles = await identity.Roles(account.Value, context.RequestAborted);
            var policy = FeaturePolicies.Resolve(await store.Read(context.RequestAborted), feature.Key, roles);
            if (!policy.Enabled) { await Error(context, 403, "feature_disabled", feature.Key); return; }
            admission = await store.Acquire(account.Value, policy, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Feature admission unavailable for {Feature}", feature.Key);
            await Error(context, 503, "feature_access_unavailable", feature.Key); return;
        }
        if (admission.LeaseId is null)
        {
            context.Response.Headers.RetryAfter = admission.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Error(context, 429, "feature_limit_exceeded", feature.Key); return;
        }
        using var heartbeatStop = new CancellationTokenSource();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        var originalToken = context.RequestAborted;
        context.RequestAborted = request.Token;
        var heartbeat = Heartbeat(store, admission.LeaseId, heartbeatStop.Token, request, context);
        try { await next(context); }
        finally
        {
            heartbeatStop.Cancel();
            await heartbeat;
            context.RequestAborted = originalToken;
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await store.Release(admission.LeaseId, cleanup.Token);
            }
            catch (Exception exception) { logger.LogWarning(exception, "Feature lease cleanup deferred to expiry"); }
        }
    }
    private async Task Heartbeat(FeatureAccessStore store, string id, CancellationToken stop,
        CancellationTokenSource request, HttpContext context)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
            while (await timer.WaitForNextTickAsync(stop))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                if (!await store.Renew(id, timeout.Token)) throw new InvalidOperationException("Feature lease expired.");
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Feature lease heartbeat failed; aborting request");
            request.Cancel();
            context.Abort();
        }
    }
    private static Task Error(HttpContext context, int status, string code, string? feature = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { code, feature, status }, context.RequestAborted);
    }
}
