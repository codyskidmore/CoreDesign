# LoggingMiddleware vs Serilog and Serilog.Enrichers.Sensitive

These operate at fundamentally different layers and are not alternatives to each other.

## Serilog

Serilog is a logging pipeline. It handles formatting, enrichment, filtering, and routing entries to sinks (files, Seq, Application Insights, etc.). It has no concept of your service layer and no mechanism to intercept method calls. It is the "how and where" of log output.

## Serilog.Enrichers.Sensitive

Serilog.Enrichers.Sensitive works inside that pipeline. It scans already-formed log messages for patterns (regex or property names) and masks matches before they reach a sink. This is a defensive, catch-all approach. It does not know what triggered the log entry or where in your application it came from. It also requires every sensitive value to be anticipated in a pattern list, and pattern-matching against arbitrary serialized objects is fragile.

## LoggingMiddleware

LoggingMiddleware operates one layer up, at the point where your application code calls your services. It intercepts every method invocation before it happens, which gives it three things the others cannot offer:

**Structural awareness.** It knows the method name, the parameter names, and the call site. `[SensitiveParameter]` is declared on the interface alongside the parameter it protects, which is the most precise place to express that intent.

**Intentional suppression.** `[NoLog]` removes a method from logging entirely. A regex enricher cannot suppress a log entry that was already written; it can only mask values within it.

**Zero instrumentation in service classes.** Services contain no log statements and no awareness of observability at all. The proxy handles it uniformly.

## How They Work Together

The right mental model is that these are complementary:

| Layer | Tool | Responsibility |
|---|---|---|
| Service boundary | LoggingMiddleware | Intercepts calls, logs method invocations and results |
| Logging pipeline | Serilog | Routes and formats entries to sinks |
| Pipeline safety net | Serilog.Enrichers.Sensitive | Masks sensitive values that slip through from code outside your control |

Using all three together gives you intentional logging at the service layer, flexible output routing, and a defensive backstop for third-party libraries, framework internals, and ad-hoc log statements that fall outside the proxy.
