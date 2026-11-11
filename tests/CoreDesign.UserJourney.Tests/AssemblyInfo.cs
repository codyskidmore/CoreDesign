// OpenTelemetry listens to ActivitySources process-wide, so parallel tests would see each other's traces.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
