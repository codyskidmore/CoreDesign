namespace CoreDesign.ExceptionHandling;

/// <summary>
/// Default <see cref="IProblemDetailsMapper"/> registered by <c>AddCoreDesignExceptionHandling()</c>
/// so the package works with zero <see cref="ProblemMappingAttribute"/> usages. Superseded once
/// the generated <c>AddGeneratedProblemMappings()</c> extension is called, regardless of call order.
/// </summary>
internal sealed class NullProblemDetailsMapper : IProblemDetailsMapper
{
    public bool TryMap(Exception exception, out ProblemMappingResult result)
    {
        result = default;
        return false;
    }
}
