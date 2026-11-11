# CoreDesign.Shared and result unions

Namespace for everything here: `CoreDesign.Shared.Infrastructure`.

## Result records

| Type | Meaning | Typical HTTP mapping |
|---|---|---|
| `Success` | Worked, nothing to return | `Results.Ok()` or `Results.NoContent()` |
| `NotFoundMessage(string Message)` | The resource does not exist | `Results.NotFound(error.Message)` |
| `BadRequestMessage(string Message)` | The input is invalid, or a write failed | `Results.BadRequest(error.Message)` |
| `ErrorMessage(string Message)` | A general failure, often from an external service | `Results.Problem(error.Message)` or 502/503 |
| `InvalidOperationMessage(string Message)` | Not allowed in the current state | `Results.Conflict(error.Message)` or 422 |

The logging generator logs a case at Warning when its type name contains NotFound, BadRequest, Error, Failure, Unauthorized, Forbidden, Conflict or InvalidOperation, so your own records named that way (`ConflictMessage`, `ForbiddenMessage`) get the same treatment automatically.

## Union types

`Union<T0, T1>`, `Union<T0, T1, T2>` and `Union<T0, T1, T2, T3>` are C# 15 unions.

```csharp
public async Task<Union<Order, NotFoundMessage, BadRequestMessage>> UpdateAsync(...)
{
    if (order is null) return new NotFoundMessage($"Order {id} not found.");
    if (!valid)        return new BadRequestMessage("Quantity must be positive.");
    return order;      // every case converts implicitly, including interfaces such as IEnumerable<T>
}
```

Handling, in order of preference:

```csharp
// 1. Switch expression: exhaustive, CS8509 if a case is missing.
return result switch
{
    Order order             => Results.Ok(Response.From(order)),
    NotFoundMessage nf      => Results.NotFound(nf.Message),
    BadRequestMessage bad   => Results.BadRequest(bad.Message),
};

// 2. A type test when only one case needs a side effect, before the switch.
if (result is Order) await cache.EvictByTagAsync(tag, ct);

// 3. Match, kept for code migrated from OneOf.
return result.Match(
    order => Results.Ok(order),
    nf    => Results.NotFound(nf.Message),
    bad   => Results.BadRequest(bad.Message));
```

`result.Value` returns the held case as `object?`.

### Behaviors that surprise people

- **`default` holds nothing.** `default(Union<A, B>)` has `Value == null`. A switch without a `null` arm throws `SwitchExpressionException`, and `Match` throws `InvalidOperationException`. Values returned from methods are always safe; uninitialized fields and array slots are not. Add `null => ...` where that can happen.
- **Prefer a `null` arm to `_`.** A discard arm hides cases added later from the exhaustiveness check.
- **`result is Union<A, B>` is error CS8121.** Test the case types, or `result is not null`.
- **Don't repeat a case type.** `Union<string, string>` compiles, but converting a string to it is ambiguous (CS0457).
- **Value-type cases are boxed.** Prefer `Success` to `bool`, and records to primitives.
- **Switch arms cannot contain statements.** Do side effects in an `if (result is X)` before the switch.

### More than four cases, or a named result

Declare a union of your own. CoreDesign.Logging handles it exactly like `Union<...>` because every compiler-generated union implements `System.Runtime.CompilerServices.IUnion`.

```csharp
public union CheckoutResult(Order, NotFoundMessage, BadRequestMessage, InvalidOperationMessage, ErrorMessage);
```

### Choosing cases

- Put the success value first; it reads naturally and matches the existing code base.
- Return `Union<IEnumerable<T>, NotFoundMessage>` only if "empty" is genuinely an error for the caller; otherwise return the list and let empty mean empty.
- Use `Union<Success, NotFoundMessage>` for deletes and commands with nothing to return.

## Configuration helpers

| Method | On | What it does |
|---|---|---|
| `AddAppSettings()` | `IHostApplicationBuilder`, `IDistributedApplicationBuilder` | Loads `appsettings.json` and `appsettings.{Environment}.json` (use it in AppHost projects too) |
| `AddDatabaseConfiguration()` | `IHostApplicationBuilder` | Binds the `DatabaseOptions` section |

`DatabaseOptions` has `HostName` (the Aspire resource name), `DatabaseName`, `HostPort` and `ConnectionStringName` (the user-secrets connection string key used for redirection). Read it with `builder.Configuration.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>()`.

## Utility extensions

| Method | Notes |
|---|---|
| `obj.ToJson()` | JSON, ignoring reference cycles |
| `obj.SaveToJsonFile(path)` / `path.LoadObjectFromJsonFile<T>()` | File round trip |
| `obj.DeepClone()` | JSON round trip; returns null for null; types need a usable constructor (`[JsonConstructor]` if not default) |
| `"42".To<int>()` | `TypeDescriptor` conversion; throws `ArgumentException` when unsupported |
| `s.PadRightWithZeros(width)` | Pads with `'0'` on the right |
