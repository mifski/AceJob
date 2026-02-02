using System.ComponentModel.DataAnnotations;

namespace AceJob.ViewModels
{
    public class Login
    {
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        /// <summary>
        /// Google reCAPTCHA v3 token - populated by JavaScript on form submission
        /// </summary>
        public string? RecaptchaToken { get; set; }
    }
}


