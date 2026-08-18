using Pharmacy.Api.Domain;

namespace Pharmacy.Api.Tests;

public sealed class JsonFileStoreTests
{
    [Fact]
    public async Task Seeds_the_file_when_it_does_not_exist_yet()
    {
        using var temp = new TempDataDirectory();
        var path = temp.File("medicines.json");

        var store = TestFactory.Store<Medicine>(path, () => [TestFactory.Medicine()]);
        var items = await store.GetAllAsync();

        Assert.Single(items);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Round_trips_through_the_file_rather_than_only_through_memory()
    {
        using var temp = new TempDataDirectory();
        var path = temp.File("medicines.json");
        var id = Guid.NewGuid();

        var writer = TestFactory.Store<Medicine>(path, Array.Empty<Medicine>);
        await writer.MutateAsync(items =>
        {
            var medicine = TestFactory.Medicine(fullName: "Ibuprofen 400mg Tablet");
            medicine.Id = id;
            items.Add(medicine);
            return true;
        });

        // A second store over the same path only sees the medicine if it really was written.
        var reader = TestFactory.Store<Medicine>(path, Array.Empty<Medicine>);
        var items = await reader.GetAllAsync();

        Assert.Single(items);
        Assert.Equal(id, items[0].Id);
        Assert.Equal("Ibuprofen 400mg Tablet", items[0].FullName);
    }

    [Fact]
    public async Task A_failed_mutation_persists_nothing()
    {
        using var temp = new TempDataDirectory();
        var path = temp.File("medicines.json");

        var store = TestFactory.Store<Medicine>(path, () => [TestFactory.Medicine(fullName: "Original")]);
        await store.GetAllAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MutateAsync<bool>(items =>
        {
            items.Add(TestFactory.Medicine(fullName: "Half written"));
            throw new InvalidOperationException("boom");
        }));

        var items = await store.GetAllAsync();

        Assert.Single(items);
        Assert.Equal("Original", items[0].FullName);
    }

    [Fact]
    public async Task Serialises_concurrent_mutations_so_no_update_is_lost()
    {
        using var temp = new TempDataDirectory();
        var store = TestFactory.Store<Medicine>(temp.File("medicines.json"), () => [TestFactory.Medicine(quantity: 0)]);

        // 200 racing increments. Without the write lock this is the classic lost-update
        // bug: read 5, read 5, write 6, write 6.
        await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => store.MutateAsync(items =>
        {
            items[0].Quantity++;
            return true;
        })));

        var items = await store.GetAllAsync();

        Assert.Equal(200, items[0].Quantity);
    }

    [Fact]
    public async Task Reports_a_corrupt_file_clearly_instead_of_crashing_obscurely()
    {
        using var temp = new TempDataDirectory();
        var path = temp.File("medicines.json");
        await File.WriteAllTextAsync(path, "{ this is not json ]");

        var store = TestFactory.Store<Medicine>(path, Array.Empty<Medicine>);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.GetAllAsync());
        Assert.Contains("not valid JSON", error.Message);
    }

    [Fact]
    public async Task Treats_an_empty_file_as_an_empty_collection()
    {
        using var temp = new TempDataDirectory();
        var path = temp.File("sales.json");
        await File.WriteAllTextAsync(path, string.Empty);

        var store = TestFactory.Store<Sale>(path, Array.Empty<Sale>);

        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task Callers_cannot_mutate_the_store_through_the_list_they_are_handed()
    {
        using var temp = new TempDataDirectory();
        var store = TestFactory.Store<Medicine>(temp.File("medicines.json"), () => [TestFactory.Medicine()]);

        var snapshot = await store.GetAllAsync();
        Assert.Single(snapshot);

        // GetAllAsync hands back a copy, so adding to it must not change what the store holds.
        var copy = snapshot.ToList();
        copy.Add(TestFactory.Medicine(fullName: "Sneaked in"));

        Assert.Single(await store.GetAllAsync());
    }
}
