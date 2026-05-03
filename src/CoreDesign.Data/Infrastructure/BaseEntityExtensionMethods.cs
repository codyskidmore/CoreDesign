namespace CoreDesign.Data.Infrastructure;

public static class BaseEntityExtensionMethods
{
    public static void InitializeAuditFields(this BaseEntity entity, Guid userId)
    {
        entity.Id = Ulid.NewUlid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.CreatedBy = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId;
    }

    public static void UpdateAuditFields(this BaseEntity entity, Guid userId)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId;
    }
}