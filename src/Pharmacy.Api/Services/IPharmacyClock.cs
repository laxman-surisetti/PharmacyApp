using Microsoft.Extensions.Options;
using Pharmacy.Api.Configuration;

namespace Pharmacy.Api.Services;

/// <summary>
/// "Today" in the pharmacy's own time zone. Injected rather than read from
/// <see cref="DateTime.Today"/> directly so the expiry rules can be tested at their
/// boundaries without waiting for midnight.
/// </summary>
public interface IPharmacyClock
{
    DateOnly Today { get; }

    DateTimeOffset UtcNow { get; }
}

public sealed class PharmacyClock : IPharmacyClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public PharmacyClock(TimeProvider timeProvider, IOptions<PharmacyOptions> options, ILogger<PharmacyClock> logger)
    {
        _timeProvider = timeProvider;

        var id = options.Value.TimeZone;
        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning(ex, "Time zone '{TimeZone}' was not found on this machine - falling back to UTC.", id);
            _timeZone = TimeZoneInfo.Utc;
        }
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime);
}
