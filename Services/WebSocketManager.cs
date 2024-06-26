using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace DanggooManager.Services
{
    public class WebSocketManager
{
    private static ConcurrentDictionary<int, WebSocket> _sockets = new ConcurrentDictionary<int, WebSocket>();

    public static void AddSocket(int tableId, WebSocket socket)
    {
        _sockets[tableId] = socket;
    }

    public static async Task SendMessageAsync(int tableId, string message)
    {
        if (_sockets.TryGetValue(tableId, out WebSocket socket))
        {
            if (socket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }

    public static async Task SendMessageToAllAsync(string message)
    {
        foreach (var socket in _sockets.Values)
        {
            if (socket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
}