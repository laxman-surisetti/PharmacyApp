using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pharmacy.Api.Configuration;
using Pharmacy.Api.Domain;
using Pharmacy.Api.Services;
using Pharmacy.Api.Storage;

namespace Pharmacy.Api.Tests;

/// <summary>A clock the test drives, so expiry boundaries can be probed exactly.</summary>
public sealed class TestClock : IPharmacyClock
{
    public TestClock(DateOnly today)
    {
        Today = today;
    }

    public DateOnly Today { get; set; }

    public DateTimeOffset UtcNow => new(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
}

/// <summary>
/// A throwaway folder for the JSON stores, removed when the test finishes. Tests use the
/// real <see cref="JsonFileStore{T}"/> rather than a fake, because the file behaviour -
/// seeding, atomic replace, locking - is a large part of what is worth testing here.
/// </summary>
public sealed class TempDataDirectory : IDisposable
{
    public TempDataDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pharmacy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A temp folder that outlives the test run is not worth failing a build over.
        }
    }
}

public static class TestFactory
{
    public static PharmacyOptions DefaultOptions() => new()
    {
        ExpiryWarningDays = 30,
        LowStockThreshold = 10,
        TimeZone = "UTC"
    };

    public static MedicineStatusEvaluator Evaluator(PharmacyOptions? options = null)
        => new(Options.Create(options ?? DefaultOptions()));

    public static JsonFileStore<T> Store<T>(string filePath, Func<IEnumerable<T>> seed) where T : class
        => new(filePath, seed, NullLogger<JsonFileStore<T>>.Instance);

    public static Medicine Medicine(
        string fullName = "Paracetamol 500mg Tablet",
        string brand = "HealWell",
        DateOnly? expiryDate = null,
        int quantity = 100,
        decimal price = 3.40m)
        => new()
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Brand = brand,
            ExpiryDate = expiryDate ?? new DateOnly(2027, 1, 1),
            Quantity = quantity,
            Price = price,
            Notes = "Seeded by a test."
        };
}
