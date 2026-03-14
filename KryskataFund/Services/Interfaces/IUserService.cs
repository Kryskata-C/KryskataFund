using KryskataFund.Models;

namespace KryskataFund.Services.Interfaces
{
    /// <summary>
    /// Defines operations for managing user accounts.
    /// Provides CRUD functionality along with authentication helper methods.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Retrieves a user by their unique identifier.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        /// <returns>The user if found; otherwise null.</returns>
        User? GetById(int id);

        /// <summary>
        /// Retrieves a user by their email address.
        /// </summary>
        /// <param name="email">The email address to search for.</param>
        /// <returns>The user if found; otherwise null.</returns>
        User? GetByEmail(string email);

        /// <summary>
        /// Creates a new user and persists them to the database.
        /// </summary>
        /// <param name="user">The user entity to create.</param>
        /// <returns>The created user with their generated identifier.</returns>
        Task<User> CreateAsync(User user);

        /// <summary>
        /// Updates an existing user in the database.
        /// </summary>
        /// <param name="user">The user entity with updated values.</param>
        Task UpdateAsync(User user);

        /// <summary>
        /// Deletes a user by their identifier.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        Task DeleteAsync(int id);

        /// <summary>
        /// Checks whether a user with the given email address already exists.
        /// </summary>
        /// <param name="email">The email address to check.</param>
        /// <returns>True if the email is already registered; otherwise false.</returns>
        bool EmailExists(string email);

        /// <summary>
        /// Hashes a plain-text password using SHA256.
        /// </summary>
        /// <param name="password">The plain-text password.</param>
        /// <returns>The hashed password as a hexadecimal string.</returns>
        string HashPassword(string password);
    }
}
