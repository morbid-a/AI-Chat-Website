using System;

namespace ProjectNet.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? RememberToken { get; set; }
        public DateTime? RememberTokenExpiry { get; set; }
    }
}


