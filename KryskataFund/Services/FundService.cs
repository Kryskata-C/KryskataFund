using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KryskataFund.Services
{
    public class FundService : IFundService
    {
        private readonly ApplicationDbContext _context;

        public FundService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Fund?> GetByIdAsync(int id)
        {
            return await _context.Funds.FirstOrDefaultAsync(f => f.Id == id);
        }

        public async Task<IEnumerable<Fund>> GetAllAsync()
        {
            return await _context.Funds.ToListAsync();
        }

        public async Task<IEnumerable<Fund>> GetByCategoryAsync(string category)
        {
            return await _context.Funds
                .Where(f => f.Category.ToLower() == category.ToLower())
                .ToListAsync();
        }

        public async Task<IEnumerable<Fund>> SearchAsync(string query)
        {
            var lowerQuery = query.ToLower();
            return await _context.Funds
                .Where(f => f.Title.ToLower().Contains(lowerQuery)
                         || f.Description.ToLower().Contains(lowerQuery))
                .ToListAsync();
        }

        public async Task<Fund> CreateAsync(Fund fund)
        {
            _context.Funds.Add(fund);
            await _context.SaveChangesAsync();
            return fund;
        }

        public async Task UpdateAsync(Fund fund)
        {
            _context.Funds.Update(fund);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var fund = await _context.Funds.FindAsync(id);
            if (fund != null)
            {
                _context.Funds.Remove(fund);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<decimal> GetTotalRaisedAsync()
        {
            return await _context.Funds.SumAsync(f => f.RaisedAmount);
        }

        public async Task<int> GetActiveCampaignCountAsync()
        {
            return await _context.Funds.CountAsync(f => f.EndDate > DateTime.UtcNow);
        }

        public async Task<IEnumerable<Fund>> GetTopFundedAsync(int count)
        {
            return await _context.Funds
                .OrderByDescending(f => f.RaisedAmount)
                .Take(count)
                .ToListAsync();
        }
    }
}
