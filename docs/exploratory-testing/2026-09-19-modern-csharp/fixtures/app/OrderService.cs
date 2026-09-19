namespace Shop.Orders;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public record OrderLine(string Sku, int Quantity, decimal UnitPrice)
{
    public decimal Total => Quantity * UnitPrice;
}

public sealed record Order(Guid Id, IReadOnlyList<OrderLine> Lines);

public interface IOrderRepository
{
    Task<Order?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(Order order, CancellationToken cancellationToken);
}

public sealed class OrderService
{
    private readonly IOrderRepository repository;
    private readonly TimeProvider clock;
    private int processed;

    public OrderService(IOrderRepository repository, TimeProvider clock)
    {
        this.repository = repository;
        this.clock = clock;
    }

    public int Processed => processed;

    public async Task<decimal> TotalAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await repository.FindAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Order {id} not found");
        Interlocked.Increment(ref processed);
        return order.Lines.Sum(line => line.Total);
    }

    public string Describe(Order order) => order.Lines.Count switch
    {
        0 => "empty",
        1 => "single",
        _ => $"{order.Lines.Count} lines at {clock.GetUtcNow():O}",
    };

    public IEnumerable<string> Skus(Order order)
    {
        foreach (var (sku, quantity, _) in order.Lines)
        {
            if (quantity > 0)
            {
                yield return Normalise(sku);
            }
        }

        static string Normalise(string value) => value.Trim().ToUpperInvariant();
    }

    public async Task SaveAllAsync(IEnumerable<Order> orders, CancellationToken cancellationToken)
    {
        await foreach (var order in Stream(orders).WithCancellation(cancellationToken))
        {
            await repository.SaveAsync(order, cancellationToken);
        }
    }

    private static async IAsyncEnumerable<Order> Stream(IEnumerable<Order> orders)
    {
        foreach (var order in orders)
        {
            await Task.Yield();
            yield return order;
        }
    }
}
