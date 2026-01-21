namespace DataChat.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? WindowsIdentity { get; }
    string? DisplayName { get; }
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool CanSelectDataSources { get; }
    IEnumerable<string> AdGroupSids { get; }
    Task<IEnumerable<Guid>> GetAccessibleDataSourceIdsAsync();
}
