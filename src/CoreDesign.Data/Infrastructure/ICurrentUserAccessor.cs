namespace CoreDesign.Data.Infrastructure;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
}
