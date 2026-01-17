using DataChat.Application.Common.Interfaces;
using DataChat.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Application.Features.Chat.Commands;

public record RenameChatCommand(Guid ChatId, string NewTitle) : IRequest<Result>;

public class RenameChatCommandHandler : IRequestHandler<RenameChatCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public RenameChatCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(RenameChatCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewTitle))
            return Result.Failure("Title cannot be empty.");

        var chat = await _context.Chats
            .FirstOrDefaultAsync(c => c.Id == request.ChatId &&
                                      c.UserId == _currentUser.UserId &&
                                      !c.IsDeleted, cancellationToken);

        if (chat == null)
            return Result.Failure("Chat not found or you don't have access to it.");

        chat.Title = request.NewTitle.Trim();
        chat.UpdatedAt = _dateTime.UtcNow;
        chat.UpdatedBy = _currentUser.WindowsIdentity;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
