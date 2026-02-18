using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TruLoad.Backend.Hubs;

/// <summary>
/// SignalR hub for real-time analytics query results.
/// Clients connect and receive query results pushed from background processing.
/// Tracks active connections so the controller can validate connectionIds before dispatching work.
/// </summary>
[Authorize]
public class AnalyticsHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _activeConnections = new();

    private readonly ILogger<AnalyticsHub> _logger;

    public AnalyticsHub(ILogger<AnalyticsHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name ?? "unknown";
        _activeConnections[Context.ConnectionId] = userId;
        _logger.LogInformation("Analytics client connected: {ConnectionId}, User: {User}", Context.ConnectionId, userId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _activeConnections.TryRemove(Context.ConnectionId, out _);
        _logger.LogInformation("Analytics client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Check whether a connection ID is currently active.
    /// </summary>
    public static bool IsConnected(string connectionId) => _activeConnections.ContainsKey(connectionId);
}
