namespace Pharmacy.Api.Domain;

/// <summary>How close a medicine is to its expiry date.</summary>
public enum ExpiryStatus
{
    /// <summary>Expiry date is further away than the configured warning window.</summary>
    Ok = 0,

    /// <summary>Expiry date is within the configured warning window (default 30 days).</summary>
    ExpiringSoon = 1,

    /// <summary>Expiry date is in the past.</summary>
    Expired = 2
}

/// <summary>How much of a medicine is left in stock.</summary>
public enum StockStatus
{
    /// <summary>At or above the configured re-order level.</summary>
    Ok = 0,

    /// <summary>Below the configured re-order level (default 10 units).</summary>
    Low = 1,

    /// <summary>Nothing left.</summary>
    OutOfStock = 2
}

/// <summary>
/// The colour band the UI must paint a grid row with. Computed on the server so that
/// the rule exists in exactly one place and cannot drift between clients or be broken
/// by a wrong clock on a workstation.
/// </summary>
public enum RowSeverity
{
    /// <summary>No colour.</summary>
    Normal = 0,

    /// <summary>Yellow background - low stock.</summary>
    Warning = 1,

    /// <summary>Red background - expired or expiring soon. Takes precedence over Warning.</summary>
    Critical = 2
}
