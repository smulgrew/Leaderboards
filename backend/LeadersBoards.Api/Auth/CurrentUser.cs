namespace LeadersBoards.Api.Auth;

/// <summary>
/// DEV-ONLY identity: the caller says who they are with an "X-User-Id" header
/// (get one from POST /dev/login). This keeps the skeleton runnable with zero setup.
/// Replace with real auth (JWT bearer from Supabase Auth / ASP.NET Identity) before
/// anyone else uses it. Only this file and DevAuthEndpoints need to change.
/// </summary>
public static class CurrentUser
{
    public const string Header = "X-User-Id";

    public static Guid? TryGetUserId(this HttpContext http) =>
        Guid.TryParse(http.Request.Headers[Header], out var id) ? id : null;

    /// <summary>Only call inside a group that uses RequireDevUser().</summary>
    public static Guid UserId(this HttpContext http) => http.TryGetUserId()!.Value;

    /// <summary>Endpoint filter: 401 unless the request carries a user id.</summary>
    public static RouteGroupBuilder RequireDevUser(this RouteGroupBuilder group) =>
        group.AddEndpointFilter(async (ctx, next) =>
            ctx.HttpContext.TryGetUserId() is null ? Results.Unauthorized() : await next(ctx));
}
