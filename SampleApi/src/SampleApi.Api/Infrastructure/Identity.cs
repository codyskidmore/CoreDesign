namespace SampleApi.Api.Infrastructure;

public static class Identity
{
    public static Guid GetUserId(this HttpContext context)
    {
        var oid = context.User.FindFirst("oid")?.Value;

        if (Guid.TryParse(oid, out var parsedId))
            return parsedId;

        throw new InvalidOperationException("User ID (oid) claim not found or invalid");
    }
}
