namespace CoreDesign.Data.Infrastructure;

public static class ValueConverters
{
    public static EnumToStringConverter<TEnum> GetEnumConverter<TEnum>() where TEnum : struct, Enum
    {
        return new EnumToStringConverter<TEnum>();
    }

    public static ValueConverter<Ulid, string> GetUlidConverter()
    {
        return new ValueConverter<Ulid, string>(
            v => v.ToString(),
            v => Ulid.Parse(v));
    }

    // Providers that map DateTime to a timezone-aware column type (e.g. Npgsql's
    // "timestamp with time zone") reject DateTimeKind.Unspecified/Local on write. SQL Server's
    // datetime2 never validated Kind, so this only surfaces under stricter providers. Normalizing
    // to Utc on both write and read keeps BaseEntity's CreatedAt/UpdatedAt provider-agnostic.
    public static ValueConverter<DateTime, DateTime> GetUtcDateTimeConverter()
    {
        return new ValueConverter<DateTime, DateTime>(
            v => NormalizeToUtc(v),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }

    private static DateTime NormalizeToUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
    };
}