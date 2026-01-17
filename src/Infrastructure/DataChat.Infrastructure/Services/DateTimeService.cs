using DataChat.Application.Common.Interfaces;

namespace DataChat.Infrastructure.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
