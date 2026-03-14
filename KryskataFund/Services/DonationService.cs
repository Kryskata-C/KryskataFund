using KryskataFund.Data;
using KryskataFund.Models;
using KryskataFund.Services.Interfaces;

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

        public IEnumerable<Donation> GetByFundId(int fundId)
        {
            return _context.Donations
                .Where(d => d.FundId == fundId)
                .ToList();
        }

        public IEnumerable<Donation> GetByUserId(int userId)
        {
            return _context.Donations
                .Where(d => d.UserId == userId)
                .ToList();
        }

        public decimal GetTotalDonated(int userId)
        {
            return _context.Donations
                .Where(d => d.UserId == userId)
                .Sum(d => d.Amount);
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
