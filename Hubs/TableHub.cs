using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using DanggooManager.Models;

namespace DanggooManager.Hubs
{
    public class TableHub : Hub
    {
        private ConcurrentDictionary<int, bool> _tableConnectionStatus;

        public void Initialize(ConcurrentDictionary<int, bool> tableConnectionStatus)
        {
            _tableConnectionStatus = tableConnectionStatus;
        }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveWebSocketMessage", user, message);
        }


        public async Task UpdateTableStatus(int tableId, bool isActive)
        {
            _tableConnectionStatus[tableId] = isActive;
            await Clients.All.SendAsync("UpdateTableStatus", tableId, isActive);
        }


        public async Task GetAllTableStatus()
        {
            var connectedTables = _tableConnectionStatus.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            await Clients.Caller.SendAsync("UpdateConnectionStatus", new { connectedTables });
        }

    }
}