using Microsoft.AspNetCore.Http;
using Pharmacy.Api.Contracts;
using Pharmacy.Api.Domain;
using Pharmacy.Api.Infrastructure;
using Pharmacy.Api.Services;
using Pharmacy.Api.Storage;

namespace Pharmacy.Api.Tests;

public sealed class MedicineServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 18);

    private readonly TempDataDirectory _temp = new();
    private readonly TestClock _clock = new(Today);
    private readonly IJsonStore<Medicine> _store;
    private readonly MedicineService _service;

    public MedicineServiceTests()
    {
        _store = TestFactory.Store<Medicine>(_temp.File("medicines.json"), Array.Empty<Medicine>);
        _service = new MedicineService(_store, TestFactory.Evaluator(), _clock);
    }

    public void Dispose() => _temp.Dispose();

    private static SaveMedicineRequest Request(
        string fullName = "Paracetamol 500mg Tablet",
        string brand = "HealWell",
        int quantity = 100,
        decimal price = 3.40m,
        DateOnly? expiry = null)
        => new()
        {
            FullName = fullName,
            Brand = brand,
            Quantity = quantity,
            Price = price,
            ExpiryDate = expiry ?? Today.AddDays(365),
            Notes = "  spaced notes  "
        };

    [Fact]
    public async Task Adds_a_medicine_and_returns_its_computed_status()
    {
        var created = await _service.CreateAsync(Request(quantity: 6, expiry: Today.AddDays(10)));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(1, created.Version);
        Assert.Equal(10, created.DaysToExpiry);
        Assert.Equal(ExpiryStatus.ExpiringSoon, created.ExpiryStatus);
        Assert.Equal(StockStatus.Low, created.StockStatus);
        Assert.Equal(RowSeverity.Critical, created.RowSeverity);
        Assert.Equal("spaced notes", created.Notes);
    }

    [Fact]
    public async Task Rounds_price_to_two_decimal_places()
    {
        var created = await _service.CreateAsync(Request(price: 12.4567m));

        Assert.Equal(12.46m, created.Price);
    }

    [Fact]
    public async Task Rejects_the_same_name_under_the_same_brand()
    {
        await _service.CreateAsync(Request());

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(Request(fullName: "paracetamol 500MG tablet")));

        Assert.Equal(DomainErrorKind.Conflict, error.Kind);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);

        // The rejected medicine must not have been written.
        Assert.Single(await _store.GetAllAsync());
    }

    [Fact]
    public async Task Allows_the_same_name_under_a_different_brand()
    {
        await _service.CreateAsync(Request(brand: "HealWell"));
        var second = await _service.CreateAsync(Request(brand: "PharmaPlus"));

        Assert.Equal("PharmaPlus", second.Brand);
        Assert.Equal(2, (await _store.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Searching_matches_part_of_the_name_regardless_of_case()
    {
        await _service.CreateAsync(Request(fullName: "Amoxicillin 500mg Capsule", brand: "MediCore"));
        await _service.CreateAsync(Request(fullName: "Amoxicillin 250mg Syrup", brand: "PharmaPlus"));
        await _service.CreateAsync(Request(fullName: "Paracetamol 500mg Tablet", brand: "HealWell"));

        var page = await _service.SearchAsync(new MedicineQuery { Search = "AMOXI" });

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item => Assert.Contains("Amoxicillin", item.FullName));
    }

    [Fact]
    public async Task Search_is_applied_before_paging_so_totals_stay_honest()
    {
        for (var i = 0; i < 12; i++)
        {
            await _service.CreateAsync(Request(fullName: $"Amoxicillin variant {i}", brand: $"Brand {i}"));
        }

        await _service.CreateAsync(Request(fullName: "Paracetamol 500mg Tablet"));

        var page = await _service.SearchAsync(new MedicineQuery { Search = "amoxicillin", Page = 2, PageSize = 5 });

        Assert.Equal(12, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(5, page.Items.Count);
    }

    [Fact]
    public async Task Default_sort_puts_the_red_rows_first_and_the_soonest_expiry_at_the_top()
    {
        await _service.CreateAsync(Request(fullName: "Healthy", brand: "A"));
        await _service.CreateAsync(Request(fullName: "Low stock", brand: "B", quantity: 3));
        await _service.CreateAsync(Request(fullName: "Expiring in 20", brand: "C", expiry: Today.AddDays(20)));
        await _service.CreateAsync(Request(fullName: "Expiring in 5", brand: "D", expiry: Today.AddDays(5)));

        var page = await _service.SearchAsync(new MedicineQuery());

        Assert.Equal(
            new[] { "Expiring in 5", "Expiring in 20", "Low stock", "Healthy" },
            page.Items.Select(i => i.FullName).ToArray());
    }

    [Fact]
    public async Task Sorts_by_price_when_asked_to()
    {
        await _service.CreateAsync(Request(fullName: "Cheap", brand: "A", price: 1.10m));
        await _service.CreateAsync(Request(fullName: "Dear", brand: "B", price: 990.00m));
        await _service.CreateAsync(Request(fullName: "Middling", brand: "C", price: 25.00m));

        var page = await _service.SearchAsync(new MedicineQuery { SortBy = "price", SortDir = "asc" });

        Assert.Equal(new[] { "Cheap", "Middling", "Dear" }, page.Items.Select(i => i.FullName).ToArray());
    }

    [Fact]
    public async Task Updating_bumps_the_version_and_keeps_the_identifier()
    {
        var created = await _service.CreateAsync(Request());

        var updated = await _service.UpdateAsync(created.Id, Request(quantity: 5, price: 4.00m));

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(2, updated.Version);
        Assert.Equal(5, updated.Quantity);
        Assert.Equal(StockStatus.Low, updated.StockStatus);
    }

    [Fact]
    public async Task Missing_identifiers_are_reported_as_not_found()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => _service.GetByIdAsync(Guid.NewGuid()));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
        Assert.Equal(StatusCodes.Status404NotFound, error.StatusCode);
    }

    [Fact]
    public async Task Summary_counts_each_condition_independently()
    {
        await _service.CreateAsync(Request(fullName: "Expired", brand: "A", expiry: Today.AddDays(-2), quantity: 50));
        await _service.CreateAsync(Request(fullName: "Expiring", brand: "B", expiry: Today.AddDays(10), quantity: 50));
        await _service.CreateAsync(Request(fullName: "Low", brand: "C", quantity: 4, price: 2.50m));
        await _service.CreateAsync(Request(fullName: "Empty", brand: "D", quantity: 0));
        await _service.CreateAsync(Request(fullName: "Fine", brand: "E", quantity: 10, price: 10.00m));

        var summary = await _service.GetSummaryAsync();

        Assert.Equal(5, summary.TotalMedicines);
        Assert.Equal(1, summary.ExpiredCount);
        Assert.Equal(1, summary.ExpiringSoonCount);
        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(1, summary.OutOfStockCount);
        // 50*3.40 + 50*3.40 + 4*2.50 + 0 + 10*10.00 = 170 + 170 + 10 + 100
        Assert.Equal(450.00m, summary.TotalStockValue);
    }

    [Fact]
    public async Task A_row_turns_red_purely_by_the_passage_of_time()
    {
        var created = await _service.CreateAsync(Request(expiry: Today.AddDays(40)));
        Assert.Equal(RowSeverity.Normal, created.RowSeverity);

        _clock.Today = Today.AddDays(15);

        var refreshed = await _service.GetByIdAsync(created.Id);
        Assert.Equal(RowSeverity.Critical, refreshed.RowSeverity);
    }
}
