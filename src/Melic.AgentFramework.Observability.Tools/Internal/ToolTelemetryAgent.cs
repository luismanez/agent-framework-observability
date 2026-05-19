// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Tools.Internal;

internal sealed class ToolTelemetryAgent
{
    internal const string ActivityName = "agent_tool_call";

    private readonly ToolTelemetryOptions _options;
    private readonly ActivitySource _activitySource;
    private readonly ToolInvocationMapper _mapper = new();
    private readonly ToolPayloadSerializer _payloadSerializer = new();
    private readonly InvocationAttemptRegistry _attemptRegistry = new();

    internal ToolTelemetryAgent(ToolTelemetryOptions options)
    {
        _options = options;
        _activitySource = new ActivitySource(options.ActivitySourceName);
    }

    internal async ValueTask<object?> InvokeAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        Activity? activity = null;
        ToolInvocationData? invocationData = null;

        try
        {
            invocationData = _mapper.ToToolInvocationData(context);
            activity = _activitySource.StartActivity(ActivityName, ActivityKind.Internal);
            EnrichAtStart(activity, invocationData);
        }
        catch
        {
            activity?.Dispose();
            activity = null;
        }

        try
        {
            object? result = await next(context, cancellationToken).ConfigureAwait(false);
            TryEnrichSuccess(activity, result);
            return result;
        }
        catch (Exception exception)
        {
            TryEnrichFailure(activity, exception);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private void EnrichAtStart(Activity? activity, ToolInvocationData invocationData)
    {
        if (activity is null)
        {
            return;
        }

        try
        {
            activity.SetTag(ToolAttributeNames.ToolName, invocationData.ToolName);
            if (invocationData.CallId is not null)
            {
                activity.SetTag(ToolAttributeNames.ToolCallId, invocationData.CallId);
            }

            if (_options.CaptureInput)
            {
                string? input = _payloadSerializer.SerializeInput(invocationData.InputPayload, _options.MaxInputLength);
                if (input is not null)
                {
                    activity.SetTag(ToolAttributeNames.ToolInput, input);
                }
            }

            AttemptInfo? attempt = _attemptRegistry.Record(invocationData.ParentActivity, invocationData.CallId);
            if (attempt is { } attemptInfo)
            {
                activity.SetTag(ToolAttributeNames.IsRetry, attemptInfo.IsRetry);
                activity.SetTag(ToolAttributeNames.AttemptIndex, attemptInfo.AttemptIndex);
            }
        }
        catch
        {
        }
    }

    private void TryEnrichSuccess(Activity? activity, object? result)
    {
        if (activity is null)
        {
            return;
        }

        try
        {
            activity.SetStatus(ActivityStatusCode.Ok);
            if (_options.CaptureOutput)
            {
                string? output = _payloadSerializer.SerializeOutput(result, _options.MaxOutputLength);
                if (output is not null)
                {
                    activity.SetTag(ToolAttributeNames.ToolOutput, output);
                }
            }
        }
        catch
        {
        }
    }

    private void TryEnrichFailure(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        try
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            if (_options.CaptureOutput)
            {
                string? output = _payloadSerializer.SerializeException(exception, _options.MaxOutputLength);
                if (output is not null)
                {
                    activity.SetTag(ToolAttributeNames.ToolOutput, output);
                }
            }
        }
        catch
        {
        }
    }
}