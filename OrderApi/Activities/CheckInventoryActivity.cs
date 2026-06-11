using Dapr.Workflow;
using OrderApi.Models;
using OrderApi.Services;

namespace OrderApi.Activities;

internal sealed class CheckInventoryActivity(IInventoryService inventory)
    : WorkflowActivity<OrderPayload, InventoryResult>
{
    public override async Task<InventoryResult> RunAsync(
        WorkflowActivityContext context,
        OrderPayload order)
    {
        bool inStock = await inventory.HasStockAsync(order.ProductId, order.Quantity);

        return new InventoryResult(inStock);
    }
}
