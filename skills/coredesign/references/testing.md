# Testing CoreDesign handlers and endpoints

The CoreDesign style keeps handlers free of HTTP and logging concerns, so most tests are plain unit tests of a handler with mocked repositories. The usual stack is xUnit, Moq and Bogus.

## Handler tests

Test files need `using System.Linq.Expressions;`, `using Moq;`, `using CoreDesign.Data.Interfaces;` and `using CoreDesign.Shared.Infrastructure;`, plus the feature namespaces.

```csharp
public class CreateWidgetHandlerTests
{
    private readonly Mock<IReadRepository<MyAppDbContext, Widget>> _read = new();
    private readonly Mock<ICudRepository<MyAppDbContext, Widget>> _cud = new();
    private CreateWidgetHandler Handler => new(_read.Object, _cud.Object);

    [Fact]
    public async Task CreateAsync_WhenInsertSucceeds_ReturnsWidget()
    {
        _cud.Setup(r => r.InsertAsync(It.IsAny<Widget>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Handler.CreateAsync(new Request("Sprocket", 9.99m), CancellationToken.None);

        var widget = Assert.IsType<Widget>(result.Value);
        Assert.Equal("Sprocket", widget.Name);
    }

    [Fact]
    public async Task CreateAsync_WhenInsertFails_ReturnsBadRequest()
    {
        _cud.Setup(r => r.InsertAsync(It.IsAny<Widget>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await Handler.CreateAsync(new Request("Sprocket", 9.99m), CancellationToken.None);

        Assert.True(result is BadRequestMessage);
    }

    [Fact]
    public async Task CreateAsync_WhenNameExists_ReturnsInvalidOperation()
    {
        _read.Setup(r => r.GetAsync(
                It.IsAny<Expression<Func<Widget, bool>>>(),
                It.IsAny<Func<IQueryable<Widget>, IQueryable<Widget>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Widget { Name = "Sprocket" });

        var result = await Handler.CreateAsync(new Request("Sprocket", 9.99m), CancellationToken.None);

        Assert.True(result is InvalidOperationMessage);
        _cud.Verify(r => r.InsertAsync(It.IsAny<Widget>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

Asserting on unions:

- `Assert.True(result is NotFoundMessage)` for the case.
- `var x = Assert.IsType<Widget>(result.Value);` to get the value and assert on it.
- `Assert.True(result is NotFoundMessage { Message: var m } && m.Contains("not found"))` for the message.
- Never `Assert.IsType<Union<...>>(...)`; a union is not a case.

Mocking methods that return unions: Moq's `.ReturnsAsync(new NotFoundMessage("..."))` works against a `Task<Union<...>>` return type through the implicit conversion. For a list case typed as an interface, return the concrete list (`ReturnsAsync(new List<Widget> { ... })` for `Union<IEnumerable<Widget>, NotFoundMessage>` may need an explicit `(Union<IEnumerable<Widget>, NotFoundMessage>)` cast when type inference fails).

## Repository reads in tests

`IReadRepository.GetAsync` takes an expression; match any expression and return the entity:

```csharp
_read.Setup(r => r.GetAsync(
        It.IsAny<Expression<Func<Widget, bool>>>(),
        It.IsAny<Func<IQueryable<Widget>, IQueryable<Widget>>?>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(widget);
```

When the expression matters (several entities), compile it against a list: `.ReturnsAsync((Expression<Func<Widget, bool>> e, Func<IQueryable<Widget>, IQueryable<Widget>>? _, CancellationToken _) => widgets.AsQueryable().FirstOrDefault(e))`.

## Endpoint tests

Endpoint lambdas are thin, so test the switch mapping by calling the handler mock and checking the `IResult` type (`Ok<T>`, `NotFound<string>`, `BadRequest<string>`) when endpoints are written as named static methods. Use `DefaultHttpContext` with a `ClaimsPrincipal` carrying `oid` and `permissions` claims for code that reads the user.

## Data and integration tests

- The EF Core in-memory provider works for repository-level tests; `PurgeBeforeSeed` and seeding fall back to load-and-remove there. Register `SystemUserAccessor` and the `AuditInterceptor`.
- Unique indexes, schemas and real SQL behavior need the real provider (a PostgreSQL or SQL Server container).
- Logging decorators do not need tests in each project; they are generated. If a test needs to prove a call is logged, resolve the interface from a service collection after `DecorateWithLogging()` and use a fake `ILogger`.

## Migrating OneOf-style assertions

Code that used `result.IsT0` and `result.AsT0` can switch to the patterns above. For a large test suite, a test-only C# 14 extension block can keep the old shape while production code moves to unions:

```csharp
public static class UnionTestExtensions
{
    extension<T0, T1>(Union<T0, T1> union)
    {
        public bool IsT0 => union is T0;
        public bool IsT1 => union is T1;
        public T0 AsT0 => union is T0 v ? v : throw new InvalidOperationException($"Union holds {union.Value?.GetType().Name ?? "no value"}.");
        public T1 AsT1 => union is T1 v ? v : throw new InvalidOperationException($"Union holds {union.Value?.GetType().Name ?? "no value"}.");
    }
}
```

Keep it in the test project only.
