using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KryskataFund.Services
{
    public class DonationService : IDonationService
    {
        private readonly ApplicationDbContext _context;

        public DonationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Donation> CreateAsync(Donation donation)
        {
            _context.Donations.Add(donation);
            await _context.SaveChangesAsync();
            return donation;
        }

        public async Task<IEnumerable<Donation>> GetByFundIdAsync(int fundId)
        {
            return await _context.Donations
                .Where(d => d.FundId == fundId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Donation>> GetByUserIdAsync(int userId)
        {
            return await _context.Donations
                .Where(d => d.UserId == userId)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalDonatedAsync(int userId)
        {
            return await _context.Donations
                .Where(d => d.UserId == userId)
                .SumAsync(d => d.Amount);
        }

        public async Task DeleteAsync(int id)
        {
            var donation = await _context.Donations.FindAsync(id);
            if (donation != null)
            {
                _context.Donations.Remove(donation);
                await _context.SaveChangesAsync();
            }
        }
    }
}
