using KryskataFund.Models;

namespace KryskataFund.Services.Interfaces
{
    /// <summary>
    /// Defines operations for managing fundraising campaigns.
    /// Provides CRUD functionality along with querying and aggregation methods.
    /// </summary>
    public interface IFundService
    {
        /// <summary>
        /// Retrieves a fund by its unique identifier.
        /// </summary>
        /// <param name="id">The fund identifier.</param>
        /// <returns>The fund if found; otherwise null.</returns>
        Task<Fund?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all funds from the database.
        /// </summary>
        /// <returns>A collection of all funds.</returns>
        Task<IEnumerable<Fund>> GetAllAsync();

        /// <summary>
        /// Retrieves all funds belonging to a specific category.
        /// </summary>
        /// <param name="category">The category name to filter by.</param>
        /// <returns>A collection of funds matching the category.</returns>
        Task<IEnumerable<Fund>> GetByCategoryAsync(string category);

        /// <summary>
        /// Searches funds by title or description matching the query string.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <returns>A collection of funds matching the search criteria.</returns>
        Task<IEnumerable<Fund>> SearchAsync(string query);

        /// <summary>
        /// Creates a new fund and persists it to the database.
        /// </summary>
        /// <param name="fund">The fund entity to create.</param>
        /// <returns>The created fund with its generated identifier.</returns>
        Task<Fund> CreateAsync(Fund fund);

        /// <summary>
        /// Updates an existing fund in the database.
        /// </summary>
        /// <param name="fund">The fund entity with updated values.</param>
        Task UpdateAsync(Fund fund);

        /// <summary>
        /// Deletes a fund by its identifier.
        /// </summary>
        /// <param name="id">The fund identifier.</param>
        Task DeleteAsync(int id);

        /// <summary>
        /// Calculates the total amount raised across all funds.
        /// </summary>
        /// <returns>The sum of all raised amounts.</returns>
        Task<decimal> GetTotalRaisedAsync();

        /// <summary>
        /// Counts the number of active campaigns (funds whose end date has not passed).
        /// </summary>
        /// <returns>The count of active campaigns.</returns>
        Task<int> GetActiveCampaignCountAsync();

        /// <summary>
        /// Retrieves the top funded campaigns ordered by raised amount descending.
        /// </summary>
        /// <param name="count">The number of top funds to return.</param>
        /// <returns>A collection of the highest-funded campaigns.</returns>
        Task<IEnumerable<Fund>> GetTopFundedAsync(int count);
    }
}
