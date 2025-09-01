// Models/LoginViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace ProjectNet.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Remember Me?")]
        public bool RememberMe { get; set; }
    }
}
