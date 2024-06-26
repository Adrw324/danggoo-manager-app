using DanggooManager.Data;
using DanggooManager.Hubs;
using DanggooManager.Controllers;
using DanggooManager.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddLogging(logging =>
{
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    logging.AddConsole();
});
builder.Services.AddSignalR();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// TableHub를 싱글톤으로 등록
builder.Services.AddSingleton<TableHub>();
builder.Services.AddScoped<GamesController>();

// CORS 설정
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.SetIsOriginAllowed(_ => true)
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

var app = builder.Build();

// 전역 변수로 테이블 연결 상태를 저장
var tableConnectionStatus = new ConcurrentDictionary<int, bool>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAuthorization();
app.UseWebSockets();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var tableId = context.Request.Query["tableId"];
            Console.WriteLine($"WebSocket connection established for table {tableId}");
            await HandleWebSocketConnection(context, webSocket, tableId);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
    else
    {
        await next();
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<TableHub>("/tablehub");

// TableHub 초기화
var tableHub = app.Services.GetRequiredService<TableHub>();
tableHub.Initialize(tableConnectionStatus);

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();

    if (!context.Settings.Any())
    {
        context.Settings.Add(new Settings { FeePerMinute = 0.5m });
        context.SaveChanges();
    }
}

app.Run("http://0.0.0.0:5157");

async Task HandleWebSocketConnection(HttpContext context, WebSocket webSocket, string tableId)
{
    var buffer = new byte[1024 * 4];
    var hubContext = app.Services.GetRequiredService<IHubContext<TableHub>>();

    int parsedTableId = int.Parse(tableId);
    tableConnectionStatus[parsedTableId] = true;
    WebSocketManager.AddSocket(parsedTableId, webSocket);  // WebSocket 등록
    await hubContext.Clients.All.SendAsync("UpdateTableStatus", parsedTableId, true);

    // 연결 성공 메시지 전송
    var connectionStatusMessage = JsonSerializer.Serialize(new { type = "connectionStatus", isConnected = true });
    await webSocket.SendAsync(new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(connectionStatusMessage)), WebSocketMessageType.Text, true, CancellationToken.None);

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                var jsonMessage = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message);
                
               switch (jsonMessage["type"].GetString())
                {
                    case "updateTableStatus":
                        var isActive = jsonMessage["isActive"].GetBoolean();
                        tableConnectionStatus[parsedTableId] = isActive;
                        await hubContext.Clients.All.SendAsync("UpdateTableStatus", parsedTableId, isActive);
                        break;
                    case "GameStarted":
                        await StartGame(parsedTableId, hubContext);
                        break;
                    case "GameEnded":
                        await EndGame(parsedTableId, hubContext);
                        break;
                    case "ForceStartGame":
                        await WebSocketManager.SendMessageAsync(parsedTableId, JsonSerializer.Serialize(new { type = "ForceStartGame", tableId = parsedTableId }));
                        break;
                    case "ForceEndGame":
                        await WebSocketManager.SendMessageAsync(parsedTableId, JsonSerializer.Serialize(new { type = "ForceEndGame", tableId = parsedTableId }));
                        break;
                }
                await hubContext.Clients.All.SendAsync("ReceiveWebSocketMessage", tableId, message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            }
        }
    }
    finally
    {
        tableConnectionStatus[parsedTableId] = false;
        await hubContext.Clients.All.SendAsync("UpdateTableStatus", parsedTableId, false);
        Console.WriteLine($"WebSocket connection closed for table {tableId}");
    }
}

async Task StartGame(int tableId, IHubContext<TableHub> hubContext)
{
    using var scope = app.Services.CreateScope();
    var gamesController = scope.ServiceProvider.GetRequiredService<GamesController>();
    var result = await gamesController.StartGame(tableId);
    await hubContext.Clients.All.SendAsync("GameStarted", tableId, result);
}

async Task EndGame(int tableId, IHubContext<TableHub> hubContext)
{
    using var scope = app.Services.CreateScope();
    var gamesController = scope.ServiceProvider.GetRequiredService<GamesController>();
    var result = await gamesController.EndGame(tableId);
    await hubContext.Clients.All.SendAsync("GameEnded", tableId, result);
}
public static class WebSocketManager
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
                var bytes = System.Text.Encoding.UTF8.GetBytes(message);
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
                var bytes = System.Text.Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}