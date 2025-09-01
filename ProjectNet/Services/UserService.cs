using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; 
using ProjectNet.Data;
using ProjectNet.Models;

namespace ProjectNet.Services
{
    public class UserService
    {
        private readonly Db _dbContext;//allows saving in database
        private readonly IPasswordHasher<User> _passwordHasher;//hashes password literaly

        public UserService(Db dbContext, IPasswordHasher<User> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        public async Task<IdentityResult> CreateUserAsync(string username, string password,string email)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.", nameof(username));

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required.", nameof(email));

            if (await _dbContext.Users.AnyAsync(u => u.Username == username))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "DuplicateUserName",
                    Description = "Username already taken."
                });
            }

            var user = new User
            {
                Username = username,
                Email=email,
                PasswordHash = _passwordHasher.HashPassword(null!, password),
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();

            return IdentityResult.Success;
        }

        public async Task<SignInResult> ValidateUserAsync(string username, string password, bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return SignInResult.Failed;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return SignInResult.Failed;

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verifyResult != PasswordVerificationResult.Success)
                return SignInResult.Failed;

            // Clear previous token if not remembering
            if (!rememberMe)
            {
                user.RememberToken = null;
                user.RememberTokenExpiry = null;
                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();
            }

            return SignInResult.Success;

        }

        public async Task<User?> GetUserByRememberTokenAsync(Guid token)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.RememberToken == token
                    && u.RememberTokenExpiry > DateTime.UtcNow);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username == username);
        }
        public async Task UpdateRememberTokenAsync(User user)
        {
            _dbContext.Users.Update(user);
            await _dbContext.SaveChangesAsync();
        }

    }
}