using System.Text.Json;

namespace AirPodsWinTray.Services;

internal static class AgentDebugLog
{
    private static readonly object Gate = new();

    internal static void Write<T>(string hypothesisId, string location, string message, T data)
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                sessionId = "4b1180",
                runId = "pre-fix",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            lock (Gate)
            {
                File.AppendAllText(@"e:\code\airpods-win\.cursor\debug-4b1180.log", line + "\n");
            }
        }
        catch
        {
            // debug log must not affect connect/disconnect
        }
    }
}
