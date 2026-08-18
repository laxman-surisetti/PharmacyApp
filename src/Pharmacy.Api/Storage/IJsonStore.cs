namespace Pharmacy.Api.Storage;

/// <summary>
/// A single-file JSON collection store. Every store is single-writer: <see cref="MutateAsync"/>
/// serialises callers, so a read-modify-write is atomic with respect to other callers in
/// this process, and the file itself is replaced atomically so a crash mid-write cannot
/// leave a truncated document behind.
/// </summary>
public interface IJsonStore<T> where T : class
{
    /// <summary>Returns a snapshot of the collection. Safe to enumerate without holding a lock.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="mutation"/> under the store's write lock against the live list and
    /// persists the result. If the mutation throws, nothing is written and the in-memory state
    /// is reloaded from disk on the next access.
    /// </summary>
    Task<TResult> MutateAsync<TResult>(Func<List<T>, TResult> mutation, CancellationToken cancellationToken = default);
}
