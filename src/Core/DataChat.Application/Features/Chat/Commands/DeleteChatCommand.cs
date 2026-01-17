using DataChat.Application.Common.Interfaces;
using DataChat.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Application.Features.Chat.Commands;

public record DeleteChatCommand(Guid ChatId) : IRequest<Result>;

public class DeleteChatCommandHandler : IRequestHandler<DeleteChatCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTime;

    public DeleteChatCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IDateTimeService dateTime)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(DeleteChatCommand request, CancellationToken cancellationToken)
    {
        var chat = await _context.Chats
            .FirstOrDefaultAsync(c => c.Id == request.ChatId &&
                                      c.UserId == _currentUser.UserId &&
                                      !c.IsDeleted, cancellationToken);

        if (chat == null)
            return Result.Failure("Chat not found or you don't have access to it.");

        chat.IsDeleted = true;
        chat.DeletedAt = _dateTime.UtcNow;
        chat.UpdatedAt = _dateTime.UtcNow;
        chat.UpdatedBy = _currentUser.WindowsIdentity;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
