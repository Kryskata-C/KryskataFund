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

        public Fund? GetById(int id)
        {
            return _context.Funds.FirstOrDefault(f => f.Id == id);
        }

        public IEnumerable<Fund> GetAll()
        {
            return _context.Funds.ToList();
        }

        public IEnumerable<Fund> GetByCategory(string category)
        {
            return _context.Funds
                .Where(f => f.Category.ToLower() == category.ToLower())
                .ToList();
        }

        public IEnumerable<Fund> Search(string query)
        {
            var lowerQuery = query.ToLower();
            return _context.Funds
                .Where(f => f.Title.ToLower().Contains(lowerQuery)
                         || f.Description.ToLower().Contains(lowerQuery))
                .ToList();
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

        public decimal GetTotalRaised()
        {
            return _context.Funds.Sum(f => f.RaisedAmount);
        }

        public int GetActiveCampaignCount()
        {
            return _context.Funds.Count(f => f.EndDate > DateTime.UtcNow);
        }

        public IEnumerable<Fund> GetTopFunded(int count)
        {
            return _context.Funds
                .OrderByDescending(f => f.RaisedAmount)
                .Take(count)
                .ToList();
        }
    }
}
