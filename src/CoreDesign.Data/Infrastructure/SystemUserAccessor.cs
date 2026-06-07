namespace CoreDesign.Data.Infrastructure;

public class SystemUserAccessor : ICurrentUserAccessor
{
    public Guid UserId => Guid.Empty;
}
