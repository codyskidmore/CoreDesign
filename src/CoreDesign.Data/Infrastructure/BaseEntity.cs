namespace CoreDesign.Data.Infrastructure;

public class BaseEntity
{
    public Ulid Id { get; set; } = Ulid.NewUlid();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}