using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DanggooManager.Data;
using DanggooManager.Models;
using System.Text.Json;

namespace DanggooManager.Controllers
{
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Accounts
        public async Task<IActionResult> Index()
        {
            return View(await _context.Accounts.ToListAsync());
        }

        // GET: Accounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _context.Accounts
                .FirstOrDefaultAsync(m => m.Id == id);
            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }

        // GET: Accounts/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Accounts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Username,Password,Average,TotalPlay,TotalScore")] Account account)
        {
            if (ModelState.IsValid)
            {
                _context.Add(account);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(account);
        }

        // GET: Accounts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }
            return View(account);
        }

        // POST: Accounts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Username,Password,Average,TotalPlay,TotalScore")] Account account)
        {
            if (id != account.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(account);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountExists(account.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(account);
        }

        // GET: Accounts/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var account = await _context.Accounts
                .FirstOrDefaultAsync(m => m.Id == id);
            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }

        // POST: Accounts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                _context.Accounts.Remove(account);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.Id == id);
        }

        // AccountsController에 추가
        public class RegisterResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        [HttpPost]
        public async Task<RegisterResult> Register(string firstName, string lastName, string username)
        {
            if (await _context.Accounts.AnyAsync(a => a.Username == username))
            {
                return new RegisterResult { Success = false, Message = "Username already exists" };
            }

            var account = new Account
            {
                FirstName = firstName,
                LastName = lastName,
                Username = username,
                Password = "defaultPassword", // 보안상 좋지 않으므로 나중에 수정 필요
                Average = 0,
                TotalPlay = 0,
                TotalScore = 0
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return new RegisterResult { Success = true, Message = "Registration successful" };
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<AccountDto>>> SearchPlayers(string query)
        {
            Console.WriteLine($"Searching players with query: {query}");

            if (string.IsNullOrWhiteSpace(query))
            {
                var allAccounts = await _context.Accounts
                    .Select(a => new AccountDto
                    {
                        Id = a.Id,
                        FirstName = a.FirstName,
                        LastName = a.LastName,
                        Username = a.Username
                    })
                    .ToListAsync();
                Console.WriteLine($"Returning all accounts. Count: {allAccounts.Count}");
                return allAccounts;
            }

            query = query.ToLower(); // 검색어를 소문자로 변환

            var results = await _context.Accounts
                .Where(a => a.FirstName.ToLower().Contains(query)
                         || a.LastName.ToLower().Contains(query)
                         || a.Username.ToLower().Contains(query))
                .Select(a => new AccountDto
                {
                    Id = a.Id,
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    Username = a.Username
                })
                .ToListAsync();

            Console.WriteLine($"Search results count: {results.Count}");
            return results;
        }
    }

    public class AccountDto
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
    }



}
