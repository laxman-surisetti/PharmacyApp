using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Pharmacy.Api.Contracts;
using Pharmacy.Api.Domain;
using Pharmacy.Api.Infrastructure;
using Pharmacy.Api.Services;
using Pharmacy.Api.Storage;

namespace Pharmacy.Api.Tests;

public sealed class SaleServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 18);

    private readonly TempDataDirectory _temp = new();
    private readonly TestClock _clock = new(Today);
    private readonly IJsonStore<Medicine> _medicineStore;
    private readonly IJsonStore<Sale> _saleStore;
    private readonly MedicineService _medicines;
    private readonly SaleService _sales;

    public SaleServiceTests()
    {
        _medicineStore = TestFactory.Store<Medicine>(_temp.File("medicines.json"), Array.Empty<Medicine>);
        _saleStore = TestFactory.Store<Sale>(_temp.File("sales.json"), Array.Empty<Sale>);
        _medicines = new MedicineService(_medicineStore, TestFactory.Evaluator(), _clock);
        _sales = new SaleService(_medicineStore, _saleStore, _clock, NullLogger<SaleService>.Instance);
    }

    public void Dispose()
    {
        _sales.Dispose();
        _temp.Dispose();
    }

    private async Task<Guid> GivenMedicine(string name, int quantity, decimal price)
    {
        var created = await _medicines.CreateAsync(new SaveMedicineRequest
        {
            FullName = name,
            Brand = "TestBrand",
            Quantity = quantity,
            Price = price,
            ExpiryDate = Today.AddDays(400)
        });

        return created.Id;
    }

    private async Task<int> QuantityOf(Guid id)
        => (await _medicineStore.GetAllAsync()).Single(m => m.Id == id).Quantity;

    [Fact]
    public async Task Records_a_sale_and_decrements_stock()
    {
        var id = await GivenMedicine("Paracetamol 500mg Tablet", quantity: 100, price: 3.40m);

        var sale = await _sales.CreateAsync(new CreateSaleRequest
        {
            Lines = [new CreateSaleLineRequest { MedicineId = id, Quantity = 3 }],
            SoldBy = "Nimal"
        });

        Assert.Equal(97, await QuantityOf(id));
        Assert.Equal(10.20m, sale.TotalAmount);
        Assert.Equal("Nimal", sale.SoldBy);
        Assert.Single(sale.Lines);
        Assert.Equal(3.40m, sale.Lines[0].UnitPrice);
        Assert.Equal(10.20m, sale.Lines[0].LineTotal);
    }

    [Fact]
    public async Task Refuses_to_sell_more_than_is_in_stock_and_changes_nothing()
    {
        var id = await GivenMedicine("Insulin Glargine 100IU/ml", quantity: 4, price: 2450.00m);

        var error = await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(new CreateSaleRequest
        {
            Lines = [new CreateSaleLineRequest { MedicineId = id, Quantity = 5 }]
        }));

        Assert.Equal(DomainErrorKind.Conflict, error.Kind);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.Equal(4, await QuantityOf(id));
        Assert.Empty(await _saleStore.GetAllAsync());
    }

    [Fact]
    public async Task A_rejected_line_rolls_back_the_whole_basket()
    {
        var good = await GivenMedicine("Paracetamol 500mg Tablet", quantity: 100, price: 3.40m);
        var short_ = await GivenMedicine("Salbutamol Inhaler 100mcg", quantity: 1, price: 890.00m);

        await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(new CreateSaleRequest
        {
            Lines =
            [
                new CreateSaleLineRequest { MedicineId = good, Quantity = 2 },
                new CreateSaleLineRequest { MedicineId = short_, Quantity = 5 }
            ]
        }));

        // The first line must not have been taken out of stock.
        Assert.Equal(100, await QuantityOf(good));
        Assert.Equal(1, await QuantityOf(short_));
    }

    [Fact]
    public async Task The_same_medicine_twice_in_one_basket_is_one_line_of_the_summed_quantity()
    {
        var id = await GivenMedicine("ORS Sachet 20.5g", quantity: 10, price: 1.95m);

        var sale = await _sales.CreateAsync(new CreateSaleRequest
        {
            Lines =
            [
                new CreateSaleLineRequest { MedicineId = id, Quantity = 4 },
                new CreateSaleLineRequest { MedicineId = id, Quantity = 3 }
            ]
        });

        Assert.Single(sale.Lines);
        Assert.Equal(7, sale.Lines[0].Quantity);
        Assert.Equal(3, await QuantityOf(id));
    }

    [Fact]
    public async Task Two_tills_cannot_both_sell_the_last_unit()
    {
        var id = await GivenMedicine("Last One", quantity: 1, price: 5.00m);

        var attempts = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            try
            {
                await _sales.CreateAsync(new CreateSaleRequest
                {
                    Lines = [new CreateSaleLineRequest { MedicineId = id, Quantity = 1 }]
                });
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(succeeded => succeeded));
        Assert.Equal(0, await QuantityOf(id));
        Assert.Single(await _saleStore.GetAllAsync());
    }

    [Fact]
    public async Task Sale_numbers_are_sequential_within_the_year()
    {
        var id = await GivenMedicine("Vitamin D3 1000IU Capsule", quantity: 100, price: 18.90m);

        var first = await _sales.CreateAsync(Basket(id));
        var second = await _sales.CreateAsync(Basket(id));

        Assert.Equal($"SL-{Today.Year}-000001", first.SaleNumber);
        Assert.Equal($"SL-{Today.Year}-000002", second.SaleNumber);
    }

    [Fact]
    public async Task A_receipt_keeps_the_price_it_was_sold_at_when_the_catalogue_is_repriced()
    {
        var id = await GivenMedicine("Azithromycin 500mg Tablet", quantity: 50, price: 34.00m);
        var sale = await _sales.CreateAsync(Basket(id, quantity: 2));

        await _medicines.UpdateAsync(id, new SaveMedicineRequest
        {
            FullName = "Azithromycin 500mg Tablet",
            Brand = "TestBrand",
            Quantity = 48,
            Price = 99.00m,
            ExpiryDate = Today.AddDays(400)
        });

        var stored = await _sales.GetByIdAsync(sale.Id);

        Assert.Equal(34.00m, stored.Lines[0].UnitPrice);
        Assert.Equal(68.00m, stored.TotalAmount);
    }

    [Fact]
    public async Task Selling_a_medicine_that_does_not_exist_is_a_not_found()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(Basket(Guid.NewGuid())));

        Assert.Equal(DomainErrorKind.NotFound, error.Kind);
    }

    [Fact]
    public async Task An_empty_basket_is_refused()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _sales.CreateAsync(new CreateSaleRequest { Lines = [] }));

        Assert.Equal(DomainErrorKind.Rule, error.Kind);
    }

    [Fact]
    public async Task History_is_returned_newest_first_and_paged()
    {
        var id = await GivenMedicine("ORS Sachet 20.5g", quantity: 100, price: 1.95m);

        for (var i = 0; i < 5; i++)
        {
            _clock.Today = Today.AddDays(i);
            await _sales.CreateAsync(Basket(id));
        }

        var page = await _sales.SearchAsync(new SaleQuery { Page = 1, PageSize = 2 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.Items[0].SoldAtUtc >= page.Items[1].SoldAtUtc);
    }

    private static CreateSaleRequest Basket(Guid medicineId, int quantity = 1) => new()
    {
        Lines = [new CreateSaleLineRequest { MedicineId = medicineId, Quantity = quantity }]
    };
}
