using System.Collections.Concurrent;

namespace UavPms.NotificationService.API.Services;

public interface INotificationConnectionRegistry
{
    void AddToGroup(string groupName, string connectionId);
    void RemoveFromGroup(string groupName, string connectionId);
    IReadOnlyCollection<string> GetConnections(string groupName);
}

public class NotificationConnectionRegistry : INotificationConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _groups = new();

    public void AddToGroup(string groupName, string connectionId)
    {
        var connections = _groups.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>());
        connections[connectionId] = 0;
    }

    public void RemoveFromGroup(string groupName, string connectionId)
    {
        if (!_groups.TryGetValue(groupName, out var connections))
        {
            return;
        }

        connections.TryRemove(connectionId, out _);

        if (connections.IsEmpty)
        {
            _groups.TryRemove(groupName, out _);
        }
    }

    public IReadOnlyCollection<string> GetConnections(string groupName)
    {
        return _groups.TryGetValue(groupName, out var connections)
            ? connections.Keys.ToArray()
            : Array.Empty<string>();
    }
}
