using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Application.Features.Chat.Queries;

public record GetUserChatsQuery : IRequest<IEnumerable<ChatDto>>;

public class GetUserChatsQueryHandler : IRequestHandler<GetUserChatsQuery, IEnumerable<ChatDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetUserChatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<ChatDto>> Handle(GetUserChatsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User not authenticated");

        var chats = await _context.Chats
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new ChatDto(
                c.Id,
                c.Title,
                c.CreatedAt,
                c.UpdatedAt,
                c.Messages.Count))
            .ToListAsync(cancellationToken);

        return chats;
    }
}
