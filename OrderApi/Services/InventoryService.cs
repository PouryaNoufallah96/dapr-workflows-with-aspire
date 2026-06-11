using System.Collections.Concurrent;

namespace OrderApi.Services;

public interface IInventoryService
{
    Task<bool> HasStockAsync(string productId, int quantity);

    Task ReserveStockAsync(string productId, int quantity);
}

internal sealed class InMemoryInventoryService : IInventoryService
{
    private const int DefaultStock = 100;

    private readonly ConcurrentDictionary<string, int> _stock = new();

    public Task<bool> HasStockAsync(string productId, int quantity)
    {
        int available = _stock.GetOrAdd(productId, _ => DefaultStock);

        return Task.FromResult(available >= quantity);
    }

    public Task ReserveStockAsync(string productId, int quantity)
    {
        _stock.AddOrUpdate(
            productId,
            _ => DefaultStock - quantity,
            (_, available) => available - quantity);

        return Task.CompletedTask;
    }
}
