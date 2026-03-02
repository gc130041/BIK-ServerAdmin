using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BIK.CoreBanking.Data;
using BIK.CoreBanking.DTOs;
using System.Linq;
using System.Threading.Tasks;

namespace BIK.CoreBanking.Controllers
{
    [ApiController]
    [Route("BIK/v1/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{id}/balance")]
        public async Task<IActionResult> GetBalance(int id)
        {
            var account = await _context.Accounts
                .Where(a => a.Id == id)
                .Select(a => new AccountBalanceResponse 
                { 
                    AccountId = a.Id, 
                    Balance = a.Balance 
                })
                .FirstOrDefaultAsync();

            if (account == null)
                return NotFound(new { message = "Cuenta no encontrada" });

            return Ok(account);
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var accountExists = await _context.Accounts.AnyAsync(a => a.Id == id);
            if (!accountExists)
                return NotFound(new { message = "Cuenta no encontrada" });

            var history = await _context.TransactionRecords
                .Where(t => t.AccountId == id)
                .OrderByDescending(t => t.Timestamp)
                .Take(50) 
                .Select(t => new TransactionHistoryResponse
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Timestamp = t.Timestamp,
                    Description = t.TransactionType, 
                    Type = t.TransactionType
                })
                .ToListAsync();

            return Ok(history);
        }
    }

    [HttpPost("sync-account")]
public async Task<IActionResult> SyncAccount([FromBody] SyncAccountRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.AccountNumber) || string.IsNullOrEmpty(request.UserId))
            return BadRequest(new { message = "Datos de cuenta inválidos" });

        var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == request.AccountNumber);
        if (existingAccount != null)
            return Conflict(new { message = "El número de cuenta ya está registrado en el Core Banking" });

        var newAccount = new Account
        {
            AccountNumber = request.AccountNumber,
            UserId = request.UserId,
            Balance = request.InitialBalance
        };

        _context.Accounts.Add(newAccount);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBalance), new { id = newAccount.Id }, newAccount);
    }

public class SyncAccountRequest
    {
        public string AccountNumber { get; set; }
        public string UserId { get; set; }
        public decimal InitialBalance { get; set; }
    }
}