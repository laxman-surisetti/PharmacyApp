using Pharmacy.Api.Contracts;
using Pharmacy.Api.Domain;
using Pharmacy.Api.Infrastructure;
using Pharmacy.Api.Storage;

namespace Pharmacy.Api.Services;

public interface ISaleService
{
    Task<SaleDto> CreateAsync(CreateSaleRequest request, CancellationToken ct = default);

    Task<PagedResult<SaleDto>> SearchAsync(SaleQuery query, CancellationToken ct = default);

    Task<SaleDto> GetByIdAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Records sales and adjusts stock (FR-06).
///
/// A sale touches two stores, and JSON files give no cross-file transaction. The order of
/// operations is therefore deliberate: stock - the scarce resource two counters can fight
/// over - is decremented first under a single-writer lock, and only then is the sale
/// appended. If appending the sale fails, the stock decrement is compensated. A production
/// system would use the transactional outbox described in the design document; this is the
/// honest small-scale equivalent, and the failure is logged loudly if compensation itself
/// fails.
/// </summary>
public sealed class SaleService : ISaleService, IDisposable
{
    private readonly IJsonStore<Medicine> _medicines;
    private readonly IJsonStore<Sale> _sales;
    private readonly IPharmacyClock _clock;
    private readonly ILogger<SaleService> _logger;

    /// <summary>Serialises whole sales, so the two-store sequence above cannot interleave.</summary>
    private readonly SemaphoreSlim _saleGate = new(1, 1);

    public SaleService(
        IJsonStore<Medicine> medicines,
        IJsonStore<Sale> sales,
        IPharmacyClock clock,
        ILogger<SaleService> logger)
    {
        _medicines = medicines;
        _sales = sales;
        _clock = clock;
        _logger = logger;
    }

    public async Task<SaleDto> CreateAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
        // The same medicine scanned twice is one line of two units, not two lines that each
        // pass the stock check on their own.
        var requestedQuantities = new Dictionary<Guid, int>();
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new DomainException("Every sale line must have a quantity of at least 1.");
            }

            requestedQuantities[line.MedicineId] = requestedQuantities.GetValueOrDefault(line.MedicineId) + line.Quantity;
        }

        if (requestedQuantities.Count == 0)
        {
            throw new DomainException("A sale must have at least one line.");
        }

        await _saleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var soldLines = await ReserveStockAsync(requestedQuantities, ct).ConfigureAwait(false);

            try
            {
                var sale = await AppendSaleAsync(request, soldLines, ct).ConfigureAwait(false);
                return ToDto(sale);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to append the sale record - restoring the stock that was decremented.");
                await CompensateStockAsync(requestedQuantities).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _saleGate.Release();
        }
    }

    private async Task<List<SaleLine>> ReserveStockAsync(
        IReadOnlyDictionary<Guid, int> requestedQuantities,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;

        return await _medicines.MutateAsync(medicines =>
        {
            // Pass 1 - validate everything before changing anything, so a rejected sale
            // never leaves half the basket decremented.
            var targets = new List<(Medicine Medicine, int Quantity)>(requestedQuantities.Count);
            foreach (var (medicineId, quantity) in requestedQuantities)
            {
                var medicine = medicines.FirstOrDefault(m => m.Id == medicineId)
                               ?? throw DomainException.NotFound("Medicine", medicineId);

                if (medicine.Quantity < quantity)
                {
                    throw new DomainException(
                        "Not enough stock to complete this sale.",
                        DomainErrorKind.Conflict,
                        $"'{medicine.FullName}' has {medicine.Quantity} unit(s) in stock but {quantity} were requested.");
                }

                targets.Add((medicine, quantity));
            }

            // Pass 2 - apply.
            var lines = new List<SaleLine>(targets.Count);
            foreach (var (medicine, quantity) in targets)
            {
                medicine.Quantity -= quantity;
                medicine.Version++;
                medicine.ModifiedUtc = now;

                lines.Add(new SaleLine
                {
                    MedicineId = medicine.Id,
                    MedicineName = medicine.FullName,
                    Brand = medicine.Brand,
                    Quantity = quantity,
                    UnitPrice = medicine.Price,
                    LineTotal = MedicineService.RoundMoney(medicine.Price * quantity)
                });
            }

            return lines;
        }, ct).ConfigureAwait(false);
    }

    private async Task<Sale> AppendSaleAsync(
        CreateSaleRequest request,
        List<SaleLine> lines,
        CancellationToken ct)
    {
        var soldAt = _clock.UtcNow;
        var year = _clock.Today.Year;

        return await _sales.MutateAsync(sales =>
        {
            var prefix = $"SL-{year}-";
            var sequence = sales.Count(s => s.SaleNumber.StartsWith(prefix, StringComparison.Ordinal)) + 1;

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                SaleNumber = prefix + sequence.ToString("D6"),
                SoldAtUtc = soldAt,
                SoldBy = string.IsNullOrWhiteSpace(request.SoldBy) ? null : request.SoldBy.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Lines = lines,
                TotalAmount = MedicineService.RoundMoney(lines.Sum(l => l.LineTotal))
            };

            sales.Add(sale);
            return sale;
        }, ct).ConfigureAwait(false);
    }

    private async Task CompensateStockAsync(IReadOnlyDictionary<Guid, int> requestedQuantities)
    {
        try
        {
            await _medicines.MutateAsync(medicines =>
            {
                foreach (var (medicineId, quantity) in requestedQuantities)
                {
                    var medicine = medicines.FirstOrDefault(m => m.Id == medicineId);
                    if (medicine is null)
                    {
                        continue;
                    }

                    medicine.Quantity += quantity;
                    medicine.Version++;
                }

                return true;
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Nothing left to do but make the divergence impossible to miss.
            _logger.LogCritical(
                ex,
                "Stock compensation failed. medicines.json may under-report stock for {MedicineIds}.",
                string.Join(", ", requestedQuantities.Keys));
        }
    }

    public async Task<PagedResult<SaleDto>> SearchAsync(SaleQuery query, CancellationToken ct = default)
    {
        var all = await _sales.GetAllAsync(ct).ConfigureAwait(false);

        IEnumerable<Sale> filtered = all;

        if (query.From is { } from)
        {
            filtered = filtered.Where(s => DateOnly.FromDateTime(s.SoldAtUtc.UtcDateTime) >= from);
        }

        if (query.To is { } to)
        {
            filtered = filtered.Where(s => DateOnly.FromDateTime(s.SoldAtUtc.UtcDateTime) <= to);
        }

        var ordered = filtered
            .OrderByDescending(s => s.SoldAtUtc)
            .ThenByDescending(s => s.SaleNumber, StringComparer.Ordinal)
            .ToList();

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToList();

        return new PagedResult<SaleDto>(items, page, pageSize, ordered.Count);
    }

    public async Task<SaleDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var all = await _sales.GetAllAsync(ct).ConfigureAwait(false);
        var sale = all.FirstOrDefault(s => s.Id == id)
                   ?? throw DomainException.NotFound("Sale", id);

        return ToDto(sale);
    }

    private static SaleDto ToDto(Sale sale) => new(
        sale.Id,
        sale.SaleNumber,
        sale.SoldAtUtc,
        sale.SoldBy,
        sale.Notes,
        sale.Lines
            .Select(l => new SaleLineDto(l.MedicineId, l.MedicineName, l.Brand, l.Quantity, l.UnitPrice, l.LineTotal))
            .ToList(),
        sale.TotalAmount);

    public void Dispose() => _saleGate.Dispose();
}
