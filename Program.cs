using AceJob.Model;
using AceJob.Services;
using AceJob.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AuthConnectionString"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);

// Configure Google reCAPTCHA v3
builder.Services.AddHttpClient();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();

// Register Audit Service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

// Register Password Service (for password history and age policies)
builder.Services.AddScoped<IPasswordService, PasswordService>();

// Configure Identity with password requirements
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings - Enhanced security (Min 12 chars)
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

// Configure Identity's main authentication cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "AceJobAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
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
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);  // Remember device for 30 days
});

// Configure session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MustBelongToHRDepartment",
        Policy => Policy.RequireClaim("Department", "HR"));
});

var app = builder.Build();

// Configure the HTTP request pipeline with custom error handling
if (app.Environment.IsDevelopment())
{
    // In development, show detailed error page
    app.UseDeveloperExceptionPage();
}
else
{
    // In production, use custom error handler
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Handle status code errors (404, 403, 401, 500, etc.)
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

app.UseHttpsRedirection();
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

app.Run();







