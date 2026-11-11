namespace CoreDesign.Data.Infrastructure;

// Providers that map DateTime to a timezone-aware column type (e.g. Npgsql's "timestamp with
// time zone") reject DateTimeKind.Unspecified/Local on write. SQL Server's datetime2 never
// validated Kind, so this only surfaced under stricter providers. Registered globally via
// CoreDesignDbContext.ConfigureConventions so every DateTime property gets this normalization
// without each entity configuration having to opt in.
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(v => NormalizeToUtc(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }

    internal static DateTime NormalizeToUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
    };
}

public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter() : base(
        v => v.HasValue ? UtcDateTimeConverter.NormalizeToUtc(v.Value) : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
