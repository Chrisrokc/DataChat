using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using MediatR;

namespace DataChat.Application.Features.Chat.Commands;

public record CreateChatCommand(string? Title = null) : IRequest<ChatDto>;

public class CreateChatCommandHandler : IRequestHandler<CreateChatCommand, ChatDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public CreateChatCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ChatDto> Handle(CreateChatCommand request, CancellationToken cancellationToken)
    {
        var chat = new Domain.Entities.Chat
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User not authenticated"),
            Title = request.Title ?? "New Chat",
            CreatedAt = _dateTime.UtcNow,
            CreatedBy = _currentUser.WindowsIdentity
        };

        _context.Chats.Add(chat);
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatDto(
            chat.Id,
            chat.Title,
            chat.CreatedAt,
            chat.UpdatedAt,
            0);
    }
}
