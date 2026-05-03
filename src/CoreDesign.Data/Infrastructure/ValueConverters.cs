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
}