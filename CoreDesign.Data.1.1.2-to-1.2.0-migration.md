# CoreDesign.Data: Migrating from 1.1.2 to 1.2.0

## Overview

Version 1.2.0 moves UTC `DateTime` normalization from an opt-in, per-property setting (only `BaseEntity.CreatedAt`/`UpdatedAt`) to a model-wide EF Core convention registered in `CoreDesignDbContext`. Every `DateTime` and `DateTime?` property on every entity in your model is now normalized to UTC on write and stamped `Kind=Utc` on read, not just the two audit fields `BaseEntity` defines.

This is a breaking change if your application:

- Has its own entities with `DateTime`/`DateTime?` properties beyond `BaseEntity.CreatedAt`/`UpdatedAt`, and
- Those properties can receive values with `Kind=Local` or `Kind=Unspecified` (deserialized input, manually constructed `DateTime` values, values read from non-JSON sources).

If neither applies, for example your entities only use `BaseEntity`'s audit fields, there is nothing to change beyond the package version bump.

---

## Before You Begin

Update the package reference to 1.2.0:

```xml
<PackageReference Include="CoreDesign.Data" Version="1.2.0" />
```

Or via the CLI:

```
dotnet add package CoreDesign.Data --version 1.2.0
```

No EF Core migration is required. The conversion changes how .NET reads and writes the value at the driver boundary; it does not change the column's store type.

---

## Migration Steps

### Step 1: Confirm Your `DbContext` Derives from `CoreDesignDbContext`

The new convention is registered in `CoreDesignDbContext.ConfigureConventions`. If your context already derives from it, as required by the rest of CoreDesign.Data, no change is needed here:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : CoreDesignDbContext(options)
{
    // ...
}
```

If your `DbContext` overrides `ConfigureConventions` itself, make sure it calls `base.ConfigureConventions(configurationBuilder)`. Without that call, the new convention never registers and every `DateTime` property is left unmanaged, exactly the 1.1.2 behavior.

### Step 2: Remove Manual `DateTime.SpecifyKind`/`ToUniversalTime` Workarounds

If your application added a manual normalization step before saving, to work around the provider rejecting non-UTC `DateTime` values on a property outside `BaseEntity`, it is now redundant.

**Before (1.1.2)**

```csharp
entity.ScheduledFor = DateTime.SpecifyKind(input.ScheduledFor, DateTimeKind.Utc);
context.Add(entity);
await context.SaveChangesAsync();
```

**After (1.2.0)**

```csharp
entity.ScheduledFor = input.ScheduledFor;
context.Add(entity);
await context.SaveChangesAsync();
```

The convention normalizes `ScheduledFor` automatically, whatever `Kind` it arrives with.

### Step 3: Review Any Existing Explicit `.HasConversion(...)` on a `DateTime` Property

EF Core's explicit, per-property `.HasConversion(...)` always takes precedence over a model-wide convention. If an entity configuration already calls `.HasConversion(...)` on a `DateTime` property with different semantics (for example, storing local time deliberately), that configuration continues to work unmodified, the new convention only fills in properties that don't already have an explicit conversion. Remove the explicit call only if you want the new UTC-normalizing default instead.

### Step 4: Re-run Your Test Suite

Any test that asserts a specific `DateTimeKind` on a loaded entity, or that constructs `DateTime` values with `Kind=Local`/`Unspecified` and compares them directly against a loaded value, should be reviewed. After this change, a loaded `DateTime` value is always `Kind=Utc`; comparisons should be done in UTC or via `.ToUniversalTime()` on the expected value.

---

## Staying on 1.1.2 Behavior

There is no configuration flag to opt out of the model-wide convention. If your application genuinely needs a specific `DateTime` property to bypass UTC normalization, apply an explicit `.HasConversion(...)` to that property in its `IEntityTypeConfiguration<T>`, it will override the convention for that property only, leaving every other `DateTime` property on the model normalized.

---

## Troubleshooting

**A `DateTime` that used to round-trip as `Local`/`Unspecified` now comes back as `Utc`, and downstream logic assumed local time.**
This is the intended effect of the convention. Convert at the presentation boundary (for example, `TimeZoneInfo.ConvertTimeFromUtc` when rendering to a user) rather than relying on the stored value's `Kind` to already reflect a local zone.

**The convention doesn't seem to apply to one of my entities.**
Check whether that entity's configuration, or your `DbContext`, calls `.HasConversion(...)` on the property explicitly, explicit configuration always wins over the convention. Also confirm your `DbContext` calls `base.ConfigureConventions(...)` if it overrides the method.

**Tests using the EF Core in-memory provider behave differently than before.**
The in-memory provider applies value converters the same way a relational provider does, so behavior should be identical. If a test fails, it is most likely asserting a `Kind` that the convention now normalizes; update the assertion rather than the entity.
