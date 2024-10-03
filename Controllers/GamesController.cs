using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DanggooManager.Data;
using DanggooManager.Models;
using Microsoft.AspNetCore.SignalR;
using DanggooManager.Hubs;
using System.Text.Json;


namespace DanggooManager.Controllers
{
    public class GamesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TableHub> _hubContext;
        private readonly ILogger<GamesController> _logger;


        public GamesController(ApplicationDbContext context, IHubContext<TableHub> hubContext, ILogger<GamesController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }


        // GET: Games
        public async Task<IActionResult> Index()
        {
            return View(await _context.Games.ToListAsync());
        }

        // GET: Games/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games
                .FirstOrDefaultAsync(m => m.Id == id);
            if (game == null)
            {
                return NotFound();
            }

            return View(game);
        }

        // GET: Games/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Games/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Table_Num,Start,End,Playtime,Fee,finished")] Game game)
        {
            if (ModelState.IsValid)
            {
                _context.Add(game);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(game);
        }

        // GET: Games/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return NotFound();
            }
            return View(game);
        }

        // POST: Games/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Table_Num,Start,End,Playtime,Fee,finished")] Game game)
        {
            if (id != game.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(game);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GameExists(game.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                // 편집이 성공적으로 완료되면 JavaScript를 반환하여 창을 닫습니다.
                return Content(@"
            <script>
                window.opener.getGameList(" + game.Table_Num + @");
                window.close();
            </script>", "text/html");
            }
            return View(game);
        }

        // GET: Games/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var game = await _context.Games
                .FirstOrDefaultAsync(m => m.Id == id);
            if (game == null)
            {
                return NotFound();
            }

            return View(game);
        }

        // POST: Games/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game != null)
            {
                _context.Games.Remove(game);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GameExists(int id)
        {
            return _context.Games.Any(e => e.Id == id);
        }

        // GET: Games/GetGameList/5
        [HttpGet]
        public async Task<IActionResult> GetGameList(int id)
        {
            var games = await _context.Games
                .Where(g => g.Table_Num == id)
                .OrderBy(g => g.Start)
                .ToListAsync();

            return Json(games);
        }

        // POST: Games/Save/5
        [HttpPost]
        public async Task<IActionResult> Save(int id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return Json(new { success = false, message = "Game not found" });
            }

            game.End = DateTime.Now;
            game.Playtime = (int)(game.End - game.Start).TotalMinutes;
            game.Fee = await CalculateFee(game.Playtime);
            game.finished = true;

            try
            {
                await _context.SaveChangesAsync();
                return Json(new { success = true, tableId = game.Table_Num });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "Concurrency error occurred" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateFeePerMinute([FromBody] decimal feePerMinute)
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new Settings { FeePerMinute = feePerMinute };
                _context.Settings.Add(settings);
            }
            else
            {
                settings.FeePerMinute = feePerMinute;
                _context.Settings.Update(settings);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Fee per minute updated successfully." });
        }

        [HttpGet]
        public async Task<IActionResult> GetFeePerMinute()
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();
            decimal feePerMinute = settings?.FeePerMinute ?? 0.5m; // Default to 0.5 if not set
            return Json(new { feePerMinute });
        }

        // 기존의 CalculateFee 메서드를 수정합니다
        private async Task<decimal> CalculateFee(int playtime)
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();
            decimal feePerMinute = settings?.FeePerMinute ?? 0.5m; // Default to 0.5 if not set
            return playtime * feePerMinute;
        }

        // POST: Games/StartGame
        [HttpPost]
        public async Task<IActionResult> StartGame(int tableNum)
        {
            var game = new Game
            {
                Table_Num = tableNum,
                Start = DateTime.Now,
                finished = false
            };

            try
            {
                _context.Games.Add(game);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Game successfully added to database for table {tableNum}. Game ID: {game.Id}");

                await _hubContext.Clients.All.SendAsync("GameStarted", tableNum);
                _logger.LogInformation($"GameStarted signal sent for table {tableNum}");

                return Json(new { success = true, game = game });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting game for table {tableNum}: {ex.Message}");
                return Json(new { success = false, message = "Failed to start game", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EndGame(int tableNum)
        {
            var game = await _context.Games
                .FirstOrDefaultAsync(g => g.Table_Num == tableNum && !g.finished);

            if (game != null)
            {
                game.End = DateTime.Now;
                game.Playtime = (int)(game.End - game.Start).TotalMinutes;
                game.Fee = await CalculateFee(game.Playtime);
                game.finished = true;

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("GameEnded", tableNum);

                return Json(new { success = true, game = game });
            }

            return Json(new { success = false, message = "No active game found for this table." });
        }

        [HttpPost("Games/ForceStartGame/{tableNum}")]
        public async Task<IActionResult> ForceStartGame([FromRoute] int tableNum)
        {
            try
            {
                await WebSocketManager.SendMessageAsync(tableNum, JsonSerializer.Serialize(new { type = "ForceStartGame", tableId = tableNum }));
                _logger.LogInformation($"ForceStartGame message sent to table {tableNum}");
                return Ok($"ForceStartGame message sent to table {tableNum}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending ForceStartGame message: {ex.Message}");
                return StatusCode(500, "Error sending ForceStartGame message");
            }
        }

        [HttpPost("Games/ForceEndGame/{tableNum}")]
        public async Task<IActionResult> ForceEndGame([FromRoute] int tableNum)
        {
            try
            {
                await WebSocketManager.SendMessageAsync(tableNum, JsonSerializer.Serialize(new { type = "ForceEndGame", tableId = tableNum }));
                _logger.LogInformation($"ForceEndGame message sent to table {tableNum}");
                return Ok($"ForceEndGame message sent to table {tableNum}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending ForceEndGame message: {ex.Message}");
                return StatusCode(500, "Error sending ForceEndGame message");
            }
        }



        [HttpPost]
        public async Task<IActionResult> MoveGameToRecords(int id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return Json(new { success = false, message = "Game not found." });
            }

            var record = new Record
            {
                Table_Num = game.Table_Num,
                Date = game.Start,
                Start = game.Start,
                End = game.End,
                Playtime = game.Playtime,
                Fee = game.Fee
            };

            _context.Records.Add(record);
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Game moved to records successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteGame(int id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return Json(new { success = false, message = "Game not found." });
            }

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Game deleted successfully." });
        }

        [HttpPost("Games/MoveAllGamesToRecords/{tableId}")]
        public async Task<IActionResult> MoveAllGamesToRecords([FromRoute] int tableId)
        {
            _logger.LogInformation($"Trying to saving games at table {tableId} to Records");

            // tableId 값 로그
            Console.WriteLine($"Received tableId: {tableId}");


            var games = await _context.Games.Where(g => g.Table_Num == tableId).ToListAsync();

            foreach (var game in games)
            {
                var record = new Record
                {
                    Table_Num = game.Table_Num,
                    Date = game.Start,
                    Start = game.Start,
                    End = game.End,
                    Playtime = game.Playtime,
                    Fee = game.Fee
                };

                _context.Records.Add(record);
                _context.Games.Remove(game);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "All games moved to records successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> SaveGame(int id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null)
            {
                return Json(new { success = false, message = "Game not found." });
            }

            var record = new Record
            {
                Table_Num = game.Table_Num,
                Date = game.Start,
                Start = game.Start,
                End = game.End,
                Playtime = game.Playtime,
                Fee = game.Fee
            };

            _context.Records.Add(record);
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Game saved successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> SaveAllGames(int tableId)
        {
            Console.WriteLine($"AAAA {tableId}");


            try
            {
                var games = await _context.Games.Where(g => g.Table_Num == tableId).ToListAsync();


                Console.WriteLine($"Found {games.Count} games for table {tableId}");
                foreach (var game in games)
                {
                    var record = new Record
                    {
                        Table_Num = game.Table_Num,
                        Date = game.Start,
                        Start = game.Start,
                        End = game.End,
                        Playtime = game.Playtime,
                        Fee = game.Fee
                    };

                    _context.Records.Add(record);
                    _context.Games.Remove(game);

                    Console.WriteLine($"Processed game {game.Id} for table {tableId}");
                }

                await _context.SaveChangesAsync();

                Console.WriteLine($"Successfully saved all games for table {tableId}");


                return Json(new { success = true, message = "All games saved successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving games for table {tableId}: {ex.Message}");
                return Json(new { success = false, message = $"Error saving games: {ex.Message}" });
            }

        }

        [HttpGet("Games/SendTestMessage/{tableNum}")]
        public async Task<IActionResult> SendTestMessage([FromRoute] int tableNum)
        {
            try
            {
                await WebSocketManager.SendMessageAsync(tableNum, JsonSerializer.Serialize(new { type = "TestMessage", tableId = tableNum, message = $"Test message for table {tableNum}" }));
                _logger.LogInformation($"Test message sent to table {tableNum}");
                return Ok($"Test message sent to table {tableNum}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending test message: {ex.Message}");
                return StatusCode(500, "Error sending test message");
            }
        }
    }

    
}

