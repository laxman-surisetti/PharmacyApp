using Pharmacy.Api.Contracts;
using Pharmacy.Api.Domain;
using Pharmacy.Api.Infrastructure;
using Pharmacy.Api.Storage;

namespace Pharmacy.Api.Services;

public interface IMedicineService
{
    Task<PagedResult<MedicineListItemDto>> SearchAsync(MedicineQuery query, CancellationToken ct = default);

    Task<MedicineDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<MedicineDto> CreateAsync(SaveMedicineRequest request, CancellationToken ct = default);

    Task<MedicineDto> UpdateAsync(Guid id, SaveMedicineRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<InventorySummaryDto> GetSummaryAsync(CancellationToken ct = default);
}

public sealed class MedicineService : IMedicineService
{
    private readonly IJsonStore<Medicine> _store;
    private readonly MedicineStatusEvaluator _evaluator;
    private readonly IPharmacyClock _clock;

    public MedicineService(IJsonStore<Medicine> store, MedicineStatusEvaluator evaluator, IPharmacyClock clock)
    {
        _store = store;
        _evaluator = evaluator;
        _clock = clock;
    }

    public async Task<PagedResult<MedicineListItemDto>> SearchAsync(MedicineQuery query, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var all = await _store.GetAllAsync(ct).ConfigureAwait(false);

        // FR-07: case-insensitive "contains" on the medicine name, applied on the server so
        // that paging stays correct - filtering after paging would drop matches.
        IEnumerable<Medicine> filtered = all;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            filtered = filtered.Where(m =>
                m.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var projected = filtered
            .Select(m => ToListItem(m, _evaluator.Evaluate(m, today)))
            .ToList();

        var totalCount = projected.Count;
        var sorted = Sort(projected, query.SortBy, query.SortDir);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<MedicineListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<MedicineDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var all = await _store.GetAllAsync(ct).ConfigureAwait(false);
        var medicine = all.FirstOrDefault(m => m.Id == id)
                       ?? throw DomainException.NotFound("Medicine", id);

        return ToDto(medicine, _evaluator.Evaluate(medicine, _clock.Today));
    }

    public async Task<MedicineDto> CreateAsync(SaveMedicineRequest request, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var created = await _store.MutateAsync(medicines =>
        {
            GuardAgainstDuplicate(medicines, request.FullName, request.Brand, excludingId: null);

            var medicine = new Medicine
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Brand = request.Brand.Trim(),
                ExpiryDate = request.ExpiryDate,
                Quantity = request.Quantity,
                Price = RoundMoney(request.Price),
                Version = 1,
                CreatedUtc = now,
                ModifiedUtc = now
            };

            medicines.Add(medicine);
            return medicine;
        }, ct).ConfigureAwait(false);

        return ToDto(created, _evaluator.Evaluate(created, _clock.Today));
    }

    public async Task<MedicineDto> UpdateAsync(Guid id, SaveMedicineRequest request, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var updated = await _store.MutateAsync(medicines =>
        {
            var medicine = medicines.FirstOrDefault(m => m.Id == id)
                           ?? throw DomainException.NotFound("Medicine", id);

            GuardAgainstDuplicate(medicines, request.FullName, request.Brand, excludingId: id);

            medicine.FullName = request.FullName.Trim();
            medicine.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            medicine.Brand = request.Brand.Trim();
            medicine.ExpiryDate = request.ExpiryDate;
            medicine.Quantity = request.Quantity;
            medicine.Price = RoundMoney(request.Price);
            medicine.Version++;
            medicine.ModifiedUtc = now;

            return medicine;
        }, ct).ConfigureAwait(false);

        return ToDto(updated, _evaluator.Evaluate(updated, _clock.Today));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _store.MutateAsync(medicines =>
        {
            var medicine = medicines.FirstOrDefault(m => m.Id == id)
                           ?? throw DomainException.NotFound("Medicine", id);

            medicines.Remove(medicine);
            return true;
        }, ct).ConfigureAwait(false);
    }

    public async Task<InventorySummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var all = await _store.GetAllAsync(ct).ConfigureAwait(false);

        var statuses = all
            .Select(m => (Medicine: m, Status: _evaluator.Evaluate(m, today)))
            .ToList();

        return new InventorySummaryDto(
            TotalMedicines: statuses.Count,
            ExpiredCount: statuses.Count(x => x.Status.ExpiryStatus is ExpiryStatus.Expired),
            ExpiringSoonCount: statuses.Count(x => x.Status.ExpiryStatus is ExpiryStatus.ExpiringSoon),
            LowStockCount: statuses.Count(x => x.Status.StockStatus is StockStatus.Low),
            OutOfStockCount: statuses.Count(x => x.Status.StockStatus is StockStatus.OutOfStock),
            TotalStockValue: RoundMoney(statuses.Sum(x => x.Medicine.Price * x.Medicine.Quantity)));
    }

    /// <summary>
    /// Name is unique per brand, not globally: two suppliers legitimately sell
    /// "Paracetamol 500mg Tablet", and the pharmacy stocks both.
    /// </summary>
    private static void GuardAgainstDuplicate(
        IEnumerable<Medicine> medicines,
        string fullName,
        string brand,
        Guid? excludingId)
    {
        var name = fullName.Trim();
        var brandName = brand.Trim();

        var clash = medicines.Any(m =>
            (excludingId is null || m.Id != excludingId) &&
            string.Equals(m.FullName, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.Brand, brandName, StringComparison.OrdinalIgnoreCase));

        if (clash)
        {
            throw new DomainException(
                "A medicine with this name already exists for this brand.",
                DomainErrorKind.Conflict,
                $"'{name}' is already registered under brand '{brandName}'.");
        }
    }

    private static IEnumerable<MedicineListItemDto> Sort(
        List<MedicineListItemDto> items,
        string? sortBy,
        string? sortDir)
    {
        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedEnumerable<MedicineListItemDto> ordered = (sortBy ?? "severity").ToLowerInvariant() switch
        {
            "name" or "fullname" => Order(items, m => m.FullName, descending),
            "brand" => Order(items, m => m.Brand, descending),
            "expirydate" => Order(items, m => m.ExpiryDate, descending),
            "quantity" => Order(items, m => m.Quantity, descending),
            "price" => Order(items, m => m.Price, descending),
            _ => Order(items, m => (int)m.RowSeverity, descending)
                // Within a colour band, the one closest to expiry is the one to deal with first.
                .ThenBy(m => m.DaysToExpiry)
        };

        return ordered.ThenBy(m => m.FullName, StringComparer.OrdinalIgnoreCase);
    }

    private static IOrderedEnumerable<MedicineListItemDto> Order<TKey>(
        IEnumerable<MedicineListItemDto> items,
        Func<MedicineListItemDto, TKey> keySelector,
        bool descending)
        => descending ? items.OrderByDescending(keySelector) : items.OrderBy(keySelector);

    internal static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static MedicineListItemDto ToListItem(Medicine m, MedicineStatus status) => new(
        m.Id,
        m.FullName,
        m.Brand,
        m.ExpiryDate,
        status.DaysToExpiry,
        m.Quantity,
        m.Price,
        status.ExpiryStatus,
        status.StockStatus,
        status.RowSeverity,
        m.Version);

    private static MedicineDto ToDto(Medicine m, MedicineStatus status) => new(
        m.Id,
        m.FullName,
        m.Notes,
        m.Brand,
        m.ExpiryDate,
        status.DaysToExpiry,
        m.Quantity,
        m.Price,
        status.ExpiryStatus,
        status.StockStatus,
        status.RowSeverity,
        m.Version,
        m.CreatedUtc,
        m.ModifiedUtc);
}
