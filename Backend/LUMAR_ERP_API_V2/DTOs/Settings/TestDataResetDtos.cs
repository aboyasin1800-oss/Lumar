using System.Text.Json.Serialization;

namespace LUMAR_ERP_API_V2.DTOs.Settings;

public sealed record TestDataResetExecutionResultDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("databaseName")] string DatabaseName,
    [property: JsonPropertyName("details")] string? Details = null,
    [property: JsonPropertyName("verified")] bool Verified = false,
    [property: JsonPropertyName("failureStage")] string? FailureStage = null,
    [property: JsonPropertyName("failureCode")] string? FailureCode = null,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null,
    [property: JsonPropertyName("timestampUtc")] DateTimeOffset? TimestampUtc = null);

public sealed record TestDataResetReadinessResultDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("databaseName")] string DatabaseName,
    [property: JsonPropertyName("scriptPath")] string ScriptPath,
    [property: JsonPropertyName("workingDirectory")] string WorkingDirectory,
    [property: JsonPropertyName("manifestPath")] string ManifestPath,
    [property: JsonPropertyName("dependencyGraphPath")] string DependencyGraphPath,
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("stdOutSummary")] string? StdOutSummary = null,
    [property: JsonPropertyName("stdErrSummary")] string? StdErrSummary = null,
    [property: JsonPropertyName("failureStage")] string? FailureStage = null,
    [property: JsonPropertyName("failureCode")] string? FailureCode = null,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null,
    [property: JsonPropertyName("timestampUtc")] DateTimeOffset? TimestampUtc = null,
    [property: JsonPropertyName("configuredTimeoutSeconds")] int? ConfiguredTimeoutSeconds = null,
    [property: JsonPropertyName("liveMode")] bool? LiveMode = null,
    [property: JsonPropertyName("testOverride")] bool? TestOverride = null);
