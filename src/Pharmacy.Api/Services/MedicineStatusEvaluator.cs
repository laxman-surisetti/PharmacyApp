using Microsoft.Extensions.Options;
using Pharmacy.Api.Configuration;
using Pharmacy.Api.Domain;

namespace Pharmacy.Api.Services;

/// <summary>The computed traffic-light state of one medicine on one day.</summary>
public readonly record struct MedicineStatus(
    int DaysToExpiry,
    ExpiryStatus ExpiryStatus,
    StockStatus StockStatus,
    RowSeverity RowSeverity);

/// <summary>
/// The single home of FR-04 and FR-05:
///
///   red    - expiry date is less than 30 days away (including already expired)
///   yellow - quantity in stock is less than 10
///
/// Red takes precedence when both apply: dispensing expired stock is a safety issue,
/// running low is a purchasing issue. The rule lives on the server so the UI cannot
/// disagree with it and a workstation with a wrong clock cannot mis-colour a row.
/// </summary>
public sealed class MedicineStatusEvaluator
{
    private readonly PharmacyOptions _options;

    public MedicineStatusEvaluator(IOptions<PharmacyOptions> options)
    {
        _options = options.Value;
    }

    public MedicineStatus Evaluate(Medicine medicine, DateOnly today)
        => Evaluate(medicine.ExpiryDate, medicine.Quantity, today);

    public MedicineStatus Evaluate(DateOnly expiryDate, int quantity, DateOnly today)
    {
        var daysToExpiry = expiryDate.DayNumber - today.DayNumber;

        var expiryStatus = daysToExpiry switch
        {
            < 0 => ExpiryStatus.Expired,
            _ when daysToExpiry < _options.ExpiryWarningDays => ExpiryStatus.ExpiringSoon,
            _ => ExpiryStatus.Ok
        };

        var stockStatus = quantity switch
        {
            <= 0 => StockStatus.OutOfStock,
            _ when quantity < _options.LowStockThreshold => StockStatus.Low,
            _ => StockStatus.Ok
        };

        var severity = expiryStatus is not ExpiryStatus.Ok
            ? RowSeverity.Critical
            : stockStatus is not StockStatus.Ok
                ? RowSeverity.Warning
                : RowSeverity.Normal;

        return new MedicineStatus(daysToExpiry, expiryStatus, stockStatus, severity);
    }
}
