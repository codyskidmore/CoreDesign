namespace CoreDesign.Shared.Infrastructure;

// General-purpose C# 15 unions for service results, e.g. Union<Cabin, NotFoundMessage>.
// Prefer switch expressions over Match: the compiler checks them for exhaustiveness (CS8509).
// Match exists so code migrating from OneOf<T> keeps compiling. Add a null arm to switches
// that can receive default(Union<...>), whose contents are null.

/// <summary>A value that is exactly one of two case types.</summary>
public union Union<T0, T1>(T0, T1)
{
    /// <summary>Runs the function for the case this union holds and returns its result.</summary>
    /// <exception cref="InvalidOperationException">The union is <c>default</c> and holds no value.</exception>
    public TResult Match<TResult>(Func<T0, TResult> f0, Func<T1, TResult> f1) => this switch
    {
        T0 v0 => f0(v0),
        T1 v1 => f1(v1),
        null => throw new InvalidOperationException("The union holds no value."),
    };
}

/// <summary>A value that is exactly one of three case types.</summary>
public union Union<T0, T1, T2>(T0, T1, T2)
{
    /// <summary>Runs the function for the case this union holds and returns its result.</summary>
    /// <exception cref="InvalidOperationException">The union is <c>default</c> and holds no value.</exception>
    public TResult Match<TResult>(Func<T0, TResult> f0, Func<T1, TResult> f1, Func<T2, TResult> f2) => this switch
    {
        T0 v0 => f0(v0),
        T1 v1 => f1(v1),
        T2 v2 => f2(v2),
        null => throw new InvalidOperationException("The union holds no value."),
    };
}

/// <summary>A value that is exactly one of four case types.</summary>
public union Union<T0, T1, T2, T3>(T0, T1, T2, T3)
{
    /// <summary>Runs the function for the case this union holds and returns its result.</summary>
    /// <exception cref="InvalidOperationException">The union is <c>default</c> and holds no value.</exception>
    public TResult Match<TResult>(
        Func<T0, TResult> f0, Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3) => this switch
    {
        T0 v0 => f0(v0),
        T1 v1 => f1(v1),
        T2 v2 => f2(v2),
        T3 v3 => f3(v3),
        null => throw new InvalidOperationException("The union holds no value."),
    };
}
