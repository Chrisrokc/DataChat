using DataChat.Application;
using DataChat.Infrastructure;
using DataChat.Web.Authentication;
using DataChat.Web.Components;
using DataChat.Web.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.FluentUI.AspNetCore.Components;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Add Application and Infrastructure layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Add FluentUI
    builder.Services.AddFluentUIComponents();

    // Add SignalR
    builder.Services.AddSignalR();

    // Add HttpContextAccessor for Blazor
    builder.Services.AddHttpContextAccessor();

    // Data Protection - configure with stable key storage
    var keysDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data-protection-keys");
    Directory.CreateDirectory(keysDirectory);

    builder.Services.AddDataProtection()
        .SetApplicationName("DataChat")
        .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

    // Configure Authentication based on settings
    var authMode = builder.Configuration.GetValue<string>("Authentication:Mode") ?? "Local";
    var windowsAuthEnabled = builder.Configuration.GetValue<bool>("Authentication:WindowsAuth:Enabled");

    if (authMode == "Windows" || windowsAuthEnabled)
    {
        // Windows Authentication
        builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate();
    }
    else
    {
        // Cookie-based Authentication (default)
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/access-denied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                // Add event handlers for debugging
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = context =>
                    {
                        Log.Information("Cookie OnValidatePrincipal - User: {User}, IsAuthenticated: {IsAuth}",
                            context.Principal?.Identity?.Name ?? "null",
                            context.Principal?.Identity?.IsAuthenticated ?? false);
                        return Task.CompletedTask;
                    },
                    OnSigningIn = context =>
                    {
                        Log.Information("Cookie OnSigningIn - User: {User}",
                            context.Principal?.Identity?.Name ?? "null");
                        return Task.CompletedTask;
                    },
                    OnSignedIn = context =>
                    {
                        Log.Information("Cookie OnSignedIn - User: {User}",
                            context.Principal?.Identity?.Name ?? "null");
                        return Task.CompletedTask;
                    }
                };
            });
    }

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Admin"));

        // Only require authentication if not on login page
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // Add Cascading Authentication State for Blazor
    builder.Services.AddCascadingAuthenticationState();

    // Add custom authentication state provider for Blazor Server
    builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

    // Add Razor Components
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseAuthentication();

    // Debug middleware to check auth state after authentication middleware
    app.Use(async (context, next) =>
    {
        Log.Information("After UseAuthentication - Path: {Path}, IsAuthenticated: {IsAuth}, User: {User}",
            context.Request.Path,
            context.User?.Identity?.IsAuthenticated ?? false,
            context.User?.Identity?.Name ?? "null");
        await next();
    });

    app.UseAuthorization();

    app.UseAntiforgery();

    // Map login endpoint (POST)
    app.MapPost("/api/login", async (HttpContext context, DataChat.Application.Common.Interfaces.IAuthenticationService authService) =>
    {
        var form = await context.Request.ReadFormAsync();
        var username = form["username"].ToString();
        var password = form["password"].ToString();

        Log.Information("Login attempt for user: {Username}", username);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Log.Warning("Login failed: empty username or password");
            return Results.Redirect("/login?error=Please+enter+username+and+password");
        }

        var result = await authService.AuthenticateAsync(username, password);

        Log.Information("Authentication result for {Username}: Success={Success}, Error={Error}",
            username, result.Success, result.ErrorMessage);

        if (result.Success)
        {
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, result.UserId.ToString()!),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, result.DisplayName!),
                new System.Security.Claims.Claim("Username", username)
            };

            foreach (var role in result.Roles!)
            {
                claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
            }

            var identity = new System.Security.Claims.ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            Log.Information("User {Username} logged in successfully, redirecting to /", username);
            return Results.Redirect("/");
        }
        else
        {
            Log.Warning("Login failed for {Username}: {Error}", username, result.ErrorMessage);
            return Results.Redirect($"/login?error={Uri.EscapeDataString(result.ErrorMessage ?? "Invalid credentials")}");
        }
    }).AllowAnonymous().DisableAntiforgery();

    // Map logout endpoint
    app.MapGet("/logout", async (HttpContext context) =>
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }).AllowAnonymous();

    // Debug endpoint to test if authentication cookie is being read
    app.MapGet("/api/auth-test", async (HttpContext context) =>
    {
        var isAuth = context.User?.Identity?.IsAuthenticated ?? false;
        var name = context.User?.Identity?.Name ?? "anonymous";
        var claims = context.User?.Claims?.Select(c => $"{c.Type}: {c.Value}").ToList() ?? new List<string>();
        var allCookies = context.Request.Cookies.Keys.ToList();

        // Try to manually authenticate to see if it works
        var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Log.Information("Manual AuthenticateAsync - Succeeded: {Succeeded}, Failure: {Failure}",
            authResult.Succeeded,
            authResult.Failure?.Message ?? "none");

        Log.Information("Auth test - IsAuthenticated: {IsAuth}, Name: {Name}, Claims: {ClaimCount}, Cookies: {Cookies}",
            isAuth, name, claims.Count, string.Join(", ", allCookies));

        return Results.Ok(new
        {
            IsAuthenticated = isAuth,
            Name = name,
            Claims = claims,
            AllCookieNames = allCookies,
            ManualAuthSucceeded = authResult.Succeeded,
            ManualAuthFailure = authResult.Failure?.Message
        });
    }).AllowAnonymous();

    // Protected endpoint - requires authentication
    app.MapGet("/api/protected", (HttpContext context) =>
    {
        var isAuth = context.User?.Identity?.IsAuthenticated ?? false;
        var name = context.User?.Identity?.Name ?? "anonymous";
        Log.Information("Protected endpoint - IsAuth: {IsAuth}, User: {User}", isAuth, name);
        return Results.Ok(new { IsAuthenticated = isAuth, Name = name, Message = "You are authenticated!" });
    }).RequireAuthorization();

    // Data Protection diagnostic endpoint
    app.MapGet("/api/dp-test", (IDataProtectionProvider dpProvider) =>
    {
        try
        {
            var protector = dpProvider.CreateProtector("test-purpose");
            var testData = "hello-world-" + DateTime.UtcNow.Ticks;

            Log.Information("DP Test - About to protect: {Data}", testData);
            var encryptedBytes = protector.Protect(System.Text.Encoding.UTF8.GetBytes(testData));
            Log.Information("DP Test - Protected bytes length: {Length}", encryptedBytes.Length);

            var decryptedBytes = protector.Unprotect(encryptedBytes);
            var decrypted = System.Text.Encoding.UTF8.GetString(decryptedBytes);
            Log.Information("DP Test - Decrypted: {Decrypted}", decrypted);

            var success = testData == decrypted;

            return Results.Ok(new
            {
                Success = success,
                Original = testData,
                EncryptedLength = encryptedBytes.Length,
                Decrypted = decrypted,
                KeysDirectory = Directory.GetCurrentDirectory() + "/data-protection-keys"
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Data Protection test failed");
            return Results.Ok(new { Success = false, Error = ex.Message, StackTrace = ex.StackTrace });
        }
    }).AllowAnonymous();

    // Clear ALL cookies endpoint (including antiforgery)
    app.MapGet("/api/clear-auth", async (HttpContext context) =>
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Delete all cookies
        foreach (var cookie in context.Request.Cookies.Keys)
        {
            context.Response.Cookies.Delete(cookie);
            Log.Information("Deleted cookie: {CookieName}", cookie);
        }

        return Results.Ok("All cookies cleared. Please log in again at /login");
    }).AllowAnonymous();

    // One-time setup endpoint to reset admin password (remove after first use)
    app.MapGet("/api/setup-admin", async (DataChat.Application.Common.Interfaces.IApplicationDbContext dbContext, DataChat.Application.Common.Interfaces.IAuthenticationService authService) =>
    {
        Log.Information("Setup admin endpoint called");
        var admin = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(dbContext.Users, u => u.Username == "admin");
        if (admin == null)
        {
            Log.Warning("Admin user not found in database");
            return Results.NotFound("Admin user not found");
        }

        var newHash = authService.HashPassword("admin123");
        Log.Information("Generated new password hash for admin: {Hash}", newHash.Substring(0, 20) + "...");
        admin.PasswordHash = newHash;
        await dbContext.SaveChangesAsync(default);

        Log.Information("Admin password successfully reset");
        return Results.Ok("Admin password has been reset to 'admin123'. Please change it after logging in.");
    }).AllowAnonymous();

    // Document access endpoint for secure, time-limited file access
    app.MapGet("/api/documents/access/{token}", async (
        string token,
        HttpContext context,
        DataChat.Application.Common.Interfaces.IDocumentAccessTokenService tokenService,
        DataChat.Application.Common.Interfaces.IApplicationDbContext dbContext,
        DataChat.Application.Common.Interfaces.ICurrentUserService currentUser) =>
    {
        // 1. Validate token
        var tokenResult = tokenService.ValidateToken(token);
        if (tokenResult == null)
        {
            Log.Warning("Invalid or expired document access token");
            return Results.NotFound("Invalid or expired access link");
        }

        // 2. Verify current user matches token's user
        var currentUserId = currentUser.UserId;
        if (currentUserId == null || currentUserId != tokenResult.UserId)
        {
            Log.Warning("Document access denied: User {CurrentUser} tried to access token for user {TokenUser}",
                currentUserId, tokenResult.UserId);
            return Results.Forbid();
        }

        // 3. Load document with DataSource
        var document = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include(dbContext.Documents, d => d.DataSource),
            d => d.Id == tokenResult.DocumentId);

        if (document == null)
        {
            Log.Warning("Document {DocumentId} not found", tokenResult.DocumentId);
            return Results.NotFound("Document not found");
        }

        // 4. Verify DataSource.Type == FileSystem
        if (document.DataSource.Type != DataChat.Domain.Enums.DataSourceType.FileSystem)
        {
            Log.Warning("Attempt to access non-file document {DocumentId} of type {Type}",
                tokenResult.DocumentId, document.DataSource.Type);
            return Results.BadRequest("This document type does not support direct file access");
        }

        // 5. Verify file exists
        if (!System.IO.File.Exists(document.FilePath))
        {
            Log.Warning("Document file not found at path: {FilePath}", document.FilePath);
            return Results.NotFound("Document file not found on server");
        }

        // 6. Log the access for audit
        Log.Information("Document access: User {UserId} accessing document {DocumentId} ({FileName}) via message {MessageId}, IsDownload: {IsDownload}",
            currentUserId, document.Id, document.FileName, tokenResult.MessageId, tokenResult.IsDownload);

        // 7. Stream file with appropriate Content-Type and Content-Disposition
        var mimeType = document.MimeType ?? "application/octet-stream";
        var contentDisposition = tokenResult.IsDownload
            ? $"attachment; filename=\"{document.FileName}\""
            : $"inline; filename=\"{document.FileName}\"";

        context.Response.Headers.Append("Content-Disposition", contentDisposition);

        return Results.File(document.FilePath, mimeType);
    }).RequireAuthorization();

    // Map SignalR Hub
    app.MapHub<ChatHub>("/chathub");

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("Application starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
