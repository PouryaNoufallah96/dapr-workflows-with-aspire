using Dapr.Workflow;
using OrderApi.Models;
using OrderApi.Services;

namespace OrderApi.Activities;

internal sealed class UpdateInventoryActivity(
    IInventoryService inventory,
    ILogger<UpdateInventoryActivity> logger)
    : WorkflowActivity<OrderPayload, object?>
{
    public override async Task<object?> RunAsync(
        WorkflowActivityContext context,
        OrderPayload order)
    {
        await inventory.ReserveStockAsync(order.ProductId, order.Quantity);

        logger.LogInformation(
            "Reserved {Quantity} x {ProductId} for order {OrderId}",
            order.Quantity,
            order.ProductId,
            order.OrderId);

        return null;
    }
}
