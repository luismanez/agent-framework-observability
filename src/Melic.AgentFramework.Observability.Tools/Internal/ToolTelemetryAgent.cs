// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Tools.Internal;

internal sealed class ToolTelemetryAgent
{
    internal const string ActivityName = "agent_tool_call";
    internal const string MafOperationNameAttribute = "gen_ai.operation.name";
    internal const string MafExecuteToolOperationName = "execute_tool";

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

        ActivityTarget activityTarget = default;
        ToolInvocationData? invocationData = null;

        try
        {
            invocationData = _mapper.ToToolInvocationData(context);
            activityTarget = ResolveActivityTarget();
            EnrichAtStart(activityTarget, invocationData);
        }
        catch
        {
            activityTarget.Dispose();
            activityTarget = default;
        }

        try
        {
            object? result = await next(context, cancellationToken).ConfigureAwait(false);
            TryEnrichSuccess(activityTarget.Activity, result);
            return result;
        }
        catch (Exception exception)
        {
            TryEnrichFailure(activityTarget.Activity, exception);
            throw;
        }
        finally
        {
            activityTarget.Dispose();
        }
    }

    private ActivityTarget ResolveActivityTarget()
    {
        Activity? current = Activity.Current;
        if (IsMafExecuteToolActivity(current))
        {
            return new ActivityTarget(current, OwnsActivity: false);
        }

        return new ActivityTarget(_activitySource.StartActivity(ActivityName, ActivityKind.Internal), OwnsActivity: true);
    }

    private void EnrichAtStart(ActivityTarget activityTarget, ToolInvocationData invocationData)
    {
        Activity? activity = activityTarget.Activity;
        if (activity is null)
        {
            return;
        }

        try
        {
            if (activityTarget.OwnsActivity)
            {
                activity.SetTag(ToolAttributeNames.ToolName, invocationData.ToolName);
                if (invocationData.CallId is not null)
                {
                    activity.SetTag(ToolAttributeNames.ToolCallId, invocationData.CallId);
                }
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

    private static bool IsMafExecuteToolActivity(Activity? activity)
        => string.Equals(
            activity?.GetTagItem(MafOperationNameAttribute) as string,
            MafExecuteToolOperationName,
            StringComparison.Ordinal);

    private readonly record struct ActivityTarget(Activity? Activity, bool OwnsActivity) : IDisposable
    {
        public void Dispose()
        {
            if (OwnsActivity)
            {
                Activity?.Dispose();
            }
        }
    }
}