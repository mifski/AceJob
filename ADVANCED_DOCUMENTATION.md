# AceJob Advanced Features Documentation

> **Last Updated:** January 2025  
> **Framework:** ASP.NET Core 8.0 (Razor Pages)  
> **Database:** SQL Server LocalDB  

---

## Table of Contents

1. [Two-Factor Authentication (2FA) Implementation](#two-factor-authentication-2fa-implementation)
2. [Change Password Feature Implementation](#change-password-feature-implementation)
3. [Password History Management](#password-history-management)
4. [Security Architecture](#security-architecture)
5. [Database Schema](#database-schema)
6. [Technical Implementation Details](#technical-implementation-details)
7. [Testing Scenarios](#testing-scenarios)

---

## Two-Factor Authentication (2FA) Implementation

### Overview

The 2FA system uses **Time-based One-Time Passwords (TOTP)** compatible with standard authenticator apps like Google Authenticator, Microsoft Authenticator, and Authy. The implementation follows RFC 6238 standards and provides enterprise-grade security.

### Architecture Components

```
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦    2FA System Architecture    ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Frontend (Razor Pages)             ©¦
©¦  ©À©¤©¤ TwoFactorAuth.cshtml  - Setup & Management UI        ©¦
©¦  ©À©¤©¤ LoginWith2fa.cshtml        - 2FA Code Entry ©¦
©¦  ©¸©¤©¤ LoginWithRecoveryCode.cshtml - Recovery Code Entry   ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Backend (PageModels & Services)            ©¦
©¦  ©À©¤©¤ TwoFactorAuthModel         - Setup/Enable/Disable Logic   ©¦
©¦  ©À©¤©¤ LoginWith2faModel          - Authentication Logic         ©¦
©¦  ©À©¤©¤ LoginWithRecoveryCodeModel - Recovery Logic          ©¦
©¦  ©¸©¤©¤ AuditService      - Security Logging   ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Database (SQL Server)            ©¦
©¦  ©À©¤©¤ AspNetUsers    - 2FA enabled flags     ©¦
©¦  ©À©¤©¤ AspNetUserTokens      - Authenticator keys           ©¦
©¦  ©¸©¤©¤ AuditLogs  - Security audit trail         ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
```

### 2FA Setup Process

#### Step 1: Initiate Setup
```csharp
// TwoFactorAuthModel.OnPostSetupAsync()
public async Task<IActionResult> OnPostSetupAsync()
{
    var user = await _userManager.GetUserAsync(User);
    
    // Reset authenticator key to generate new one
    await _userManager.ResetAuthenticatorKeyAsync(user);
    var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
    
    // Format key for display (groups of 4 characters)
    SharedKey = FormatKey(unformattedKey!);
    
    // Generate QR code URL
    QrCodeUrl = GenerateQrCodeUri(user.Email!, unformattedKey!);
    
    return Page();
}
```

#### Step 2: Key Generation & Formatting
```csharp
private static string FormatKey(string unformattedKey)
{
    var result = new StringBuilder();
    var currentPosition = 0;
    
    // Format as groups of 4 characters: "abcd efgh ijkl..."
    while (currentPosition + 4 < unformattedKey.Length)
    {
        result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
      currentPosition += 4;
    }
 
    return result.ToString().ToLowerInvariant();
}
```

#### Step 3: QR Code Generation
```csharp
private string GenerateQrCodeUri(string email, string unformattedKey)
{
    // TOTP URI format: otpauth://totp/Issuer:Account?secret=Key&issuer=Issuer
    var uri = string.Format(
        "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
        _urlEncoder.Encode("AceJob"),
        _urlEncoder.Encode(email),
        unformattedKey);
    
    // Use external QR code service
    return $"https://api.qrserver.com/v1/create-qr-code/?size=200x200&data={Uri.EscapeDataString(uri)}";
}
```

#### Step 4: Verification & Activation
```csharp
public async Task<IActionResult> OnPostEnableAsync()
{
    var user = await _userManager.GetUserAsync(User);
 var verificationCode = VerificationCode.Replace(" ", "").Replace("-", "");
    
    // Verify the TOTP code
    var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
  user,
    _userManager.Options.Tokens.AuthenticatorTokenProvider,
        verificationCode);
    
if (is2faTokenValid)
    {
        // Enable 2FA for the user
        await _userManager.SetTwoFactorEnabledAsync(user, true);
     
        // Generate 10 recovery codes
        RecoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
        
// Audit log the event
  await _auditService.LogAsync("2FA_Enabled", user.Id, user.Email, "Two-Factor Authentication enabled", true);
 
      return Page();
    }
}
```

### 2FA Login Flow

#### Login Process Diagram
```
User enters email/password
         ¡ý
Normal authentication succeeds
         ¡ý
Check if 2FA is enabled
         ¡ý
    [If 2FA enabled]
         ¡ý
Redirect to /LoginWith2fa
         ¡ý
User enters 6-digit TOTP code
   ¡ý
Validate code with Identity
  ¡ý
    [If valid]
         ¡ý
Complete sign-in process
         ¡ý
Create session & audit log
  ¡ý
Redirect to intended page
```

#### Implementation Details
```csharp
// LoginWith2faModel.OnPostAsync()
public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
{
    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
  var authenticatorCode = TwoFactorCode.Replace(" ", "").Replace("-", "");
    
    // Perform 2FA sign-in
    var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
authenticatorCode,
        RememberMe,        // Remember login
        RememberMachine);  // Remember this device for 30 days
    
    if (result.Succeeded)
    {
        // Store session data
 HttpContext.Session.SetString("UserId", user.Id);
        HttpContext.Session.SetString("UserEmail", user.Email ?? "");
        HttpContext.Session.SetString("LoginTime", DateTime.UtcNow.ToString("O"));
     
        // Audit the successful login
        await _auditService.LogAsync(AuditActions.Login, user.Id, user.Email, 
            $"Login successful with 2FA (RememberDevice: {RememberMachine})", true);
        
        return LocalRedirect(returnUrl);
    }
}
```

### Recovery Code System

#### Generation
```csharp
// Generate 10 unique recovery codes
RecoveryCodes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
```

#### Usage
```csharp
// LoginWithRecoveryCodeModel.OnPostAsync()
public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
{
    var recoveryCode = RecoveryCode.Replace(" ", "").Replace("-", "");
    var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode);
    
  if (result.Succeeded)
{
 await _auditService.LogAsync("2FA_RecoveryLogin", user.Id, user.Email, 
  "Login successful using recovery code", true);
        return LocalRedirect(returnUrl);
    }
}
```

**Important:** Recovery codes are single-use and automatically consumed when used successfully.

### 2FA Security Features

| Feature | Implementation | Security Benefit |
|---------|---------------|------------------|
| **Time-based codes** | 30-second windows | Prevents replay attacks |
| **Remember device** | 30-day cookie | User convenience without compromising security |
| **Recovery codes** | 10 single-use codes | Account recovery when device unavailable |
| **Audit logging** | All 2FA events logged | Security monitoring and compliance |
| **Rate limiting** | Built into ASP.NET Identity | Prevents brute force attacks |
| **Secure key storage** | Database encryption | Protects authenticator keys |

---

## Change Password Feature Implementation

### Overview

The Change Password feature implements enterprise-grade password management with history tracking, age policies, and comprehensive security validations.

### Architecture Components

```
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦  Password Management System     ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Frontend          ©¦
©¦  ©¸©¤©¤ ChangePassword.cshtml     - Password change UI  ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Backend Services        ©¦
©¦  ©À©¤©¤ ChangePasswordModel       - Page logic           ©¦
©¦  ©À©¤©¤ IPasswordService          - Password policy enforcement    ©¦
©¦  ©À©¤©¤ PasswordService     - Implementation      ©¦
©¦  ©¸©¤©¤ AuditService    - Security logging    ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Database       ©¦
©¦  ©À©¤©¤ AspNetUsers           - Password metadata              ©¦
©¦  ©À©¤©¤ PasswordHistories   - Historical passwords           ©¦
©¦  ©¸©¤©¤ AuditLogs    - Password change events         ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
```

### Password Policy Configuration

```json
// appsettings.json
{
  "PasswordPolicy": {
    "MaxAgeDays": 90,      // Password expires after 90 days
    "MinAgeDays": 1,   // Cannot change for 1 day after last change
    "HistoryCount": 2      // Cannot reuse last 2 passwords
  }
}
```

### Change Password Process Flow

```
User accesses /ChangePassword
         ¡ý
Check password expiry status
         ¡ý
Display form with current policy info
         ¡ý
User submits form
    ¡ý
    [Validation Chain]
         ¡ý
Verify current password
         ¡ý
Check minimum age policy (1 day)
      ¡ý
Check password history (last 2)
         ¡ý
Validate new password complexity
       ¡ý
    [If all pass]
         ¡ý
Hash new password
       ¡ý
Add old password to history
         ¡ý
Update user record
         ¡ý
Refresh authentication cookie
         ¡ý
Log success to audit trail
```

### Implementation Details

#### Password Service Interface
```csharp
public interface IPasswordService
{
    Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword, int historyCount = 2);
    Task AddToHistoryAsync(string userId, string passwordHash);
    Task<bool> IsPasswordExpiredAsync(ApplicationUser user);
    Task<bool> CanChangePasswordAsync(ApplicationUser user);
    int GetDaysUntilExpiry(ApplicationUser user);
    Task CleanupOldHistoryAsync(string userId, int keepCount = 2);
}
```

#### Password History Check
```csharp
public async Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword, int historyCount = 2)
{
    var recentPasswords = await _context.PasswordHistories
    .Where(ph => ph.UserId == userId)
     .OrderByDescending(ph => ph.CreatedAt)
        .Take(historyCount)
        .ToListAsync();

    var tempUser = new ApplicationUser { Id = userId };

    foreach (var history in recentPasswords)
    {
   var result = _passwordHasher.VerifyHashedPassword(tempUser, history.PasswordHash, newPassword);
        if (result == PasswordVerificationResult.Success || 
        result == PasswordVerificationResult.SuccessRehashNeeded)
        {
    return true; // Password found in history
        }
    }

    return false;
}
```

#### Password Age Validation
```csharp
public async Task<bool> CanChangePasswordAsync(ApplicationUser user)
{
    if (user.PasswordChangedDate == null)
    {
    return await Task.FromResult(true); // No previous change, can change anytime
    }

    var daysSinceChange = (DateTime.UtcNow - user.PasswordChangedDate.Value).TotalDays;
    var canChange = daysSinceChange >= _minPasswordAgeDays;

    return canChange;
}
```

#### Password Expiry Check
```csharp
public async Task<bool> IsPasswordExpiredAsync(ApplicationUser user)
{
    if (user.PasswordChangedDate == null)
    {
        return true; // No change date = expired (force change)
    }

    var daysSinceChange = (DateTime.UtcNow - user.PasswordChangedDate.Value).TotalDays;
    return daysSinceChange > _maxPasswordAgeDays;
}
```

#### Main Password Change Logic
```csharp
public async Task<IActionResult> OnPostAsync()
{
    var user = await _userManager.GetUserAsync(User);
    
    // Check minimum age policy
    if (!await _passwordService.CanChangePasswordAsync(user))
    {
  ErrorMessage = "You cannot change your password yet. Please wait at least 1 day.";
        await _auditService.LogAsync(AuditActions.PasswordChange, user.Id, user.Email, 
      "Password change rejected - minimum age not reached", false);
        return Page();
    }

    // Verify current password
var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, ChangeModel.CurrentPassword);
    if (!isCurrentPasswordValid)
    {
        ModelState.AddModelError("ChangeModel.CurrentPassword", "Current password is incorrect.");
  return Page();
    }

    // Check password history
    if (await _passwordService.IsPasswordInHistoryAsync(user.Id, ChangeModel.NewPassword))
    {
        ModelState.AddModelError("ChangeModel.NewPassword", 
            "This password has been used recently. Please choose a different password.");
      return Page();
    }

    // Change the password
    var result = await _userManager.ChangePasswordAsync(user, ChangeModel.CurrentPassword, ChangeModel.NewPassword);

    if (result.Succeeded)
    {
    // Add old password to history
 await _passwordService.AddToHistoryAsync(user.Id, user.PasswordHash!);

        // Update metadata
        user.PasswordChangedDate = DateTime.UtcNow;
        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);

   // Refresh authentication
     await _signInManager.RefreshSignInAsync(user);

        // Audit log
        await _auditService.LogAsync(AuditActions.PasswordChange, user.Id, user.Email, 
"Password changed successfully", true);

        SuccessMessage = "Your password has been changed successfully!";
    }

    return Page();
}
```

---

## Password History Management

### Database Schema

#### PasswordHistory Entity
```csharp
public class PasswordHistory
{
    [Key]
    public int Id { get; set; }

  [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual ApplicationUser? User { get; set; }
}
```

#### ApplicationUser Extensions
```csharp
public class ApplicationUser : IdentityUser
{
    // ... existing properties ...
    
/// <summary>
    /// When the password was last changed
    /// </summary>
public DateTime? PasswordChangedDate { get; set; }
    
    /// <summary>
    /// Force password change on next login
    /// </summary>
    public bool MustChangePassword { get; set; }
    
    /// <summary>
    /// Navigation property to password history
    /// </summary>
  public virtual ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();
}
```

### History Management

#### Adding to History
```csharp
public async Task AddToHistoryAsync(string userId, string passwordHash)
{
    var history = new PasswordHistory
 {
        UserId = userId,
      PasswordHash = passwordHash,
        CreatedAt = DateTime.UtcNow
  };

    _context.PasswordHistories.Add(history);
    await _context.SaveChangesAsync();

    // Clean up old entries automatically
    await CleanupOldHistoryAsync(userId, _passwordHistoryCount);
}
```

#### Cleanup Process
```csharp
public async Task CleanupOldHistoryAsync(string userId, int keepCount = 2)
{
    var allHistory = await _context.PasswordHistories
        .Where(ph => ph.UserId == userId)
        .OrderByDescending(ph => ph.CreatedAt)
        .ToListAsync();

    if (allHistory.Count > keepCount)
    {
        var toRemove = allHistory.Skip(keepCount).ToList();
        _context.PasswordHistories.RemoveRange(toRemove);
        await _context.SaveChangesAsync();
    }
}
```

---

## Security Architecture

### Security Layers

```
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦     Security Layers      ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Layer 1: Authentication & Authorization    ©¦
©¦  ©À©¤©¤ [Authorize] attributes on pages          ©¦
©¦  ©À©¤©¤ User session validation       ©¦
©¦  ©¸©¤©¤ Identity framework integration              ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Layer 2: Input Validation           ©¦
©¦  ©À©¤©¤ Model validation attributes             ©¦
©¦  ©À©¤©¤ Password complexity rules              ©¦
©¦  ©¸©¤©¤ Data sanitization       ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Layer 3: Business Logic Security   ©¦
©¦  ©À©¤©¤ Password age policies       ©¦
©¦  ©À©¤©¤ Password history checking      ©¦
©¦  ©À©¤©¤ Rate limiting (via Identity)         ©¦
©¦  ©¸©¤©¤ 2FA enforcement            ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Layer 4: Data Protection             ©¦
©¦  ©À©¤©¤ Password hashing (PBKDF2)  ©¦
©¦  ©À©¤©¤ 2FA key encryption      ©¦
©¦  ©À©¤©¤ Database connection security     ©¦
©¦  ©¸©¤©¤ Audit trail logging      ©¦
©À©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©È
©¦ Layer 5: Infrastructure Security    ©¦
©¦  ©À©¤©¤ HTTPS enforcement         ©¦
©¦  ©À©¤©¤ Secure cookies         ©¦
©¦  ©À©¤©¤ Session security            ©¦
©¦  ©¸©¤©¤ Error handling        ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
```

### Audit Logging

#### 2FA Audit Events
| Event | Trigger | Data Logged |
|-------|---------|-------------|
| `2FA_Setup` | User initiates 2FA setup | User ID, Email, Timestamp |
| `2FA_Enabled` | 2FA successfully activated | User ID, Email, Success timestamp |
| `2FA_Disabled` | User disables 2FA | User ID, Email, Disable timestamp |
| `2FA_Failed` | Invalid verification code | User ID, Email, Failed attempt |
| `2FA_RecoveryLogin` | Login with recovery code | User ID, Email, Recovery usage |
| `2FA_RecoveryCodes` | New recovery codes generated | User ID, Email, Generation time |

#### Password Change Audit Events
| Event | Trigger | Data Logged |
|-------|---------|-------------|
| `PasswordChange` | Successful password change | User ID, Email, Success timestamp |
| `PasswordChange` | Failed attempt (wrong current) | User ID, Email, Failure reason |
| `PasswordChange` | Blocked by minimum age | User ID, Email, Policy violation |
| `PasswordChange` | Blocked by history | User ID, Email, History violation |
| `PasswordExpiry` | Password expiration warning | User ID, Email, Days remaining |
| `PasswordForced` | Mandatory password change | User ID, Email, Force reason |

---

## Database Schema

### Core Tables

#### AspNetUsers (Extended)
```sql
CREATE TABLE [AspNetUsers] (
  [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
    [UserName] NVARCHAR(256) NULL,
    [NormalizedUserName] NVARCHAR(256) NULL,
    [Email] NVARCHAR(256) NULL,
    [NormalizedEmail] NVARCHAR(256) NULL,
    [EmailConfirmed] BIT NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NULL,
    [SecurityStamp] NVARCHAR(MAX) NULL,
    [ConcurrencyStamp] NVARCHAR(MAX) NULL,
    [PhoneNumber] NVARCHAR(MAX) NULL,
    [PhoneNumberConfirmed] BIT NOT NULL,
    [TwoFactorEnabled] BIT NOT NULL,       -- 2FA status
    [LockoutEnd] DATETIMEOFFSET NULL,
    [LockoutEnabled] BIT NOT NULL,
    [AccessFailedCount] INT NOT NULL,
    
    -- Custom fields
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName] NVARCHAR(100) NOT NULL,
    [Gender] NVARCHAR(10) NOT NULL,
  [NRIC] NVARCHAR(MAX) NOT NULL,
    [DateOfBirth] DATETIME2 NOT NULL,
    [ResumeURL] NVARCHAR(500) NULL,
    [WhoAmI] NVARCHAR(500) NULL,
    
    -- Password policy fields
    [PasswordChangedDate] DATETIME2 NULL,     -- When password last changed
    [MustChangePassword] BIT NOT NULL         -- Force change flag
);
```

#### PasswordHistories
```sql
CREATE TABLE [PasswordHistories] (
    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId] NVARCHAR(450) NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    
    CONSTRAINT [FK_PasswordHistories_AspNetUsers_UserId] 
        FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_PasswordHistories_UserId] ON [PasswordHistories] ([UserId]);
CREATE INDEX [IX_PasswordHistories_CreatedAt] ON [PasswordHistories] ([CreatedAt]);
```

#### AuditLogs
```sql
CREATE TABLE [AuditLogs] (
    [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId] NVARCHAR(450) NULL,
    [UserEmail] NVARCHAR(256) NULL,
    [Action] NVARCHAR(50) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [IpAddress] NVARCHAR(45) NULL,
    [UserAgent] NVARCHAR(500) NULL,
    [Timestamp] DATETIME2 NOT NULL,
    [IsSuccess] BIT NOT NULL,
    [AdditionalData] NVARCHAR(MAX) NULL
);

CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
```

---

## Technical Implementation Details

### Dependency Injection Setup

```csharp
// Program.cs
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();

// Identity configuration
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // Password policy
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredUniqueChars = 3;

    // Lockout policy
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.AllowedForNewUsers = true;

    // 2FA configuration
    options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
})
.AddEntityFrameworkStores<AuthDbContext>();
```

### Session Management

```csharp
// Session configuration
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
```

### Error Handling

```csharp
// Global error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
    app.UseHsts();
}
```

---

## Testing Scenarios

### 2FA Testing

#### Setup Testing
```
1. Login as test user
2. Navigate to /TwoFactorAuth
3. Click "Set Up Two-Factor Authentication"
4. Scan QR code with Google Authenticator
5. Enter 6-digit code
6. Verify success message and recovery codes display
7. Logout and test 2FA login flow
```

#### Login Testing
```
1. Enter email/password on /Login
2. Verify redirect to /LoginWith2fa
3. Enter code from authenticator app
4. Test "Remember this device" functionality
5. Test invalid code handling
6. Test account lockout after multiple failures
```

#### Recovery Code Testing
```
1. Use /LoginWithRecoveryCode instead of /LoginWith2fa
2. Enter one of the saved recovery codes
3. Verify successful login
4. Confirm recovery code is consumed (can't reuse)
5. Test all 10 recovery codes work
6. Generate new recovery codes
```

### Change Password Testing

#### Policy Testing
```
1. Try changing password immediately after last change (should fail - 1 day minimum)
2. Try reusing current password (should fail)
3. Try reusing previous password (should fail)
4. Use weak password (should fail complexity rules)
5. Use valid new password (should succeed)
```

#### Expiry Testing
```
1. Set PasswordChangedDate to 91 days ago in database
2. Login - should redirect to change password
3. Change password successfully
4. Verify PasswordChangedDate updated
5. Verify MustChangePassword flag cleared
```

#### History Testing
```
1. Change password to "NewPassword123!"
2. Try changing again to same password (should fail)
3. Change to "AnotherPassword456@"
4. Try changing back to "NewPassword123!" (should fail)
5. Change to third unique password (should succeed)
6. Verify only last 2 passwords in history table
```

### Security Testing

#### Audit Log Verification
```sql
-- Check 2FA events
SELECT * FROM AuditLogs 
WHERE Action LIKE '2FA_%' 
ORDER BY Timestamp DESC;

-- Check password change events
SELECT * FROM AuditLogs 
WHERE Action = 'PasswordChange' 
ORDER BY Timestamp DESC;

-- Check failed login attempts
SELECT * FROM AuditLogs 
WHERE Action = 'FailedLogin' 
ORDER BY Timestamp DESC;
```

#### Password History Verification
```sql
-- Check password history for user
SELECT ph.*, u.Email 
FROM PasswordHistories ph
JOIN AspNetUsers u ON ph.UserId = u.Id
WHERE u.Email = 'test@example.com'
ORDER BY ph.CreatedAt DESC;
```

---

## Production Considerations

### Performance Optimizations

1. **Database Indexes**
   - PasswordHistories: UserId, CreatedAt
   - AuditLogs: UserId, Action, Timestamp
   - Regular cleanup of old audit logs

2. **Caching Strategies**
   - Cache password policy configuration
   - Cache user 2FA status
   - Implement distributed cache for high availability

3. **Background Services**
   - Automated password expiry notifications
   - Cleanup of old password histories
   - Audit log archival

### Security Hardening

1. **Environment-specific Settings**
   ```json
   // Production appsettings.json
   {
     "PasswordPolicy": {
       "MaxAgeDays": 60,        // Stricter in production
       "MinAgeDays": 1,
       "HistoryCount": 5        // More history in production
     }
   }
   ```

2. **Monitoring & Alerting**
   - Failed 2FA attempts
   - Password policy violations
   - Account lockouts
   - Recovery code usage

3. **Compliance Features**
   - Audit log retention policies
   - Data encryption at rest
   - Secure backup procedures
   - Regular security assessments

---

**Documentation Complete** ?

This implementation provides enterprise-grade security with comprehensive audit trails, policy enforcement, and user-friendly interfaces while maintaining high security standards.