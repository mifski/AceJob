using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AceJob.ViewModels
{
    public class Register
    {
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }
        
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [DataType(DataType.Text)]
        public string Gender { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "NRIC")]
        public string NRIC { get; set; }

        [Required]
        [DataType(DataType.EmailAddress)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Password and confirmation password does not match")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateOnly DateOfBirth { get; set; }
  
        [Required]
        [Display(Name = "Resume (.docx or .pdf)")]
        public IFormFile Resume { get; set; }

        [Required]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Who Am I")]
        [StringLength(500, ErrorMessage = "Maximum 500 characters allowed")]
        public string WhoAmI { get; set; }

        /// <summary>
        /// reCAPTCHA v3 token for bot protection
        /// </summary>
        public string? RecaptchaToken { get; set; }
    }
}
