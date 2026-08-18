namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_TELEMETRY: the one place the Application Insights connection string lives.
    ///
    /// TO ENABLE TELEMETRY:
    ///   1. Azure Portal → Create resource → Application Insights (Workspace-based).
    ///   2. On the resource Overview blade, copy the CONNECTION STRING (not the old
    ///      instrumentation key — that form is deprecated and will stop working).
    ///   3. Paste it into ConnectionString below and rebuild.
    ///
    /// While this is empty the entire telemetry layer is inert: TelemetryService.IsEnabled
    /// returns false, no client is constructed, no first-run notice is shown, and every
    /// Track* call is a no-op. That is the deliberate default so a build can never start
    /// phoning home just because someone forgot a step.
    ///
    /// This string is not a secret in the credential sense — it only authorises WRITING
    /// telemetry to the resource, never reading it — but it does ship inside the package,
    /// so treat it as public. Do not reuse a resource that holds anything else.
    /// </summary>
    public static class TelemetryConfig
    {
        public const string ConnectionString =
            "InstrumentationKey=78fa93f5-36f4-400d-a3b9-0385715d5d80;" +
            "IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;" +
            "LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;" +
            "ApplicationId=613e85d7-b157-4788-85fd-c4ab8a559671";

        /// <summary>
        /// Schema version stamped on every event. Bump this when the meaning of an
        /// existing property changes, so old and new rows are never silently averaged
        /// together in a query — the same discipline as ClickMetricVersion.
        /// </summary>
        public const string SchemaVersion = "1";
    }
}
