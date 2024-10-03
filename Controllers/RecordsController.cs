using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DanggooManager.Data;
using DanggooManager.Models;

namespace DanggooManager.Controllers
{
    public class RecordsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecordsController(ApplicationDbContext context)
        {
            _context = context;
        }

       // GET: Records
        public async Task<IActionResult> Index(int? tableNum, int? year, int? month, DateTime? date)
        {
            var records = from r in _context.Records
                          select r;

            if (tableNum.HasValue)
            {
                records = records.Where(r => r.Table_Num == tableNum.Value);
            }

            if (year.HasValue)
            {
                records = records.Where(r => r.Date.Year == year.Value);
            }

            if (month.HasValue)
            {
                records = records.Where(r => r.Date.Month == month.Value);
            }

            if (date.HasValue)
            {
                records = records.Where(r => r.Date.Date == date.Value.Date);
            }

           var recordsList = await records.ToListAsync();

           // Calculate total fee
        decimal totalFee = recordsList.Sum(r => r.Fee);
        ViewBag.TotalFee = totalFee;

            // 레코드가 있는지 확인
            if (recordsList.Any())
            {
                ViewBag.TableNumbers = await _context.Records.Select(r => r.Table_Num).Distinct().OrderBy(t => t).ToListAsync();
                ViewBag.Years = await _context.Records.Select(r => r.Date.Year).Distinct().OrderByDescending(y => y).ToListAsync();
                ViewBag.Months = Enumerable.Range(1, 12).ToList();
                
                var minDate = recordsList.Min(r => r.Date);
                var maxDate = recordsList.Max(r => r.Date);
                ViewBag.MinDate = minDate.ToString("yyyy-MM-dd");
                ViewBag.MaxDate = maxDate.ToString("yyyy-MM-dd");
            }
            else
            {
                // 레코드가 없을 경우 기본값 설정
                ViewBag.TableNumbers = new List<int>();
                ViewBag.Years = new List<int>();
                ViewBag.Months = Enumerable.Range(1, 12).ToList();
                ViewBag.MinDate = DateTime.Today.ToString("yyyy-MM-dd");
                ViewBag.MaxDate = DateTime.Today.ToString("yyyy-MM-dd");
            }

            return View(recordsList);
        }

        // GET: Records/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @record = await _context.Records
                .FirstOrDefaultAsync(m => m.Id == id);
            if (@record == null)
            {
                return NotFound();
            }

            return View(@record);
        }

        // GET: Records/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Records/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Table_Num,Date,Start,End,Playtime,Fee")] Record @record)
        {
            if (ModelState.IsValid)
            {
                _context.Add(@record);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(@record);
        }

        // GET: Records/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @record = await _context.Records.FindAsync(id);
            if (@record == null)
            {
                return NotFound();
            }
            return View(@record);
        }

        // POST: Records/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Table_Num,Date,Start,End,Playtime,Fee")] Record @record)
        {
            if (id != @record.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(@record);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecordExists(@record.Id))
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
            return View(@record);
        }

        // GET: Records/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @record = await _context.Records
                .FirstOrDefaultAsync(m => m.Id == id);
            if (@record == null)
            {
                return NotFound();
            }

            return View(@record);
        }

        // POST: Records/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @record = await _context.Records.FindAsync(id);
            if (@record != null)
            {
                _context.Records.Remove(@record);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RecordExists(int id)
        {
            return _context.Records.Any(e => e.Id == id);
        }
    }
}
