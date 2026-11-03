using System.Text.Json;

namespace PowerPlanTray;

// Codex writes explicit task_started / task_complete / turn_aborted events.
internal sealed class CodexSessionLog : AgentSessionLog
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    internal override TimeSpan ActivityTimeout => Timeout;

    private protected override void ReadEvent(JsonElement root, string kind, DateTimeOffset eventTime)
    {
        root.TryGetProperty("payload", out var payload);
        var payloadKind = StringProperty(payload, "type");
        if (kind != "event_msg" || payloadKind is null)
        {
            if (Active && kind == "response_item" &&
                (payloadKind is "function_call" or "function_call_output" or "reasoning" ||
                 payloadKind == "message" && StringProperty(payload, "role") == "assistant"))
                LastActivity = eventTime;
            return;
        }
        switch (payloadKind)
        {
            case "task_started":
                Start(eventTime);
                break;
            case "task_complete":
            case "turn_aborted":
                Complete(eventTime);
                break;
            case "token_count":
            case "token_usage_record":
            case "item_completed":
            case "agent_message":
            case "agent_reasoning":
                if (Active) LastActivity = eventTime;
                break;
        }
    }
}
