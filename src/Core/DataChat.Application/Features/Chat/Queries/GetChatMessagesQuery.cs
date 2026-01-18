using System.Text.Json;
using DataChat.Application.Common.Interfaces;
using DataChat.Application.Features.Chat.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DataChat.Application.Features.Chat.Queries;

public record GetChatMessagesQuery(Guid ChatId) : IRequest<ChatDetailDto?>;

public class GetChatMessagesQueryHandler : IRequestHandler<GetChatMessagesQuery, ChatDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetChatMessagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<ChatDetailDto?> Handle(GetChatMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User not authenticated");

        var chat = await _context.Chats
            .Where(c => c.Id == request.ChatId && c.UserId == userId && !c.IsDeleted)
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (chat == null)
            return null;

        return new ChatDetailDto(
            chat.Id,
            chat.Title,
            chat.CreatedAt,
            chat.UpdatedAt,
            chat.Messages.Select(m => new ChatMessageDto(
                m.Id,
                m.Role,
                m.Content,
                m.CreatedAt,
                null,
                ParseAttachments(m.AttachmentsJson),
                ParseSourceChunks(m.SourceChunksJson))));
    }

    private static IEnumerable<MessageAttachmentDto>? ParseAttachments(string? attachmentsJson)
    {
        if (string.IsNullOrEmpty(attachmentsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<MessageAttachmentDto>>(attachmentsJson);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<SourceChunkDto>? ParseSourceChunks(string? sourceChunksJson)
    {
        if (string.IsNullOrEmpty(sourceChunksJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<SourceChunkDto>>(sourceChunksJson);
        }
        catch
        {
            return null;
        }
    }
}
