namespace CoreDesign.Data.Infrastructure;

public class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(e => e.Id)
            .IsRequired()
            .HasConversion(ValueConverters.GetUlidConverter());
        builder.Property(e => e.Id).IsRequired();
        builder.HasIndex(e => e.Id);
        builder.HasQueryFilter(e => !e.IsDeleted);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.CreatedBy).IsRequired();
        builder.Property(e => e.UpdatedBy).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();
    }
}