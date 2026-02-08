using AceJob.Model;
using AceJob.Services;
using AceJob.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // Add API Controllers support

builder.Services.AddDbContext<AuthDbContext>(options =>
 options.UseSqlServer(
 builder.Configuration.GetConnectionString("AuthConnectionString"),
 sqlOptions => sqlOptions.EnableRetryOnFailure()
 )
);

// Configure Data Protection for enhanced security
builder.Services.AddDataProtection()
 .PersistKeysToFileSystem(new DirectoryInfo(@"./keys"))
 .SetApplicationName("AceJob")
 .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

// Configure Google reCAPTCHA v3
builder.Services.AddHttpClient();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();

// Register Email Service
builder.Services.AddScoped<GmailEmailService>();

// Register Audit Service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

// Register Password Service (for password history and age policies)
builder.Services.AddScoped<IPasswordService, PasswordService>();

// Register Lockout Recovery Service
builder.Services.AddScoped<ILockoutRecoveryService, LockoutRecoveryService>();

// Add background service for automatic lockout recovery
builder.Services.AddHostedService<LockoutRecoveryBackgroundService>();

// Configure Identity with enhanced security
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
 // Password settings - Enhanced security (Min12 chars)
 options.Password.RequireDigit = true;
 options.Password.RequireLowercase = true;
 options.Password.RequireUppercase = true;
 options.Password.RequireNonAlphanumeric = true;
 options.Password.RequiredLength = 12;
 options.Password.RequiredUniqueChars = 3;

 // Lockout settings - Protection against brute force (3 attempts)
 options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
 options.Lockout.MaxFailedAccessAttempts = 3;
 options.Lockout.AllowedForNewUsers = true;

 // User settings - Email must be unique
 options.User.RequireUniqueEmail = true;

 // Sign-in settings
 options.SignIn.RequireConfirmedEmail = false;
 options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<AuthDbContext>()
.AddDefaultTokenProviders();

// Configure Identity's main authentication cookie with enhanced security
builder.Services.ConfigureApplicationCookie(options =>
{
 options.Cookie.Name = "AceJobAuth";
 options.Cookie.HttpOnly = true; // Prevent JavaScript access (XSS protection)
 options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
 options.Cookie.SameSite = SameSiteMode.Strict; // CSRF protection
 options.ExpireTimeSpan = TimeSpan.FromHours(24);
 options.SlidingExpiration = true;
 options.LoginPath = "/Login";
 options.LogoutPath = "/Logout";
 options.AccessDeniedPath = "/AccessDenied";
});

// Configure Two-Factor Authentication "Remember Device" cookie (30 days)
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, options =>
{
 options.Cookie.Name = "AceJob2FARemember";
 options.Cookie.HttpOnly = true;
 options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Always require HTTPS
 options.Cookie.SameSite = SameSiteMode.Strict;
 options.ExpireTimeSpan = TimeSpan.FromDays(30); // Remember device for 30 days
});

// Configure session with enhanced security
builder.Services.AddSession(options =>
{
 options.IdleTimeout = TimeSpan.FromMinutes(30);
 options.Cookie.HttpOnly = true;
 options.Cookie.IsEssential = true;
 options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Always require HTTPS
 options.Cookie.SameSite = SameSiteMode.Strict;
});

// Add security headers
builder.Services.AddHsts(options =>
{
 options.Preload = true;
 options.IncludeSubDomains = true;
 options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddAuthorization(options =>
{
 options.AddPolicy("MustBelongToHRDepartment",
 Policy => Policy.RequireClaim("Department", "HR"));
});

var app = builder.Build();

// Configure the HTTP request pipeline with enhanced security
if (app.Environment.IsDevelopment())
{
 // In development, show detailed error page
 app.UseDeveloperExceptionPage();
}
else
{
 // In production, use custom error handler and security headers
 app.UseExceptionHandler("/Error");
 app.UseHsts(); // Enforce HTTPS with security headers
}

// Handle status code errors (404,403,401,500, etc.)
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

// Enforce HTTPS redirection
app.UseHttpsRedirection();

// Add security headers middleware
app.Use(async (context, next) =>
{
 // Prevent clickjacking
 context.Response.Headers["X-Frame-Options"] = "DENY";
 
 // Prevent MIME type sniffing
 context.Response.Headers["X-Content-Type-Options"] = "nosniff";
 
 // Enable XSS filtering (legacy)
 context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
 
 // Referrer Policy
 context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
 
 string csp;
 if (app.Environment.IsDevelopment())
 {
 // Relax CSP for development to allow BrowserLink, local websocket endpoints and CDNs used during dev
 csp =
 "default-src 'self' 'unsafe-inline' data: https:; " +
 "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://www.google.com https://www.gstatic.com https://cdn.jsdelivr.net; " +
 "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
 "img-src 'self' data: https:; " +
 "connect-src 'self' http://localhost:43763 ws://localhost:43763 wss://localhost:44324 https://cdn.jsdelivr.net https://www.google.com https://www.gstatic.com; " +
 "font-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
 "frame-src https://www.google.com https://www.gstatic.com;";
 }
 else
 {
 // Stricter CSP for production
 csp =
 "default-src 'self'; " +
 "script-src 'self' https://www.google.com https://www.gstatic.com https://cdn.jsdelivr.net; " +
 "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
 "img-src 'self' data: https:; " +
 "connect-src 'self' https://cdn.jsdelivr.net https://www.google.com https://www.gstatic.com; " +
 "font-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
 "frame-src https://www.google.com https://www.gstatic.com;";
 }

 context.Response.Headers["Content-Security-Policy"] = csp;
 
 await next();
});

app.UseStaticFiles();
app.UseRouting();

app.UseSession();

// Detect session timeout for authenticated users and redirect to /Login
app.UseMiddleware<SessionTimeoutMiddleware>();

app.UseAuthentication();

// Detect concurrent logins (invalidate older sessions)
app.UseMiddleware<ConcurrentLoginMiddleware>();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // Map API Controllers

app.Run();

























