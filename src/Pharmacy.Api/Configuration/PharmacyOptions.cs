namespace Pharmacy.Api.Configuration;

/// <summary>
/// Business rule thresholds. These are configuration, not constants, because the client
/// may well want "30 days" and "10 units" tuned per branch later.
/// </summary>
public sealed class PharmacyOptions
{
    public const string SectionName = "Pharmacy";

    /// <summary>A medicine expiring in fewer than this many days is flagged red. FR-04.</summary>
    public int ExpiryWarningDays { get; set; } = 30;

    /// <summary>A medicine with fewer than this many units is flagged yellow. FR-05.</summary>
    public int LowStockThreshold { get; set; } = 10;

    /// <summary>
    /// IANA time zone the pharmacy trades in. "Today" for the expiry rule is evaluated here,
    /// not in UTC, so a shop in Colombo does not see a row turn red six hours early.
    /// </summary>
    public string TimeZone { get; set; } = "Asia/Colombo";

    /// <summary>Folder that holds medicines.json and sales.json, relative to the content root.</summary>
    public string DataDirectory { get; set; } = "App_Data";

    /// <summary>Origins allowed to call the API. The Angular dev server by default.</summary>
    public string[] AllowedOrigins { get; set; } = ["http://localhost:4200"];
}
