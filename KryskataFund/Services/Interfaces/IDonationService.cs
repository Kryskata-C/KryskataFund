using KryskataFund.Models;

namespace KryskataFund.Services.Interfaces
{
    /// <summary>
    /// Defines operations for managing donations.
    /// Provides methods for creating, retrieving, and aggregating donation data.
    /// </summary>
    public interface IDonationService
    {
        /// <summary>
        /// Creates a new donation and persists it to the database.
        /// </summary>
        /// <param name="donation">The donation entity to create.</param>
        /// <returns>The created donation with its generated identifier.</returns>
        Task<Donation> CreateAsync(Donation donation);

        /// <summary>
        /// Retrieves all donations associated with a specific fund.
        /// </summary>
        /// <param name="fundId">The fund identifier.</param>
        /// <returns>A collection of donations for the specified fund.</returns>
        Task<IEnumerable<Donation>> GetByFundIdAsync(int fundId);

        /// <summary>
        /// Retrieves all donations made by a specific user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>A collection of donations made by the specified user.</returns>
        Task<IEnumerable<Donation>> GetByUserIdAsync(int userId);

        /// <summary>
        /// Calculates the total amount donated by a specific user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns>The sum of all donation amounts by the user.</returns>
        Task<decimal> GetTotalDonatedAsync(int userId);

        /// <summary>
        /// Deletes a donation by its identifier.
        /// </summary>
        /// <param name="id">The donation identifier.</param>
        Task DeleteAsync(int id);
    }
}
