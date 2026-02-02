# AceJob Security Implementation Documentation

> **Project:** AceJob Web Application  
> **Framework:** ASP.NET Core 8.0 (Razor Pages)  
> **Database:** SQL Server with Entity Framework Core  
> **Last Updated:** February 2025

This document provides a comprehensive overview of how each security checklist item has been implemented in the AceJob application, including the specific code files, methods, and data flow involved.

---

## Table of Contents

1. [Registration and User Data Management](#1-registration-and-user-data-management)
2. [Session Management](#2-session-management)
3. [Login/Logout Security](#3-loginlogout-security)
4. [Anti-Bot Protection](#4-anti-bot-protection)
5. [Input Validation and Sanitization](#5-input-validation-and-sanitization)
6. [Error Handling](#6-error-handling)
7. [Security Architecture Diagram](#7-security-architecture-diagram)

---

## 1. Registration and User Data Management

### 1.1 Implement Successful Saving of Member Info into the Database

**Status:** ? Implemented

**Files Involved:**
- `Pages/Register.cshtml` - Registration form UI
- `Pages/Register.cshtml.cs` - Registration logic
- `ViewModels/Register.cs` - Form validation model
- `Model/ApplicationUser.cs` - User entity model
- `Model/AuthDbContext.cs` - Database context

**Implementation Flow:**

```
User fills form ¡ú Register.cshtml
        ¡ý
Form POST ¡ú Register.cshtml.cs OnPostAsync()
 ¡ý
Validate ModelState ¡ú Check all [Required] attributes
        ¡ý
Create ApplicationUser object with all fields
        ¡ý
_userManager.CreateAsync(user, password) ¡ú Saves to AspNetUsers table
        ¡ý
Redirect to Index page (logged in)
```

**Code Implementation:**

```csharp
// Pages/Register.cshtml.cs - OnPostAsync()
var user = new ApplicationUser()
{
    UserName = RModel.Email,
    Email = RModel.Email,
    FirstName = RModel.FirstName,
    LastName = RModel.LastName,
    Gender = RModel.Gender,
    NRIC = EncryptNRIC(RModel.NRIC),  // Encrypted before storage
    DateOfBirth = RModel.DateOfBirth,
    ResumeURL = resumeUrl ?? string.Empty,
    WhoAmI = RModel.WhoAmI,
    PasswordChangedDate = DateTime.UtcNow,
    MustChangePassword = false
};

var result = await _userManager.CreateAsync(user, RModel.Password);
```

**Database Table:** `AspNetUsers` (Extended with custom fields)

| Column | Type | Description |
|--------|------|-------------|
| Id | nvarchar(450) | Primary key (GUID) |
| Email | nvarchar(256) | User email |
| FirstName | nvarchar(max) | First name |
| LastName | nvarchar(max) | Last name |
| Gender | nvarchar(max) | Male/Female |
| NRIC | nvarchar(max) | **Encrypted** national ID |
| DateOfBirth | date | Birth date |
| ResumeURL | nvarchar(max) | Path to uploaded resume |
| WhoAmI | nvarchar(max) | User bio |
| PasswordHash | nvarchar(max) | **Hashed** password |
| SessionVersion | int | For concurrent login detection |

---

### 1.2 Check for Duplicate Email Addresses

**Status:** ? Implemented

**Files Involved:**
- `Pages/Register.cshtml.cs`
- `Program.cs` (Identity configuration)

**Implementation:**

**Method 1: Manual Check in Registration**
```csharp
// Pages/Register.cshtml.cs - OnPostAsync()
var existingUser = await _userManager.FindByEmailAsync(RModel.Email);
if (existingUser != null)
{
    ModelState.AddModelError(string.Empty, "Email address is already registered");
    return Page();
}
```

**Method 2: Identity Configuration (Belt-and-Suspenders)**
```csharp
// Program.cs
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;  // Enforced at Identity level
})
```

**User Experience:**
- If duplicate email detected, form displays: *"Email address is already registered"*

---

### 1.3 Strong Password Requirements

**Status:** ? Implemented (Both Client-Side and Server-Side)

#### 1.3.1 Identity Configuration (Server-Side Enforcement)

**File:** `Program.cs`

```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings - Enhanced security (Min 12 chars)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 3;
})
```

| Requirement | Value | Enforced By |
|-------------|-------|-------------|
| Minimum Length | 12 characters | Identity + Custom validation |
| Lowercase Letter | Required | Identity + Regex |
| Uppercase Letter | Required | Identity + Regex |
| Digit | Required | Identity + Regex |
| Special Character | Required | Identity + Regex |
| Unique Characters | 3 minimum | Identity |

#### 1.3.2 Custom Server-Side Validation

**File:** `Pages/Register.cshtml.cs`

```csharp
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
        errorMessage = "Password must contain at least one special character";
        return false;
    }

    return true;
}
```

#### 1.3.3 Client-Side Password Strength Feedback

**File:** `Pages/Register.cshtml`

```javascript
$('#password').on('keyup', function () {
    var password = $(this).val();
    var feedback = $('#password-strength-feedback');
    var strength = 0;
    var messages = [];

    if (password.length >= 12) { strength++; } 
    else { messages.push('at least 12 characters'); }

    if (password.match(/[a-z]/)) { strength++; } 
    else { messages.push('a lowercase letter'); }

    if (password.match(/[A-Z]/)) { strength++; } 
 else { messages.push('an uppercase letter'); }

    if (password.match(/[0-9]/)) { strength++; } 
    else { messages.push('a number'); }

    if (password.match(/[^a-zA-Z0-9]/)) { strength++; } 
    else { messages.push('a special character'); }

    var strengthText = 'Password strength: ';
    if (strength < 3) {
   feedback.css('color', 'red');
        strengthText += 'Weak. ';
    } else if (strength < 5) {
        feedback.css('color', 'orange');
     strengthText += 'Medium. ';
    } else {
        feedback.css('color', 'green');
        strengthText += 'Strong.';
    }

    if (messages.length > 0) {
        strengthText += ' Missing: ' + messages.join(', ');
    }

  feedback.html(strengthText);
});
```

**Visual Feedback:**
- **Red (Weak):** < 3 requirements met
- **Orange (Medium):** 3-4 requirements met
- **Green (Strong):** All 5 requirements met

---

### 1.4 Encrypt Sensitive User Data (NRIC)

**Status:** ? Implemented using AES-256 Encryption

**Files Involved:**
- `Pages/Register.cshtml.cs` - Encryption on save
- `Helpers/EncryptionHelper.cs` - Reusable encryption/decryption
- `Pages/Index.cshtml.cs` - Decryption for display
- `appsettings.json` - Encryption keys

**Encryption Flow:**

```
User enters NRIC: "S1234567A"
  ¡ý
EncryptNRIC() in Register.cshtml.cs
        ¡ý
AES-256 encryption with key + IV
        ¡ý
Base64 encoded: "kJ8vH3nF2mP9xQ1wZ..."
        ¡ý
Stored in AspNetUsers.NRIC column
```

**Encryption Implementation:**

```csharp
// Pages/Register.cshtml.cs
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
```

**Decryption for Display:**

```csharp
// Helpers/EncryptionHelper.cs
public static string Decrypt(string cipherText, string key, string iv)
{
    if (string.IsNullOrEmpty(cipherText))
        return cipherText;

    var keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
    var ivBytes = Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));

  using (var aes = Aes.Create())
    {
        aes.Key = keyBytes;
     aes.IV = ivBytes;
        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

        using (var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
    using (var srDecrypt = new StreamReader(csDecrypt))
        {
            return srDecrypt.ReadToEnd();
        }
    }
}

// Mask for safe display: S1234567A ¡ú *****567A
public static string MaskNRIC(string nric)
{
    if (string.IsNullOrEmpty(nric) || nric.Length < 4)
 return nric;

    return new string('*', nric.Length - 4) + nric.Substring(nric.Length - 4);
}
```

**Configuration:**

```json
// appsettings.json
{
  "Encryption": {
    "Key": "YourSecureEncryptionKey123456789",
    "IV": "YourSecureIV1234"
  }
}
```

---

### 1.5 Proper Password Hashing and Storage

**Status:** ? Implemented via ASP.NET Core Identity

**Implementation:**

ASP.NET Core Identity automatically handles password hashing using **PBKDF2 with HMAC-SHA256** (10,000+ iterations).

```csharp
// Password hashing happens automatically in:
var result = await _userManager.CreateAsync(user, RModel.Password);

// The raw password is NEVER stored. Only the hash:
// Example hash: "AQAAAAIAAYagAAAAEBdD7mN4a+Nj5xF..."
```

**Security Properties:**
- Passwords are **never stored in plain text**
- Uses **salted hashes** (salt is embedded in the hash)
- Industry-standard **PBKDF2** algorithm
- Resistant to rainbow table attacks

---

### 1.6 File Upload Restrictions

**Status:** ? Implemented (.pdf and .docx only, 5MB max)

**File:** `Pages/Register.cshtml.cs`

```csharp
private async Task<string?> UploadResumeAsync(IFormFile? file)
{
    if (file == null || file.Length == 0)
    return null;

    // Size validation: Max 5MB
    if (file.Length > 5 * 1024 * 1024)
    {
      ModelState.AddModelError("RModel.Resume", "File size must not exceed 5MB");
        return null;
    }

    // Extension validation: Only .pdf and .docx
    var allowedExtensions = new[] { ".pdf", ".docx" };
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(fileExtension))
{
        ModelState.AddModelError("RModel.Resume", "Only .pdf and .docx files are allowed");
        return null;
    }

    // Generate unique filename to prevent overwrites and path traversal
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
```

**Security Measures:**
| Measure | Implementation |
|---------|----------------|
| File type restriction | `.pdf` and `.docx` only |
| Size limit | 5MB maximum |
| Path traversal prevention | `Path.GetFileName()` strips directory |
| Unique naming | GUID prefix prevents overwrites |
| Separate upload folder | `/wwwroot/uploads/resumes/` |

---

## 2. Session Management

### 2.1 Create Secure Session Upon Successful Login

**Status:** ? Implemented

**Files Involved:**
- `Program.cs` - Session configuration
- `Pages/Login.cshtml.cs` - Session creation
- `Pages/Register.cshtml.cs` - Session creation on auto-login

**Session Configuration:**

```csharp
// Program.cs
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;      // Prevents JavaScript access (XSS protection)
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;     // HTTPS only in production
});
```

**Session Data Stored on Login:**

```csharp
// Pages/Login.cshtml.cs - AttemptSignInAsync()
HttpContext.Session.SetString("UserId", user.Id);
HttpContext.Session.SetString("UserEmail", user.Email ?? "");
HttpContext.Session.SetString("LoginTime", DateTime.UtcNow.ToString("O"));
```

**Cookie Security Configuration:**

```csharp
// Program.cs - Authentication cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AceJobAuth";
    options.Cookie.HttpOnly = true;           // XSS protection
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Strict;  // CSRF protection
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
    options.AccessDeniedPath = "/AccessDenied";
});
```

---

### 2.2 Session Timeout Implementation

**Status:** ? Implemented (30-minute idle timeout with redirect)

**Files Involved:**
- `Program.cs` - Timeout configuration
- `Middleware/SessionTimeoutMiddleware.cs` - Redirect logic

**Configuration:**

```csharp
// Program.cs
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);  // Session expires after 30 min idle
});
```

**Middleware Registration:**

```csharp
// Program.cs - Middleware pipeline
app.UseSession();
app.UseMiddleware<SessionTimeoutMiddleware>();  // Detects expired sessions
app.UseAuthentication();
```

---

### 2.3 Redirect to Login After Session Timeout

**Status:** ? Implemented via Custom Middleware

**File:** `Middleware/SessionTimeoutMiddleware.cs`

```csharp
public sealed class SessionTimeoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionTimeoutMiddleware> _logger;
    private const string UserIdKey = "UserId";

    // Paths to skip (avoid redirect loops)
    private static readonly PathString[] BypassPaths =
    [
        "/Login", "/Logout", "/Register", "/Error", "/AccessDenied", "/NotFound"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to authenticated users
    if (context.User?.Identity?.IsAuthenticated == true)
      {
     var path = context.Request.Path;

      // Skip static files and auth pages
  if (!BypassPaths.Any(p => path.StartsWithSegments(p))
           && !path.StartsWithSegments("/css")
   && !path.StartsWithSegments("/js"))
        {
        // If session marker is missing, session has expired
   if (string.IsNullOrEmpty(context.Session.GetString(UserIdKey)))
   {
 _logger.LogInformation("Session expired. Redirecting to /Login");

           context.Session.Clear();
         await context.SignOutAsync();

             var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/Login?reason=session-timeout&returnUrl={Uri.EscapeDataString(returnUrl)}");
     return;
  }
            }
  }

      await _next(context);
    }
}
```

**Flow Diagram:**

```
Authenticated user makes request
        ¡ý
SessionTimeoutMiddleware checks Session["UserId"]
        ¡ý
    ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
    ©¦ Session["UserId"] exists?      ©¦
    ©¦   YES ¡ú Continue to page          ©¦
    ©¦   NO  ¡ú Session expired        ©¦
  ©¦ ¡ý                ©¦
    ©¦         Clear session      ©¦
    ©¦      Sign out user          ©¦
    ©¦Redirect to /Login        ©¦
    ©¦         ?reason=session-timeout       ©¦
    ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
```

---

### 2.4 Detect and Handle Multiple Logins (Concurrent Sessions)

**Status:** ? Implemented

**Files Involved:**
- `Model/ApplicationUser.cs` - SessionVersion property
- `Pages/Login.cshtml.cs` - Increment version on login
- `Pages/Register.cshtml.cs` - Initialize version on registration
- `Middleware/ConcurrentLoginMiddleware.cs` - Detection and kick-out logic

**Mechanism Overview:**

```
User logs in on Device A
    ¡ú SessionVersion = 1, Cookie contains claim "sv=1"
    
User logs in on Device B
    ¡ú SessionVersion = 2, Cookie contains claim "sv=2"
    
Device A makes next request
    ¡ú Middleware checks: Cookie sv=1 vs DB SessionVersion=2
    ¡ú Mismatch detected ¡ú User kicked out ¡ú Redirect to /Login
```

**Database Column:**

```csharp
// Model/ApplicationUser.cs
public class ApplicationUser : IdentityUser
{
    /// <summary>
  /// Incremented on each login. Used to detect and invalidate older sessions.
    /// </summary>
    public int SessionVersion { get; set; } = 0;
}
```

**Login Process - Increment Version:**

```csharp
// Pages/Login.cshtml.cs - AttemptSignInAsync()
if (result.Succeeded)
{
    // Increment SessionVersion to invalidate all other sessions
    user.SessionVersion++;
    await _userManager.UpdateAsync(user);

    // Re-issue authentication cookie with updated SessionVersion claim
    var principal = await _signInManager.CreateUserPrincipalAsync(user);
    var identity = (ClaimsIdentity)principal.Identity!;
    identity.AddClaim(new Claim("sv", user.SessionVersion.ToString()));

    await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
}
```

**Middleware Detection:**

```csharp
// Middleware/ConcurrentLoginMiddleware.cs
public async Task InvokeAsync(HttpContext context)
{
    if (context.User?.Identity?.IsAuthenticated != true)
    {
        await _next(context);
        return;
    }

    var userId = _userManager.GetUserId(context.User);
    var claim = context.User.FindFirst("sv")?.Value;
    
 if (!int.TryParse(claim, out var tokenSessionVersion))
    {
     await KickAsync(context, userId, "Missing session version claim");
 return;
    }

    var user = await _userManager.FindByIdAsync(userId);
    
    // If cookie's version doesn't match DB, another login occurred
    if (tokenSessionVersion != user.SessionVersion)
    {
        await KickAsync(context, userId, "Logged in elsewhere");
      return;
    }

    await _next(context);
}

private async Task KickAsync(HttpContext context, string userId, string reason)
{
    await _auditService.LogAsync(AuditActions.ConcurrentLogin, userId, email,
        $"Signed out due to concurrent login. {reason}", isSuccess: true);

    context.Session.Clear();
    await context.SignOutAsync();

    context.Response.Redirect("/Login?reason=concurrent-login");
}
```

**Audit Log Entry:**
- Action: `ConcurrentLogin`
- Description: "Signed out due to concurrent login. SessionVersion mismatch (logged in elsewhere)"

---

## 3. Login/Logout Security

### 3.1 Proper Login Functionality

**Status:** ? Implemented

**Files Involved:**
- `Pages/Login.cshtml` - Login form UI
- `Pages/Login.cshtml.cs` - Login logic
- `ViewModels/Login.cs` - Form model

**Login Flow:**

```
User submits email + password
        ¡ý
Validate reCAPTCHA token (if enabled)
  ¡ý
Check if account is locked out
  ¡ý
_signInManager.PasswordSignInAsync()
        ¡ý
    ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
 ©¦ Result:       ©¦
    ©¦   Succeeded ¡ú Set session ¡ú Index   ©¦
    ©¦   RequiresTwoFactor ¡ú 2FA page      ©¦
    ©¦   IsLockedOut ¡ú Show lockout msg    ©¦
    ©¦   Failed ¡ú Show attempts remaining  ©¦
    ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
```

---

### 3.2 Rate Limiting (Account Lockout)

**Status:** ? Implemented (3 attempts, 15-minute lockout)

**File:** `Program.cs`

```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Lockout settings - Protection against brute force
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.AllowedForNewUsers = true;
})
```

**Login Enforcement:**

```csharp
// Pages/Login.cshtml.cs
var result = await _signInManager.PasswordSignInAsync(
    user.UserName!,
    LModel.Password,
    LModel.RememberMe,
    lockoutOnFailure: true  // Enable lockout tracking
);

// Check if already locked out before attempt
if (await _userManager.IsLockedOutAsync(user))
{
  var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
    var remainingTime = (int)Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes);
    
    ModelState.AddModelError(string.Empty, 
        $"Account locked. Try again in {remainingTime} minutes.");
return Page();
}

// Show remaining attempts
if (!result.Succeeded && !result.IsLockedOut)
{
    var failedCount = await _userManager.GetAccessFailedCountAsync(user);
    var remainingAttempts = 3 - failedCount;
    
    ModelState.AddModelError(string.Empty, 
        $"Invalid login. {remainingAttempts} attempt(s) remaining before lockout.");
}
```

| Setting | Value |
|---------|-------|
| Max Failed Attempts | 3 |
| Lockout Duration | 15 minutes |
| Auto-Recovery | Yes (after lockout period) |

---

### 3.3 Safe Logout

**Status:** ? Implemented

**File:** `Pages/Logout.cshtml.cs`

```csharp
public async Task<IActionResult> OnPostAsync()
{
    // Get user info before signing out (for audit log)
    var user = await _userManager.GetUserAsync(User);
    var userId = user?.Id;
    var userEmail = user?.Email;

    // Log the logout event
    if (userId != null && userEmail != null)
    {
        await _auditService.LogLogoutAsync(userId, userEmail);
    }

    // Clear all session data
    HttpContext.Session.Clear();

    // Sign out from Identity (delete auth cookie)
    await _signInManager.SignOutAsync();

    // Delete any additional cookies
    foreach (var cookie in Request.Cookies.Keys)
    {
        Response.Cookies.Delete(cookie);
    }

 // Redirect to login page
    return RedirectToPage("/Login");
}
```

**Logout Checklist:**
- ? Log logout event to audit
- ? Clear session data
- ? Sign out from Identity
- ? Delete all cookies
- ? Redirect to login page

---

### 3.4 Audit Logging

**Status:** ? Implemented

**Files Involved:**
- `Model/AuditLog.cs` - Audit log entity
- `Services/AuditService.cs` - Audit logging service
- All login/logout/registration pages

**Audit Log Entity:**

```csharp
public class AuditLog
{
    public int Id { get; set; }
 public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Action { get; set; }  // Login, Logout, FailedLogin, etc.
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsSuccess { get; set; }
    public string? AdditionalData { get; set; }
}
```

**Logged Actions:**

| Action | When | Success |
|--------|------|---------|
| `Login` | Successful login | ? |
| `FailedLogin` | Invalid credentials | ? |
| `Lockout` | Account locked after 3 failures | ? |
| `Logout` | User logs out | ? |
| `Registration` | New user registers | ? |
| `ProfileView` | User views home page | ? |
| `PasswordChange` | Password changed | ?/? |
| `ConcurrentLogin` | Session invalidated by new login | ? |

**Example Audit Log Query:**

```sql
SELECT Action, UserEmail, Description, IpAddress, Timestamp, IsSuccess
FROM AuditLogs
WHERE UserEmail = 'user@example.com'
ORDER BY Timestamp DESC;
```

---

### 3.5 Redirect to Homepage After Login

**Status:** ? Implemented

**File:** `Pages/Login.cshtml.cs`

```csharp
if (result.Succeeded)
{
    // ... session setup ...
    return RedirectToPage("/Index");  // Redirect to home page
}
```

**Home Page (Index) Displays:**

```csharp
// Pages/Index.cshtml.cs
public async Task OnGetAsync()
{
    if (User.Identity?.IsAuthenticated == true)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
 {
            UserFirstName = user.FirstName;
    UserLastName = user.LastName;
            UserEmail = user.Email ?? "";
            UserGender = user.Gender;
            DateOfBirth = user.DateOfBirth;
          WhoAmI = user.WhoAmI;
 ResumeURL = user.ResumeURL;

      // Decrypt NRIC for display
    DecryptedNRIC = EncryptionHelper.Decrypt(user.NRIC, key, iv);
          MaskedNRIC = EncryptionHelper.MaskNRIC(DecryptedNRIC);

          // Get recent activity
 RecentActivity = await _auditService.GetUserAuditLogsAsync(user.Id, 5);
        }
    }
}
```

---

## 4. Anti-Bot Protection

### 4.1 Google reCAPTCHA v3

**Status:** ? Implemented (on Login page)

**Files Involved:**
- `Pages/Login.cshtml` - Client-side reCAPTCHA integration
- `Pages/Login.cshtml.cs` - Server-side validation
- `Services/RecaptchaService.cs` - Token verification service
- `appsettings.json` - Configuration

**Client-Side Integration:**

```html
<!-- Pages/Login.cshtml -->
<script src="https://www.google.com/recaptcha/api.js?render=@siteKey"></script>

<input type="hidden" asp-for="LModel.RecaptchaToken" id="recaptchaToken" />

<script>
grecaptcha.ready(function () {
    grecaptcha.execute(config.siteKey, { action: 'login' })
        .then(function (token) {
     document.getElementById('recaptchaToken').value = token;
        });
});
</script>
```

**Server-Side Validation:**

```csharp
// Pages/Login.cshtml.cs
private async Task<bool> ValidateRecaptchaAsync()
{
    var recaptchaEnabled = _configuration.GetValue<bool>("RecaptchaSettings:Enabled", true);
    if (!recaptchaEnabled) return true;

    if (string.IsNullOrEmpty(LModel.RecaptchaToken))
    {
        ModelState.AddModelError(string.Empty, "Security verification is missing.");
      return false;
    }

    var recaptchaResult = await _recaptchaService.VerifyToken(LModel.RecaptchaToken);

 if (!recaptchaResult.Success)
    {
        ModelState.AddModelError(string.Empty, "Security verification failed.");
     return false;
    }

    // Check score threshold (0.0 = bot, 1.0 = human)
    var minScore = _configuration.GetValue<double>("RecaptchaSettings:MinScore", 0.5);
    if (recaptchaResult.Score < minScore)
    {
        ModelState.AddModelError(string.Empty, "Security verification failed.");
   return false;
    }

  return true;
}
```

**RecaptchaService:**

```csharp
// Services/RecaptchaService.cs
public async Task<RecaptchaResponse> VerifyToken(string token)
{
    var parameters = new Dictionary<string, string>
    {
    { "secret", _secretKey },
     { "response", token }
    };

    var content = new FormUrlEncodedContent(parameters);
    var response = await httpClient.PostAsync(
        "https://www.google.com/recaptcha/api/siteverify", content);

    var jsonResponse = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<RecaptchaResponse>(jsonResponse);
}
```

**Configuration:**

```json
// appsettings.json
{
  "RecaptchaSettings": {
    "SiteKey": "6Lc...",
    "SecretKey": "6Lc...",
    "Enabled": true,
    "MinScore": 0.5
  }
}
```

| Score Range | Interpretation |
|-------------|----------------|
| 0.9 - 1.0 | Definitely human |
| 0.7 - 0.9 | Likely human |
| 0.5 - 0.7 | Possibly human |
| < 0.5 | Likely bot (blocked) |

---

## 5. Input Validation and Sanitization

### 5.1 SQL Injection Prevention

**Status:** ? Implemented via Entity Framework Core

**Implementation:**

Entity Framework Core uses **parameterized queries** by default, which prevents SQL injection attacks.

```csharp
// All database queries use EF Core:
var user = await _userManager.FindByEmailAsync(email);  // Parameterized
var logs = _context.AuditLogs.Where(a => a.UserId == userId).ToList();  // Parameterized

// NEVER concatenating user input into SQL strings
```

**Example of Safe Query:**

```csharp
// This generates a parameterized query:
var user = await _context.Users
    .Where(u => u.Email == userInput)
    .FirstOrDefaultAsync();

// Generated SQL:
// SELECT * FROM AspNetUsers WHERE Email = @p0
// @p0 is parameterized, not concatenated
```

---

### 5.2 CSRF Protection

**Status:** ? Implemented (Automatic in Razor Pages)

**Implementation:**

Razor Pages automatically includes anti-forgery tokens in all forms.

```html
<!-- Automatically added by Razor Pages -->
<form method="post">
    <input name="__RequestVerificationToken" type="hidden" value="CfDJ8..." />
    <!-- form fields -->
</form>
```

**Configuration:**

```csharp
// Program.cs - Cookie settings
options.Cookie.SameSite = SameSiteMode.Strict;  // Additional CSRF protection
```

---

### 5.3 XSS Prevention

**Status:** ? Implemented via Razor Encoding

**Implementation:**

Razor automatically HTML-encodes all output using `@` syntax:

```html
<!-- Razor automatically encodes this -->
<p>@Model.UserFirstName</p>

<!-- If UserFirstName = "<script>alert('xss')</script>" -->
<!-- Output: &lt;script&gt;alert('xss')&lt;/script&gt; -->
```

**Additional Protection:**

```csharp
// Cookies are HttpOnly (can't be accessed by JavaScript)
options.Cookie.HttpOnly = true;
```

---

### 5.4 Input Validation (Client-Side and Server-Side)

**Status:** ? Implemented

**Server-Side Validation (Data Annotations):**

```csharp
// ViewModels/Register.cs
public class Register
{
    [Required]
    [DataType(DataType.Text)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; }
    
    [Required]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; }

  [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; }

    [StringLength(500, ErrorMessage = "Maximum 500 characters")]
    public string WhoAmI { get; set; }
}
```

**Server-Side Validation Check:**

```csharp
// Pages/Register.cshtml.cs
public async Task<IActionResult> OnPostAsync()
{
    if (!ModelState.IsValid)
    {
        return Page();  // Return with validation errors
    }
    // ... proceed with registration
}
```

**Client-Side Validation:**

```html
<!-- Pages/Register.cshtml -->
@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

The `_ValidationScriptsPartial` includes jQuery Validation which enforces the same rules on the client side.

---

### 5.5 Error Messages for Invalid Input

**Status:** ? Implemented

```html
<!-- Display all validation errors -->
<div asp-validation-summary="All" class="text-danger"></div>

<!-- Display field-specific errors -->
<input asp-for="RModel.Email" class="form-control" />
<span asp-validation-for="RModel.Email" class="text-danger"></span>
```

**Error Messages Shown:**
- "The Email field is required."
- "Password and confirmation password does not match"
- "Only .pdf and .docx files are allowed"
- "File size must not exceed 5MB"
- "Password must be at least 12 characters long"

---

## 6. Error Handling

### 6.1 Graceful Error Handling

**Status:** ? Implemented

**File:** `Program.cs`

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();  // Detailed errors in dev
}
else
{
    app.UseExceptionHandler("/Error");  // Custom error page in production
    app.UseHsts();
}

// Handle HTTP status code errors
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
```

---

### 6.2 Custom Error Pages

**Status:** ? Implemented

**Files:**
- `Pages/Error.cshtml` - Universal error page
- `Pages/Error.cshtml.cs` - Error handling logic
- `Pages/AccessDenied.cshtml` - 403 Forbidden page
- `Pages/NotFound.cshtml` - 404 Not Found page

**Error Page Logic:**

```csharp
// Pages/Error.cshtml.cs
public void OnGet(string? code = null)
{
    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    ErrorCode = code ?? HttpContext.Response.StatusCode.ToString();
    ErrorMessage = GetErrorMessage(ErrorCode);
}

private static string GetErrorMessage(string code)
{
    return code switch
    {
        "400" => "The request was invalid or cannot be processed.",
        "401" => "You need to be logged in to access this resource.",
        "403" => "You don't have the required permissions.",
        "404" => "The page you requested could not be found.",
        "500" => "An internal server error occurred.",
        "503" => "The service is temporarily unavailable.",
        _ => "An unexpected error occurred."
    };
}
```

**Supported Error Codes:**

| Code | Name | User Message |
|------|------|--------------|
| 400 | Bad Request | Invalid request format |
| 401 | Unauthorized | Login required |
| 403 | Forbidden | Permission denied |
| 404 | Not Found | Page doesn't exist |
| 500 | Server Error | Internal error |
| 503 | Unavailable | Temporary maintenance |

---

## 7. Security Architecture Diagram

```
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦   CLIENT (BROWSER)                ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦©¦ Login/Register Forms             ©¦   ©¦
©¦  ©¦ ? jQuery Validation (client-side)           ©¦   ©¦
©¦  ©¦ ? Password strength indicator           ©¦   ©¦
©¦  ©¦ ? reCAPTCHA v3 token generation        ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼   ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©Ð©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
    ©¦ HTTPS
  ¨‹
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦           ASP.NET CORE MIDDLEWARE PIPELINE     ©¦
©¦         ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦  ©¦   Session    ©¦¡ú ©¦ SessionTimeout©¦¡ú ©¦Authentication©¦¡ú ©¦ Concurrent  ©¦   ©¦
©¦  ©¦  Middleware  ©¦  ©¦  Middleware   ©¦  ©¦  Middleware  ©¦  ©¦   Login     ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼   ©¦
©¦         ©¦       ©¦           ©¦        ©¦          ©¦
©¦         ¨‹      ¨‹      ¨‹       ¨‹        ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦  ©¦        AUTHORIZATION MIDDLEWARE        ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼   ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©Ð©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
      ©¦
      ¨‹
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦            RAZOR PAGES        ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦  ©¦ Login.cshtml.cs    ©¦   ©¦
©¦  ©¦ ? reCAPTCHA validation       ©¦   ©¦
©¦  ©¦ ? Identity SignInAsync   ©¦   ©¦
©¦  ©¦ ? Lockout check    ©¦   ©¦
©¦  ©¦ ? Session creation        ©¦   ©¦
©¦  ©¦ ? SessionVersion increment        ©¦ ©¦
©¦  ©¦ ? Audit logging     ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼   ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦  ©¦ Register.cshtml.cs  ©¦   ©¦
©¦  ©¦ ? ModelState validation    ©¦   ©¦
©¦  ©¦ ? Password complexity check       ©¦   ©¦
©¦  ©¦ ? Duplicate email check     ©¦   ©¦
©¦  ©¦ ? NRIC encryption (AES-256)          ©¦   ©¦
©¦  ©¦ ? File upload validation            ©¦   ©¦
©¦  ©¦ ? Identity CreateAsync (password hashing)          ©¦   ©¦
©¦  ©¦ ? Audit logging        ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©Ð©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
               ©¦
               ¨‹
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦      SERVICES   ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´                ©¦
©¦  ©¦ RecaptchaService©¦  ©¦ AuditService   ©¦  ©¦PasswordService ©¦    ©¦
©¦  ©¦ ? Google API    ©¦  ©¦ ? Log actions  ©¦  ©¦ ? History check©¦     ©¦
©¦  ©¦ ? Score check   ©¦  ©¦ ? IP tracking  ©¦  ©¦ ? Age policies ©¦©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼         ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©Ð©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
    ©¦
  ¨‹
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦           DATABASE (SQL Server)         ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦  ©¦ AspNetUsers              ©¦©¦
©¦  ©¦ ? PasswordHash (PBKDF2-HMAC-SHA256)      ©¦   ©¦
©¦  ©¦ ? NRIC (AES-256 encrypted)             ©¦   ©¦
©¦  ©¦ ? SessionVersion (concurrent login detection)           ©¦   ©¦
©¦  ©¦ ? LockoutEnd (rate limiting)     ©¦   ©¦
©¦  ©¦ ? AccessFailedCount (failed attempts)    ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼   ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦  ©¦ AuditLogs  ©¦   ©¦
©¦  ©¦ ? All user actions logged  ©¦   ©¦
©¦  ©¦ ? IP address, user agent, timestamp      ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼   ©¦
©¦  ©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´   ©¦
©¦  ©¦ PasswordHistories           ©¦   ©¦
©¦  ©¦ ? Last 2 password hashes           ©¦   ©¦
©¦  ©¦ ? Prevents password reuse             ©¦   ©¦
©¦  ©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼   ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
```

---

## Summary: Security Checklist Status

| Category | Item | Status |
|----------|------|--------|
| **Registration** | Save member info to database | ? |
| | Check duplicate emails | ? |
| | 12+ character passwords | ? |
| | Password complexity (upper, lower, digit, special) | ? |
| | Password strength feedback (client-side) | ? |
| | Client + server password validation | ? |
| | Encrypt NRIC (AES-256) | ? |
| | Password hashing (PBKDF2) | ? |
| | File upload restrictions (.pdf, .docx, 5MB) | ? |
| **Session** | Secure session on login | ? |
| | 30-minute idle timeout | ? |
| | Redirect to login after timeout | ? |
| | Detect concurrent logins | ? |
| **Login/Logout** | Proper login functionality | ? |
| | Rate limiting (3 attempts, 15-min lockout) | ? |
| | Safe logout (clear session, cookies) | ? |
| | Audit logging | ? |
| | Redirect to home after login | ? |
| **Anti-Bot** | Google reCAPTCHA v3 | ? |
| **Validation** | SQL injection prevention (EF Core) | ? |
| | CSRF protection (anti-forgery tokens) | ? |
| | XSS prevention (Razor encoding) | ? |
| | Input sanitization/validation | ? |
| | Client + server validation | ? |
| | Error messages for invalid input | ? |
| **Error Handling** | Graceful error handling | ? |
| | Custom error pages (404, 403, 500) | ? |

---

**Document Version:** 1.0  
**Last Updated:** February 2025  
**Author:** AceJob Development Team
