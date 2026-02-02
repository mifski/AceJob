# AceJob Application Documentation

> **Last Updated:** January 2025  
> **Framework:** ASP.NET Core 8.0 (Razor Pages)  
> **Database:** SQL Server LocalDB

---

## Table of Contents

1. [Security Implementation](#security-implementation)
2. [User Registration](#user-registration)
3. [User Login & Authentication](#user-login--authentication)
4. [Account Lockout & Rate Limiting](#account-lockout--rate-limiting)
5. [Password Policy](#password-policy)
6. [Two-Factor Authentication (2FA)](#two-factor-authentication-2fa)
7. [Audit Logging](#audit-logging)
8. [Session Management & Logout](#session-management--logout)
9. [Google reCAPTCHA v3](#google-recaptcha-v3)
10. [File Upload (Resume)](#file-upload-resume)
11. [Home Page & User Profile Display](#home-page--user-profile-display)
12. [Custom Error Pages](#custom-error-pages)
13. [Database Guide](#database-guide)
14. [Troubleshooting](#troubleshooting)

---

## Security Implementation

### Overview

AceJob implements enterprise-grade security with multiple layers of protection:

| Feature | Status | Description |
|---------|--------|-------------|
| NRIC Encryption | ? | AES-256 encryption before database storage |
| Password Hashing | ? | PBKDF2 with HMAC-SHA256 (10,000 iterations) |
| Password Complexity | ? | 12+ chars, upper, lower, digit, special character |
| Brute Force Protection | ? | 3 failed attempts = 15 minute lockout |
| Password History | ? | Cannot reuse last 2 passwords |
| Password Age Policy | ? | Max 90 days, Min 1 day between changes |
| Change Password | ? | Users can change their password |
| Two-Factor Auth (2FA) | ? | TOTP-based authenticator app support |
| Email Uniqueness | ? | Prevents duplicate accounts |
| HTTPS Enforcement | ? | Secure cookie policy |
| XSS Protection | ? | HttpOnly cookies |
| Session Management | ? | 30-minute idle timeout, proper cleanup on logout |
| File Upload Validation | ? | Type, size, and path security checks |
| reCAPTCHA v3 | ? | Bot protection on login |
| Audit Logging | ? | All user activities logged to database |
| Custom Error Pages | ? | Graceful error handling (404, 403, 500, etc.) |

### NRIC Encryption

NRIC (Singapore National ID) is encrypted using AES-256 before storage:

```
Plain NRIC ¡ú AES-256 Encryption ¡ú Base64 Encoded String ¡ú Stored in Database
```

**Configuration** (`appsettings.json`):
```json
{
  "Encryption": {
    "Key": "YourSecureEncryptionKey123456789",
    "IV": "YourSecureIV1234"
  }
}
```

> ?? **Production:** Move encryption keys to Azure Key Vault or environment variables.

### Decrypting NRIC (When Needed)

Use the `EncryptionHelper` class:

```csharp
// Get encryption keys from configuration
var key = _configuration["Encryption:Key"];
var iv = _configuration["Encryption:IV"];

// Decrypt NRIC
var decryptedNRIC = EncryptionHelper.Decrypt(user.NRIC, key, iv);

// Show masked version for display (e.g., *****567A)
var maskedNRIC = EncryptionHelper.MaskNRIC(decryptedNRIC);
```

### Cookie Security

```csharp
// Configured in Program.cs
options.Cookie.HttpOnly = true;     // Prevents XSS attacks
options.Cookie.SecurePolicy = Always;  // HTTPS only in production
options.Cookie.SameSite = Strict;      // Prevents CSRF attacks
options.ExpireTimeSpan = 24 hours;     // Session timeout
options.SlidingExpiration = true;  // Auto-refresh on activity
```

---

## Password Policy

### Password Requirements

| Requirement | Value |
|-------------|-------|
| Minimum Length | 12 characters |
| Lowercase Letter | Required (a-z) |
| Uppercase Letter | Required (A-Z) |
| Digit | Required (0-9) |
| Special Character | Required |
| Unique Characters | At least 3 |

### Password Age Policy

| Setting | Value | Description |
|---------|-------|-------------|
| Maximum Age | 90 days | Password expires after 90 days |
| Minimum Age | 1 day | Cannot change password more than once per day |
| History Count | 2 | Cannot reuse last 2 passwords |

**Configuration** (`appsettings.json`):
```json
{
  "PasswordPolicy": {
    "MaxAgeDays": 90,
    "MinAgeDays": 1,
    "HistoryCount": 2
  }
}
```

### Password History

The system maintains a history of the last 2 password hashes for each user. When changing passwords:

1. New password is checked against history
2. If match found, change is rejected
3. Old password is added to history
4. Oldest entries are automatically cleaned up

**Database Table:** `PasswordHistories`

| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key |
| UserId | string | Foreign key to AspNetUsers |
| PasswordHash | string | Hashed password |
| CreatedAt | DateTime | When password was set |

### Change Password

**Page:** `/ChangePassword`

Features:
- Verify current password
- Check minimum password age (1 day)
- Check password history (last 2)
- Real-time password strength indicator
- Password match validation
- Audit logging of all attempts

**Access:** Authenticated users only (via user dropdown menu)

### Password Expiry

- Users are automatically redirected to Change Password page if password is expired
- Warning shown when password expires within 14 days
- `MustChangePassword` flag forces password change on next login

---

## Two-Factor Authentication (2FA)

### Overview

Two-Factor Authentication adds an extra layer of security using Time-based One-Time Passwords (TOTP) compatible with:
- Google Authenticator
- Microsoft Authenticator
- Authy
- Any TOTP-compatible authenticator app

### Setup Process

1. Navigate to `/TwoFactorAuth`
2. Click "Set Up Two-Factor Authentication"
3. Scan QR code with authenticator app (or enter manual key)
4. Enter 6-digit verification code
5. Save recovery codes in a secure location

### Login with 2FA

1. Enter email and password on `/Login`
2. If 2FA is enabled, redirected to `/LoginWith2fa`
3. Enter 6-digit code from authenticator app
4. Optionally check "Remember this device" (30 days)
5. Successfully logged in

### Recovery Codes

- 10 recovery codes generated on 2FA setup
- Each code can only be used once
- Use `/LoginWithRecoveryCode` if authenticator unavailable
- Generate new codes from 2FA settings page

### 2FA Pages

| Page | Purpose |
|------|---------|
| `/TwoFactorAuth` | Enable/disable 2FA, generate recovery codes |
| `/LoginWith2fa` | Enter 2FA code during login |
| `/LoginWithRecoveryCode` | Use recovery code if authenticator unavailable |

### 2FA Audit Events

| Event | Description |
|-------|-------------|
| `2FA_Setup` | User initiated 2FA setup |
| `2FA_Enabled` | 2FA successfully enabled |
| `2FA_Disabled` | 2FA disabled |
| `2FA_Failed` | Invalid verification code |
| `2FA_RecoveryLogin` | Logged in using recovery code |
| `2FA_RecoveryCodes` | Generated new recovery codes |

---

## Account Lockout & Rate Limiting

### Configuration

| Setting | Value |
|---------|-------|
| Max Failed Attempts | **3** |
| Lockout Duration | **15 minutes** |
| Applies to New Users | Yes |

### Lockout Behavior

1. **First failed attempt:** "2 attempt(s) remaining"
2. **Second failed attempt:** "1 attempt(s) remaining"
3. **Third failed attempt:** Account locked for 15 minutes

### Automatic Recovery

After lockout period expires:
- Account automatically unlocks
- Failed attempt counter resets
- User can try again

### Manual Reset (SQL)

```sql
UPDATE AspNetUsers
SET AccessFailedCount = 0, LockoutEnd = NULL
WHERE Email = 'user@example.com';
```

---

## User Registration

### Registration Form Fields

| # | Field | Type | Validation |
|---|-------|------|------------|
| 1 | First Name | Text | Required |
| 2 | Last Name | Text | Required |
| 3 | Gender | Dropdown | Male/Female/Other |
| 4 | NRIC | Text | Required, Encrypted |
| 5 | Email | Email | Required, Unique |
| 6 | Date of Birth | Date | Required |
| 7 | Password | Password | 12+ chars, complexity rules |
| 8 | Confirm Password | Password | Must match |
| 9 | Resume | File | .pdf/.docx, max 5MB |
| 10 | Who Am I | Textarea | Max 500 characters |

### Registration Flow

```
User Input (Plain Text + File)
    ¡ý
Register.cshtml (Form with enctype="multipart/form-data")
    ¡ý
Register.cshtml.cs (PageModel)
    ¡ý
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦ NRIC ¡ú EncryptNRIC() ¡ú Encrypted NRIC     ©¦
©¦ Password ¡ú Identity.CreateAsync() ¡ú Hashed Password©¦
©¦ Resume File ¡ú UploadResumeAsync() ¡ú File Path      ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
    ¡ý
ApplicationUser Object Created ¡ú Saved to Database
    ¡ý
©°©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©´
©¦ Audit Log: Registration event recorded ©¦
©¦ Audit Log: Auto-login event recorded             ©¦
©¦ Session: User ID, Email, Login Time stored         ©¦
©¸©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¼
    ¡ý
Redirect to Home Page (User automatically logged in)
```

### Testing Registration

**Valid Test Data:**
```
First Name: John
Last Name: Doe
Gender: Male
NRIC: S1234567A
Email: john.doe@example.com
Date of Birth: 01/01/1990
Password: MyP@ssw0rd123!
Confirm Password: MyP@ssw0rd123!
Resume: [Upload valid .pdf file < 5MB]
Who Am I: Experienced software developer with 5 years...
```

---

## User Login & Authentication

### Login Flow

```
User enters email + password
    ¡ý
Validate reCAPTCHA token (if enabled)
    ¡ý
Check if account is locked out
    ¡ý
Find user by email in database
    ¡ý
Verify password against hash (PBKDF2)
    ¡ý
If valid:
    ©À©¤©¤ Sign in user
    ©À©¤©¤ Create authentication cookie
    ©À©¤©¤ Store session data (UserId, Email, LoginTime)
    ©À©¤©¤ Log successful login to AuditLogs
    ©¸©¤©¤ Redirect to /Index (Home Page with User Info)
    
If invalid:
    ©À©¤©¤ Increment failed attempt counter
  ©À©¤©¤ Log failed login attempt to AuditLogs
    ©À©¤©¤ Show error with remaining attempts
    ©¸©¤©¤ Lock account if 3+ failed attempts (log lockout event)
```

### Login Features

| Feature | Description |
|---------|-------------|
| Email-based login | Uses email as username |
| Remember Me | Optional persistent login (24 hours) |
| Remaining attempts | Shows how many attempts left before lockout |
| Lockout notification | Clear message with remaining lockout time |
| Audit trail | All login attempts logged |

---

## Audit Logging

### Overview

All user activities are logged to the `AuditLogs` database table for security monitoring and compliance.

### Audit Log Entity

**File:** `Model/AuditLog.cs`

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| UserId | string? | User ID (null for anonymous actions) |
| UserEmail | string? | Email associated with action |
| Action | string | Action type (Login, Logout, etc.) |
| Description | string? | Detailed description |
| IpAddress | string? | Client IP address |
| UserAgent | string? | Browser/client info |
| Timestamp | DateTime | When action occurred (UTC) |
| IsSuccess | bool | Whether action succeeded |
| AdditionalData | string? | JSON data (optional) |

### Logged Actions

| Action | When Logged | Success |
|--------|-------------|---------|
| `Login` | Successful login | ? |
| `FailedLogin` | Invalid credentials | ? |
| `Lockout` | Account locked after 3 failures | ? |
| `Logout` | User logs out | ? |
| `Registration` | New user registers | ? |
| `ProfileView` | User views home page (profile) | ? |
| `PasswordChange` | Password changed | ?/? |
| `2FA_Enabled` | 2FA enabled | ? |
| `2FA_Disabled` | 2FA disabled | ? |
| `2FA_Failed` | Invalid 2FA code | ? |

### Audit Service

**File:** `Services/AuditService.cs`

```csharp
public interface IAuditService
{
    Task LogAsync(string action, string? userId, string? userEmail, 
 string? description, bool isSuccess, string? additionalData = null);
    Task LogLoginAsync(string? userId, string email, bool isSuccess, string? failureReason = null);
    Task LogLogoutAsync(string userId, string email);
    Task LogRegistrationAsync(string userId, string email);
    Task LogProfileViewAsync(string userId, string email);
    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(string userId, int count = 10);
}
```

### View Audit Logs (SQL)

```sql
-- View all audit logs
SELECT * FROM AuditLogs ORDER BY Timestamp DESC;

-- View logs for specific user
SELECT * FROM AuditLogs 
WHERE UserEmail = 'user@example.com' 
ORDER BY Timestamp DESC;

-- View failed login attempts
SELECT * FROM AuditLogs 
WHERE Action = 'FailedLogin' 
ORDER BY Timestamp DESC;

-- View lockout events
SELECT * FROM AuditLogs 
WHERE Action = 'Lockout' 
ORDER BY Timestamp DESC;

-- View recent activity (last 24 hours)
SELECT * FROM AuditLogs 
WHERE Timestamp > DATEADD(hour, -24, GETUTCDATE()) 
ORDER BY Timestamp DESC;
```

---

## Session Management & Logout

### Session Configuration

**Configured in `Program.cs`:**
```csharp
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
});
```

### Session Data Stored

| Key | Value | Purpose |
|-----|-------|---------|
| UserId | User's GUID | Identify current user |
| UserEmail | User's email | Quick access to email |
| LoginTime | ISO 8601 timestamp | Track session start |

### Proper Logout Process

The logout process performs these steps:

1. **Get current user info** (for audit log)
2. **Log logout event** to AuditLogs table
3. **Clear all session data** (`HttpContext.Session.Clear()`)
4. **Sign out from Identity** (`SignOutAsync()`)
5. **Delete all cookies** (authentication and others)
6. **Redirect to Login page**

---

## Google reCAPTCHA v3

### Overview

reCAPTCHA v3 provides invisible bot protection on the login page. It runs in the background and assigns a score (0.0 - 1.0) indicating how likely the user is human.

### Score Interpretation

| Score | Meaning | Action |
|-------|---------|--------|
| 0.9 - 1.0 | Definitely human | Allow |
| 0.7 - 0.9 | Likely human | Allow |
| 0.5 - 0.7 | Possibly human | Allow with caution |
| 0.3 - 0.5 | Possibly bot | Challenge or block |
| 0.0 - 0.3 | Likely bot | Block |

### Configuration

**`appsettings.json`** (Production):
```json
{
  "RecaptchaSettings": {
  "SiteKey": "your-site-key",
"SecretKey": "your-secret-key",
    "Enabled": true,
    "MinScore": 0.5
  }
}
```

**`appsettings.Development.json`** (Development):
```json
{
  "RecaptchaSettings": {
    "Enabled": false,
    "MinScore": 0.1
  }
}
```

### Getting reCAPTCHA Keys

1. Go to [Google reCAPTCHA Admin Console](https://www.google.com/recaptcha/admin/create)
2. Fill in:
   - **Label:** AceJob Login
   - **reCAPTCHA type:** reCAPTCHA v3
   - **Domains:** Add `localhost` for development
3. Click Submit
4. Copy the **Site Key** and **Secret Key**

### Troubleshooting reCAPTCHA

#### Browser Blocking reCAPTCHA

**For Microsoft Edge:**
1. Open Edge Settings: `edge://settings/privacy`
2. Find "Tracking prevention"
3. Change from "Strict" to **"Balanced"**
4. Restart Edge

**For Firefox:**
1. Click the shield icon in the address bar
2. Toggle off "Enhanced Tracking Protection"
3. Refresh the page

**For Brave:**
1. Click the Brave icon (right side of address bar)
2. Change "Shields" to **Down**
3. Refresh the page

---

## File Upload (Resume)

### Configuration

| Setting | Value |
|---------|-------|
| Allowed Types | `.pdf`, `.docx` |
| Maximum Size | 5MB |
| Storage Location | `/wwwroot/uploads/resumes/` |
| Filename Format | `{GUID}_{original-filename}` |

### Security Features

- ? File extension validation
- ? File size validation
- ? Unique filename generation (GUID)
- ? Path traversal prevention
- ? Secure storage directory

### Create Upload Directory

```powershell
New-Item -Path "wwwroot\uploads\resumes" -ItemType Directory -Force
```

---

## Home Page & User Profile Display

### Overview

After successful login, users are redirected to the home page which displays their complete profile information, including decrypted sensitive data.

### Information Displayed

| Section | Fields |
|---------|--------|
| **Personal Details** | Full Name, Email, Gender, Date of Birth |
| **Sensitive Information** | NRIC (Masked), NRIC (Decrypted with toggle), Resume download |
| **About Me** | Who Am I description |
| **Recent Activity** | Last 5 audit log entries |

### NRIC Display

- **Masked:** `*****567A` (always visible)
- **Decrypted:** `S1234567A` (hidden by default, click to reveal)

### Security Notice

The home page displays a security notice informing users:
> "Your NRIC is encrypted using AES-256 encryption and stored securely. Only you can view the decrypted value when logged in."

### Recent Activity Section

Shows the last 5 user activities from the audit log:
- Action type (Login, Logout, ProfileView, etc.)
- Description
- Timestamp
- Success/Failure indicator

### Last Login Time

Displayed from session data, showing when the current session started.

---

## Custom Error Pages

### Overview

AceJob implements graceful error handling with custom error pages for all HTTP status codes. Users never see raw error messages or stack traces.

### Supported Error Codes

| Code | Name | Icon | Description |
|------|------|------|-------------|
| 400 | Bad Request | ? | Invalid request format |
| 401 | Unauthorized | ?? | Login required |
| 403 | Access Denied | ??? | Permission denied |
| 404 | Not Found | ?? | Page doesn't exist |
| 405 | Method Not Allowed | ? | Wrong HTTP method |
| 408 | Request Timeout | ?? | Request took too long |
| 429 | Too Many Requests | ?? | Rate limited |
| 500 | Internal Server Error | ?? | Server error |
| 502 | Bad Gateway | ? | Invalid upstream response |
| 503 | Service Unavailable | ?? | Temporary maintenance |
| 504 | Gateway Timeout | ?? | Upstream timeout |

### Configuration

**Configured in `Program.cs`:**
```csharp
// Handle exceptions
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Detailed errors in dev
}
else
{
    app.UseExceptionHandler("/Error"); // Custom error page in production
    app.UseHsts();
}

// Handle status code errors (404, 403, 401, 500, etc.)
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
```

### Error Page Features

Each error page includes:

- ? **Appropriate icon** based on error type
- ? **Clear error message** (user-friendly, no technical details)
- ? **Helpful suggestions** based on error type
- ? **Navigation buttons:**
  - Go Home
  - Go Back
  - Login (for 401/403 errors)
- ? **Helpful links** to common pages
- ? **Request ID** for debugging (shown in development)

### Error Page Files

| File | Purpose |
|------|---------|
| `Pages/Error.cshtml` | Main error page view |
| `Pages/Error.cshtml.cs` | Error page logic |
| `Pages/AccessDenied.cshtml` | 403 Access Denied page |
| `Pages/NotFound.cshtml` | 404 Not Found page |

### Error Messages

Error messages are user-friendly and don't expose technical details:

| Code | User Message |
|------|--------------|
| 400 | "The request was invalid or cannot be processed." |
| 401 | "You need to be logged in to access this resource." |
| 403 | "You don't have the required permissions to access this resource." |
| 404 | "The page you requested could not be found." |
| 500 | "An internal server error occurred. Our team has been notified." |
| 503 | "The service is temporarily unavailable. Please try again later." |

### Testing Error Pages

**Test 404 (Not Found):**
```
Navigate to: /nonexistent-page
Expected: Custom 404 page with search icon
```

**Test 403 (Access Denied):**
```
Try accessing a page that requires admin role without permissions
Expected: Custom 403 page with shield icon
```

**Test 401 (Unauthorized):**
```
Access a protected page while logged out
Expected: Redirect to Login page (handled by Identity)
```

### Security Considerations

- ? **No stack traces** shown to users in production
- ? **No sensitive information** in error messages
- ? **Request ID** logged for debugging
- ? **Errors logged** to application logs
- ? **Consistent styling** with rest of application

---

## Database Guide

### Connection String

```json
{
  "ConnectionStrings": {
    "AuthConnectionString": "Data Source=(localdb)\\ProjectModels;Initial Catalog=AspNetAuth;..."
  }
}
```

### Database Tables

| Table | Purpose |
|-------|---------|
| `AspNetUsers` | User accounts with custom fields |
| `AspNetRoles` | Role definitions |
| `AspNetUserRoles` | User-role assignments |
| `AuditLogs` | User activity audit trail |
| `PasswordHistories` | Password reuse prevention |

### New User Fields

| Column | Type | Description |
|--------|------|-------------|
| PasswordChangedDate | DateTime? | When password was last changed |
| MustChangePassword | bool | Force password change on next login |

### Viewing Users in SQL Server

**Method 1: SQL Server Object Explorer (Visual Studio)**

1. View ¡ú SQL Server Object Explorer (`Ctrl + \, Ctrl + S`)
2. Expand: SQL Server ¡ú (localdb)\ProjectModels ¡ú Databases ¡ú AspNetAuth ¡ú Tables
3. Right-click on `dbo.AspNetUsers` ¡ú View Data

**Method 2: SQL Query**

```sql
-- View all registered users
SELECT 
    Id, UserName, Email, FirstName, LastName,
    Gender, NRIC, DateOfBirth, ResumeURL, WhoAmI
FROM AspNetUsers
ORDER BY Email;

-- Get total user count
SELECT COUNT(*) AS TotalUsers FROM AspNetUsers;

-- View users with roles
SELECT 
    u.Email, u.FirstName, u.LastName, r.Name AS RoleName
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AspNetRoles r ON ur.RoleId = r.Id
ORDER BY u.Email;
```

### Security Verification

After registration, verify in database:
- **NRIC should look like:** `kJ8vH3nF2mP9xQ1wZ...` (encrypted Base64)
- **NRIC should NOT look like:** `S1234567A` (plain text)
- **PasswordHash should be long:** `AQAAAAEAACcQAAAAE...` (hashed)

---

## Troubleshooting

### Login Issues

| Problem | Solution |
|---------|----------|
| Redirect loop | Clear browser cookies, restart app |
| Login button doesn't work | Check browser console for errors |
| Password doesn't work | Case-sensitive, check if locked out |
| Account locked | Wait 15 minutes or reset in database |
| "X attempts remaining" | Enter correct password before lockout |

### Registration Issues

| Problem | Solution |
|---------|----------|
| File upload not working | Create `wwwroot/uploads/resumes` folder |
| NRIC not encrypted | Check encryption keys in appsettings.json |
| Password validation not working | Check Program.cs password requirements |
| Duplicate email not caught | Verify `RequireUniqueEmail = true` |

### Error Page Issues

| Problem | Solution |
|---------|----------|
| Seeing raw errors | Check `UseExceptionHandler` is configured |
| 404 not showing custom page | Verify `UseStatusCodePagesWithReExecute` is configured |
| Error page missing styles | Check `_Layout.cshtml` is being used |
| Request ID not showing | Only shown in development environment |

### Session Issues

| Problem | Solution |
|---------|----------|
| Session lost | Check session timeout (30 min default) |
| Login time not showing | Verify session middleware is configured |
| Logout not working | Check form has `method="post"` |

---

## Project Structure

```
AceJob/
©À©¤©¤ wwwroot/
©¦   ©¸©¤©¤ uploads/
©¦  ©¸©¤©¤ resumes/         ¡û Resume files stored here
©À©¤©¤ Pages/
©¦   ©À©¤©¤ Login.cshtml      ¡û Login form with reCAPTCHA
©¦   ©À©¤©¤ Login.cshtml.cs    ¡û Login logic with audit logging
©¦   ©À©¤©¤ Logout.cshtml             ¡û Logout confirmation page
©¦   ©À©¤©¤ Logout.cshtml.cs      ¡û Logout with session clearing
©¦   ©À©¤©¤ Register.cshtml       ¡û Registration form
©¦   ©À©¤©¤ Register.cshtml.cs        ¡û Registration with audit logging
©¦   ©À©¤©¤ Index.cshtml            ¡û Home page with user profile
©¦   ©À©¤©¤ Index.cshtml.cs       ¡û Profile display with decryption
©¦   ©À©¤©¤ Error.cshtml     ¡û Custom error page (all codes)
©¦   ©À©¤©¤ Error.cshtml.cs ¡û Error handling logic
©¦   ©À©¤©¤ AccessDenied.cshtml       ¡û 403 Access Denied page
©¦   ©À©¤©¤ NotFound.cshtml           ¡û 404 Not Found page
©¦   ©¸©¤©¤ Shared/
©¦       ©¸©¤©¤ _Layout.cshtml        ¡û Main layout with navigation
©À©¤©¤ Model/
©¦   ©À©¤©¤ ApplicationUser.cs¡û User model with custom fields
©¦   ©À©¤©¤ AuthDbContext.cs          ¡û Database context with AuditLogs
©¦   ©¸©¤©¤ AuditLog.cs     ¡û Audit log entity
©À©¤©¤ ViewModels/
©¦   ©À©¤©¤ Login.cs       ¡û Login form model
©¦   ©¸©¤©¤ Register.cs               ¡û Registration form model
©À©¤©¤ Services/
©¦   ©À©¤©¤ RecaptchaService.cs       ¡û reCAPTCHA validation service
©¦   ©¸©¤©¤ AuditService.cs           ¡û Audit logging service
©À©¤©¤ Helpers/
©¦   ©¸©¤©¤ EncryptionHelper.cs       ¡û AES encryption utilities
©À©¤©¤ Program.cs       ¡û App configuration
©À©¤©¤ appsettings.json              ¡û Production settings
©¸©¤©¤ appsettings.Development.json  ¡û Development settings
```

---

## Production Deployment Checklist

- [ ] Move encryption keys to Azure Key Vault
- [ ] Configure reCAPTCHA for production domain
- [ ] Enable HTTPS enforcement
- [ ] Set password policy values appropriately
- [ ] Test 2FA setup and recovery
- [ ] Verify audit logging is working
- [ ] Test password expiry flow
- [ ] Test custom error pages
- [ ] Set up monitoring for failed logins
- [ ] Backup recovery code generation

---

## Quick Commands

```powershell
# Run the application
dotnet run

# Apply database migrations
dotnet ef database update

# Create new migration
dotnet ef migrations add MigrationName

# View password history for user
SELECT * FROM PasswordHistories WHERE UserId = 'user-id' ORDER BY CreatedAt DESC
```

---

**Documentation Complete** ?

**Advanced Security Features Implemented:**
- ? Automatic account recovery after lockout period (15 minutes)
- ? Password history (last 2 passwords)
- ? Change password functionality
- ? Minimum password age (1 day)
- ? Maximum password age (90 days)
- ? Two-Factor Authentication (2FA) with authenticator apps
- ? Recovery codes for 2FA
