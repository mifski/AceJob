using AceJob.Model;
using AceJob.ViewModels;
using AceJob.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace AceJob.Pages
{
 public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IAuditService _auditService;
        private readonly IPasswordService _passwordService;
        private readonly ILogger<RegisterModel> _logger;

        [BindProperty]
        public Register RModel { get; set; } = null!;

        private const string SessionVersionClaimType = "sv";

        public RegisterModel(
         UserManager<ApplicationUser> userManager,
      SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            IWebHostEnvironment webHostEnvironment,
            IAuditService auditService,
            IPasswordService passwordService,
  ILogger<RegisterModel> logger)
 {
            _userManager = userManager;
         _signInManager = signInManager;
            _roleManager = roleManager;
   _configuration = configuration;
        _webHostEnvironment = webHostEnvironment;
        _auditService = auditService;
       _passwordService = passwordService;
  _logger = logger;
        }

    public void OnGet()
        {
   }

        public async Task<IActionResult> OnPostAsync()
        {
    if (!ModelState.IsValid)
            {
            return Page();
            }

      // Server-side password complexity validation
   if (!ValidatePasswordComplexity(RModel.Password, out string passwordError))
   {
           ModelState.AddModelError("RModel.Password", passwordError);
 return Page();
            }

            // Check if email is unique
            var existingUser = await _userManager.FindByEmailAsync(RModel.Email);
            if (existingUser != null)
     {
            ModelState.AddModelError(string.Empty, "Email address is already registered");
            return Page();
     }

     // Upload resume file
        var resumeUrl = await UploadResumeAsync(RModel.Resume);
     if (resumeUrl == null && RModel.Resume != null)
 {
     return Page();
    }

        // Create user with all registration fields
     var user = new ApplicationUser()
  {
                UserName = RModel.Email,
       Email = RModel.Email,
     FirstName = RModel.FirstName,
       LastName = RModel.LastName,
        Gender = RModel.Gender,
    NRIC = EncryptNRIC(RModel.NRIC),
      DateOfBirth = RModel.DateOfBirth,
           ResumeURL = resumeUrl ?? string.Empty,
                WhoAmI = RModel.WhoAmI,
    PasswordChangedDate = DateTime.UtcNow, // Initialize password changed date
  MustChangePassword = false
         };

            // Ensure roles exist
            await EnsureRolesExistAsync();

       // Create user with password hashing
  var result = await _userManager.CreateAsync(user, RModel.Password);

      if (result.Succeeded)
          {
  _logger.LogInformation("New user registered: {Email}", user.Email);

   // Add initial password to history
  await _passwordService.AddToHistoryAsync(user.Id, user.PasswordHash!);

        // Add user to roles
        await _userManager.AddToRoleAsync(user, "Admin");
        await _userManager.AddToRoleAsync(user, "HR");

        // Log the registration
   await _auditService.LogRegistrationAsync(user.Id, user.Email ?? "");

        // Sign in the user
      await _signInManager.SignInAsync(user, isPersistent: false);

        // Set session data
    HttpContext.Session.SetString("UserId", user.Id);
        HttpContext.Session.SetString("UserEmail", user.Email ?? "");
        HttpContext.Session.SetString("LoginTime", DateTime.UtcNow.ToString("O"));

      // Log the auto-login after registration
        await _auditService.LogLoginAsync(user.Id, user.Email ?? "", true);

      // Initialize SessionVersion
    user.SessionVersion = 1;
    await _userManager.UpdateAsync(user);

    // Create a new principal with the SessionVersion claim
  var principal = await _signInManager.CreateUserPrincipalAsync(user);
    var identity = (ClaimsIdentity)principal.Identity!;
  identity.AddClaim(new Claim(SessionVersionClaimType, user.SessionVersion.ToString()));

    // Sign in with the updated principal
    await HttpContext.SignInAsync(
     IdentityConstants.ApplicationScheme,
principal,
        new AuthenticationProperties
        {
         IsPersistent = false,
            IssuedUtc = DateTimeOffset.UtcNow
        });

    return RedirectToPage("Index");
       }

            // Add errors to model state
  foreach (var error in result.Errors)
      {
      ModelState.AddModelError(string.Empty, error.Description);
  }

            return Page();
        }

        private bool ValidatePasswordComplexity(string password, out string errorMessage)
        {
            errorMessage = string.Empty;

       if (string.IsNullOrEmpty(password))
       {
     errorMessage = "Password is required";
   return false;
            }

    if (password.Length < 12)
   {
          errorMessage = "Password must be at least 12 characters long";
           return false;
        }

  if (!Regex.IsMatch(password, @"[a-z]"))
            {
    errorMessage = "Password must contain at least one lowercase letter (a-z)";
    return false;
            }

        if (!Regex.IsMatch(password, @"[A-Z]"))
            {
  errorMessage = "Password must contain at least one uppercase letter (A-Z)";
    return false;
         }

            if (!Regex.IsMatch(password, @"[0-9]"))
  {
     errorMessage = "Password must contain at least one number (0-9)";
 return false;
         }

  if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
   {
      errorMessage = "Password must contain at least one special character (!@#$%^&*()_+-=[]{}|:;',.<>?)";
      return false;
     }

            return true;
        }

        private string EncryptNRIC(string plainText)
      {
            if (string.IsNullOrEmpty(plainText))
       return plainText;

       var keyString = _configuration["Encryption:Key"] ?? "MySecureKey12345MySecureKey12345";
            var ivString = _configuration["Encryption:IV"] ?? "MySecureIV123456";

            var key = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
   var iv = Encoding.UTF8.GetBytes(ivString.PadRight(16).Substring(0, 16));

         using (var aes = Aes.Create())
     {
                aes.Key = key;
                aes.IV = iv;
           var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

  using (var msEncrypt = new MemoryStream())
    {
       using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
      using (var swEncrypt = new StreamWriter(csEncrypt))
         {
               swEncrypt.Write(plainText);
          }
           return Convert.ToBase64String(msEncrypt.ToArray());
         }
            }
        }

        private async Task<string?> UploadResumeAsync(IFormFile? file)
        {
        if (file == null || file.Length == 0)
     return null;

if (file.Length > 5 * 1024 * 1024)
            {
   ModelState.AddModelError("RModel.Resume", "File size must not exceed 5MB");
        return null;
    }

          var allowedExtensions = new[] { ".pdf", ".docx" };
   var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
      if (!allowedExtensions.Contains(fileExtension))
       {
    ModelState.AddModelError("RModel.Resume", "Only .pdf and .docx files are allowed");
    return null;
         }

     var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "resumes");
     if (!Directory.Exists(uploadsFolder))
         {
   Directory.CreateDirectory(uploadsFolder);
 }

     var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

   using (var fileStream = new FileStream(filePath, FileMode.Create))
     {
                await file.CopyToAsync(fileStream);
        }

  return $"/uploads/resumes/{uniqueFileName}";
        }

     private async Task EnsureRolesExistAsync()
        {
          if (!await _roleManager.RoleExistsAsync("Admin"))
      {
            await _roleManager.CreateAsync(new IdentityRole("Admin"));
       }

       if (!await _roleManager.RoleExistsAsync("HR"))
        {
             await _roleManager.CreateAsync(new IdentityRole("HR"));
            }
        }
    }
}
