using Pharmacy.Api.Domain;

namespace Pharmacy.Api.Storage;

/// <summary>
/// First-run catalogue. Expiry dates are relative to the day the store is first created so
/// that the red / yellow / normal bands are all represented whenever the app is run, rather
/// than every row turning red because the seed was authored two years ago.
/// </summary>
public static class SeedData
{
    public static IEnumerable<Medicine> Medicines()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTimeOffset.UtcNow;

        Medicine Make(string name, string brand, int expiresInDays, int quantity, decimal price, string? notes)
            => new()
            {
                Id = Guid.NewGuid(),
                FullName = name,
                Brand = brand,
                ExpiryDate = today.AddDays(expiresInDays),
                Quantity = quantity,
                Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero),
                Notes = notes,
                Version = 1,
                CreatedUtc = now,
                ModifiedUtc = now
            };

        return
        [
            // Red - expiring inside 30 days.
            Make("Amoxicillin 500mg Capsule", "MediCore", 18, 42, 12.50m,
                "Store below 25 C. Blister pack of 10."),
            Make("Cetirizine 10mg Tablet", "AllerFree", 9, 120, 4.25m,
                "Non-drowsy antihistamine."),

            // Red and low stock - red wins, an expiry problem is a safety problem.
            Make("Insulin Glargine 100IU/ml", "GlucoLife", 21, 4, 2450.00m,
                "Cold chain 2-8 C. Do not freeze."),

            // Already expired - still red.
            Make("Chlorpheniramine 4mg Tablet", "HealWell", -6, 30, 2.10m,
                "Quarantine for disposal. Do not dispense."),

            // Yellow - low stock only.
            Make("Salbutamol Inhaler 100mcg", "BreathEasy", 210, 6, 890.00m,
                "Shake before use. 200 metered doses."),
            Make("Omeprazole 20mg Capsule", "GastroMed", 400, 9, 15.75m,
                "Take before breakfast."),
            Make("Metformin 500mg Tablet", "DiabeCare", 320, 0, 6.40m,
                "Out of stock - reorder raised with supplier."),

            // Normal.
            Make("Paracetamol 500mg Tablet", "HealWell", 470, 250, 3.40m,
                "Blister pack of 10. Max 8 tablets in 24 hours."),
            Make("Ibuprofen 400mg Tablet", "PainAway", 365, 180, 5.60m,
                "Take with food."),
            Make("Amoxicillin 250mg Syrup", "PharmaPlus", 194, 36, 8.75m,
                "Reconstitute with 60ml water. Refrigerate after mixing."),
            Make("Vitamin D3 1000IU Capsule", "NutriPlus", 540, 95, 18.90m,
                null),
            Make("Losartan 50mg Tablet", "CardioMed", 288, 74, 22.30m,
                "Monitor blood pressure."),
            Make("Azithromycin 500mg Tablet", "MediCore", 150, 48, 34.00m,
                "Three day course."),
            Make("ORS Sachet 20.5g", "HydraSalt", 600, 300, 1.95m,
                "Dissolve one sachet in 1 litre of clean water.")
        ];
    }

    public static IEnumerable<Sale> Sales() => [];
}
