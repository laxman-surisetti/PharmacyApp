using System.Text.Json;

namespace Pharmacy.Api.Storage;

/// <summary>
/// JSON-file backed collection store.
///
/// The brief mandates JSON on the server rather than a database, which removes the
/// guarantees a database would normally give. Three things replace them:
///
///  1. <b>Single writer.</b> A <see cref="SemaphoreSlim"/> serialises every read-modify-write,
///     so two concurrent sales cannot both read quantity 1 and both decrement it.
///  2. <b>Atomic replace.</b> The document is written to a temporary file and then moved over
///     the real one, so a crash halfway through a write leaves the previous good file intact
///     rather than a truncated document.
///  3. <b>Write-through cache.</b> The collection is held in memory and only re-read from disk
///     when it has never been loaded or a mutation failed, so reads do not pay for disk I/O.
/// </summary>
public sealed class JsonFileStore<T> : IJsonStore<T>, IDisposable where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<IEnumerable<T>> _seedFactory;
    private readonly ILogger<JsonFileStore<T>> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private List<T>? _cache;

    public JsonFileStore(string filePath, Func<IEnumerable<T>> seedFactory, ILogger<JsonFileStore<T>> logger)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _seedFactory = seedFactory ?? throw new ArgumentNullException(nameof(seedFactory));
        _logger = logger;

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

            // Hand back a copy of the list so the caller can enumerate it after the lock is
            // released without tripping over a concurrent mutation.
            return items.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TResult> MutateAsync<TResult>(
        Func<List<T>, TResult> mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var items = await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

            TResult result;
            try
            {
                result = mutation(items);
            }
            catch
            {
                // The mutation may have half-applied itself to the in-memory list. Drop the
                // cache so the next access re-reads the last known good file from disk.
                _cache = null;
                throw;
            }

            await SaveAsync(items, cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<T>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("{File} not found - seeding a new store.", _filePath);
            _cache = _seedFactory().ToList();
            await SaveAsync(_cache, cancellationToken).ConfigureAwait(false);
            return _cache;
        }

        await using var stream = File.OpenRead(_filePath);
        if (stream.Length == 0)
        {
            _cache = [];
            return _cache;
        }

        try
        {
            _cache = await JsonSerializer
                .DeserializeAsync<List<T>>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"'{_filePath}' is not valid JSON for a list of {typeof(T).Name}. " +
                "Fix or delete the file and restart - deleting it re-seeds the store.", ex);
        }

        return _cache;
    }

    private async Task SaveAsync(List<T> items, CancellationToken cancellationToken)
    {
        var temporaryPath = _filePath + ".tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, items, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        // Atomic on both Windows and POSIX: readers see either the old file or the new one.
        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    public void Dispose() => _gate.Dispose();
}
