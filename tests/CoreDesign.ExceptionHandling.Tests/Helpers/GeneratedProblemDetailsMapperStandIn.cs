namespace CoreDesign.ExceptionHandling.Tests.Helpers;

// Hand-written mapper matching the dispatch table pattern produced by ProblemMappingGenerator for
// the attribute usages below. This exists so the test project can verify dispatch behavior without
// referencing the generator as an analyzer, mirroring how CoreDesign.Logging.Tests exercises its
// decorator logic without the LoggingDecoratorGenerator wired in.
//
// Equivalent source attributes:
//   [ProblemMapping(400, Title = "Domain rule violated")]                       DomainException
//   [ProblemMapping(404, Title = "Not found")]                                  EntityNotFoundException
//   [ProblemMapping(500, Title = "Secret", IncludeMessage = false)]             SecretException
//   [ProblemMapping(422, Title = "Exact only", MatchDerived = false)]           ExactOnlyException
//
// Cases are ordered most-derived first (EntityNotFoundException before its base DomainException),
// exactly as ProblemMappingGenerator orders by inheritance depth.
public sealed class GeneratedProblemDetailsMapperStandIn : IProblemDetailsMapper
{
    public bool TryMap(Exception exception, out ProblemMappingResult result)
    {
        switch (exception)
        {
            case EntityNotFoundException e:
                result = new ProblemMappingResult(404, "Not found", e.Message, null);
                return true;
            case DomainException e:
                result = new ProblemMappingResult(400, "Domain rule violated", e.Message, null);
                return true;
            case SecretException:
                result = new ProblemMappingResult(500, "Secret", null, null);
                return true;
            case ExactOnlyException e when e.GetType() == typeof(ExactOnlyException):
                result = new ProblemMappingResult(422, "Exact only", e.Message, null);
                return true;
            default:
                result = default;
                return false;
        }
    }
}
