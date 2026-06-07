namespace Sample.Api.Infrastructure;

public class SampleCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid UserId
    {
        get
        {
            var oid = httpContextAccessor.HttpContext?.User.FindFirst("oid")?.Value;
            return Guid.TryParse(oid, out var id) ? id : Guid.Empty;
        }
    }
}
