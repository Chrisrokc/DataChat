using System.Security.Claims;
using DataChat.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApplicationDbContext _dbContext;
    private Guid? _cachedUserId;
    private bool? _cachedIsAdmin;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        IApplicationDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public string? WindowsIdentity
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            // For Windows Auth, get from Identity.Name
            // For cookie auth, try to get from claims
            var windowsIdentityClaim = user.FindFirst("WindowsIdentity")?.Value;
            if (!string.IsNullOrEmpty(windowsIdentityClaim))
                return windowsIdentityClaim;

            // Check if this is Windows Auth (DOMAIN\username format)
            var identityName = user.Identity?.Name;
            if (!string.IsNullOrEmpty(identityName) && identityName.Contains('\\'))
                return identityName;

            return null;
        }
    }

    public string? DisplayName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            // Try ClaimTypes.Name first (set during cookie auth)
            var nameClaim = user.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(nameClaim))
                return nameClaim;

            // Try GivenName claim
            var givenNameClaim = user.FindFirst(ClaimTypes.GivenName)?.Value;
            if (!string.IsNullOrEmpty(givenNameClaim))
                return givenNameClaim;

            // Fall back to username part of DOMAIN\username for Windows Auth
            var identity = user.Identity?.Name;
            if (!string.IsNullOrEmpty(identity))
            {
                return identity.Contains('\\')
                    ? identity.Split('\\').Last()
                    : identity;
            }

            return null;
        }
    }

    public Guid? UserId
    {
        get
        {
            if (_cachedUserId.HasValue)
                return _cachedUserId;

            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || user.Identity?.IsAuthenticated != true)
                return null;

            // Try to get from NameIdentifier claim first (set during cookie auth)
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedId))
            {
                _cachedUserId = parsedId;
                return _cachedUserId;
            }

            // Fall back to looking up by WindowsIdentity
            if (!string.IsNullOrEmpty(WindowsIdentity))
            {
                var dbUser = _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.WindowsIdentity == WindowsIdentity);

                _cachedUserId = dbUser?.Id;
                return _cachedUserId;
            }

            // Fall back to looking up by username
            var username = user.FindFirst("Username")?.Value;
            if (!string.IsNullOrEmpty(username))
            {
                var dbUser = _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.Username == username);

                _cachedUserId = dbUser?.Id;
                return _cachedUserId;
            }

            return null;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin
    {
        get
        {
            if (_cachedIsAdmin.HasValue)
                return _cachedIsAdmin.Value;

            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                _cachedIsAdmin = false;
                return false;
            }

            // Check role claim first (set during cookie auth)
            if (user.IsInRole("Admin"))
            {
                _cachedIsAdmin = true;
                return true;
            }

            if (!UserId.HasValue)
            {
                _cachedIsAdmin = false;
                return false;
            }

            // Check if user has Admin role in database
            var hasAdminRole = _dbContext.UserRoles
                .AsNoTracking()
                .Any(ur => ur.UserId == UserId.Value &&
                           ur.Role.Name == "Admin");

            if (hasAdminRole)
            {
                _cachedIsAdmin = true;
                return true;
            }

            // Check if any of user's AD groups are mapped to Admin role
            var userGroupSids = AdGroupSids.ToList();
            if (userGroupSids.Any())
            {
                hasAdminRole = _dbContext.AdGroupRoleMappings
                    .AsNoTracking()
                    .Any(m => userGroupSids.Contains(m.AdGroupSid) &&
                              m.Role.Name == "Admin");
            }

            _cachedIsAdmin = hasAdminRole;
            return hasAdminRole;
        }
    }

    public IEnumerable<string> AdGroupSids
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
                return Enumerable.Empty<string>();

            // Get group SIDs from Windows claims
            return user.Claims
                .Where(c => c.Type == ClaimTypes.GroupSid ||
                           c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/groupsid")
                .Select(c => c.Value)
                .ToList();
        }
    }

    public async Task<IEnumerable<Guid>> GetAccessibleDataSourceIdsAsync()
    {
        if (!UserId.HasValue)
            return Enumerable.Empty<Guid>();

        // Admins can access all active data sources (including personal ones)
        if (IsAdmin)
        {
            return await _dbContext.DataSources
                .AsNoTracking()
                .Where(d => d.IsActive)
                .Select(d => d.Id)
                .ToListAsync();
        }

        var result = new HashSet<Guid>();

        // Get data sources user has direct permission to
        var userPermissions = await _dbContext.UserDataSourcePermissions
            .AsNoTracking()
            .Where(p => p.UserId == UserId.Value && p.CanRead)
            .Select(p => p.DataSourceId)
            .ToListAsync();

        foreach (var id in userPermissions)
            result.Add(id);

        // Get data sources user has access to via AD groups
        var userGroupSids = AdGroupSids.ToList();
        if (userGroupSids.Any())
        {
            var groupPermissions = await _dbContext.AdGroupDataSourcePermissions
                .AsNoTracking()
                .Where(p => userGroupSids.Contains(p.AdGroupSid) && p.CanRead)
                .Select(p => p.DataSourceId)
                .ToListAsync();

            foreach (var id in groupPermissions)
                result.Add(id);
        }

        // Include user's personal data source (owned by them)
        var personalSourceId = await _dbContext.DataSources
            .AsNoTracking()
            .Where(d => d.OwnerUserId == UserId.Value && d.IsActive)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        if (personalSourceId != default)
            result.Add(personalSourceId);

        return result;
    }
}
