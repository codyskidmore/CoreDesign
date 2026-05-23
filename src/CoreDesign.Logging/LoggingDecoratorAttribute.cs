namespace CoreDesign.Logging;

/// <summary>
/// Triggers generation of a logging decorator for the decorated interface.
/// The generated class is named <c>{InterfaceNameWithoutLeadingI}LoggingDecorator</c>
/// and is placed in the same namespace as the interface.
///
/// Register via the generated <c>services.DecorateWithLogging()</c> extension,
/// or explicitly with <c>LoggingDecoratorRegistration.Decorate&lt;IFoo, FooLoggingDecorator&gt;(services)</c>.
///
/// Use <see cref="SuppressAttribute"/> on a method to skip logging for that method.
/// Use <see cref="RedactAttribute"/> on a parameter to log <c>[REDACTED]</c> instead of the value.
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class LoggingDecoratorAttribute : Attribute { }
