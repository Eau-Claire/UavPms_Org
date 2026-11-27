namespace UavPms.NotificationService.API.Services;

public interface INotificationConnectionRegistry
{
    void AddToGroup(string groupName, string connectionId);
    void RemoveFromGroup(string groupName, string connectionId);
    void RemoveConnection(string connectionId);
    IReadOnlyCollection<string> GetConnections(string groupName);
}

public class NotificationConnectionRegistry : INotificationConnectionRegistry
{
    // Giữ cập nhật membership và xóa nhóm rỗng trong cùng một khóa để không mất lượt join đồng thời.
    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);

    public void AddToGroup(string groupName, string connectionId)
    {
        lock (_gate)
        {
            if (!_groups.TryGetValue(groupName, out var connections))
            {
                connections = new HashSet<string>(StringComparer.Ordinal);
                _groups.Add(groupName, connections);
            }

            connections.Add(connectionId);
        }
    }

    public void RemoveFromGroup(string groupName, string connectionId)
    {
        lock (_gate)
        {
            RemoveMembership(groupName, connectionId);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        lock (_gate)
        {
            foreach (var groupName in _groups.Keys.ToArray())
            {
                RemoveMembership(groupName, connectionId);
            }
        }
    }

    public IReadOnlyCollection<string> GetConnections(string groupName)
    {
        lock (_gate)
        {
            return _groups.TryGetValue(groupName, out var connections)
                ? connections.ToArray()
                : Array.Empty<string>();
        }
    }

    private void RemoveMembership(string groupName, string connectionId)
    {
        if (!_groups.TryGetValue(groupName, out var connections))
        {
            return;
        }

        connections.Remove(connectionId);
        if (connections.Count == 0)
        {
            _groups.Remove(groupName);
        }
    }
}
