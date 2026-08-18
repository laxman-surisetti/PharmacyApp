using Pharmacy.Api.Domain;

namespace Pharmacy.Api.Tests;

/// <summary>
/// The colour rules are the requirement most likely to be got subtly wrong, so they are
/// tested at their boundaries rather than in the middle of each range.
/// </summary>
public sealed class MedicineStatusEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 18);

    [Theory]
    [InlineData(-30, ExpiryStatus.Expired)]   // long gone
    [InlineData(-1, ExpiryStatus.Expired)]    // yesterday
    [InlineData(0, ExpiryStatus.ExpiringSoon)]  // expires today - still sellable, still red
    [InlineData(1, ExpiryStatus.ExpiringSoon)]
    [InlineData(29, ExpiryStatus.ExpiringSoon)] // last day inside the window
    [InlineData(30, ExpiryStatus.Ok)]           // "less than 30 days" excludes exactly 30
    [InlineData(31, ExpiryStatus.Ok)]
    public void Classifies_expiry_at_the_boundaries(int daysFromToday, ExpiryStatus expected)
    {
        var status = TestFactory.Evaluator().Evaluate(Today.AddDays(daysFromToday), quantity: 100, today: Today);

        Assert.Equal(expected, status.ExpiryStatus);
        Assert.Equal(daysFromToday, status.DaysToExpiry);
    }

    [Theory]
    [InlineData(0, StockStatus.OutOfStock)]
    [InlineData(1, StockStatus.Low)]
    [InlineData(9, StockStatus.Low)]   // last quantity inside the window
    [InlineData(10, StockStatus.Ok)]   // "less than 10" excludes exactly 10
    [InlineData(11, StockStatus.Ok)]
    public void Classifies_stock_at_the_boundaries(int quantity, StockStatus expected)
    {
        var status = TestFactory.Evaluator().Evaluate(Today.AddDays(365), quantity, Today);

        Assert.Equal(expected, status.StockStatus);
    }

    [Fact]
    public void Paints_red_when_expiry_is_close()
    {
        var status = TestFactory.Evaluator().Evaluate(Today.AddDays(18), quantity: 100, today: Today);

        Assert.Equal(RowSeverity.Critical, status.RowSeverity);
    }

    [Fact]
    public void Paints_yellow_when_only_stock_is_low()
    {
        var status = TestFactory.Evaluator().Evaluate(Today.AddDays(365), quantity: 6, today: Today);

        Assert.Equal(RowSeverity.Warning, status.RowSeverity);
    }

    [Fact]
    public void Red_wins_when_a_medicine_is_both_expiring_and_low()
    {
        // An expiry problem is a patient-safety problem; running low is a purchasing problem.
        var status = TestFactory.Evaluator().Evaluate(Today.AddDays(3), quantity: 2, today: Today);

        Assert.Equal(RowSeverity.Critical, status.RowSeverity);
        Assert.Equal(ExpiryStatus.ExpiringSoon, status.ExpiryStatus);
        Assert.Equal(StockStatus.Low, status.StockStatus);
    }

    [Fact]
    public void Paints_nothing_when_the_medicine_is_healthy()
    {
        var status = TestFactory.Evaluator().Evaluate(Today.AddDays(400), quantity: 250, today: Today);

        Assert.Equal(RowSeverity.Normal, status.RowSeverity);
    }

    [Fact]
    public void Honours_configured_thresholds_instead_of_hard_coding_30_and_10()
    {
        var options = TestFactory.DefaultOptions();
        options.ExpiryWarningDays = 60;
        options.LowStockThreshold = 25;

        var evaluator = TestFactory.Evaluator(options);

        Assert.Equal(ExpiryStatus.ExpiringSoon, evaluator.Evaluate(Today.AddDays(45), 100, Today).ExpiryStatus);
        Assert.Equal(StockStatus.Low, evaluator.Evaluate(Today.AddDays(400), 20, Today).StockStatus);
    }
}
