using Dapr.Workflow;
using OrderApi.Activities;
using OrderApi.Models;

namespace OrderApi.Workflows;

internal sealed class OrderProcessingWorkflow : Workflow<OrderPayload, OrderResult>
{
    public override async Task<OrderResult> RunAsync(
        WorkflowContext context,
        OrderPayload order)
    {
        // 1. Check inventory
        var inventory = await context.CallActivityAsync<InventoryResult>(
            nameof(CheckInventoryActivity),
            order);

        if (!inventory.InStock)
        {
            return new OrderResult(order.OrderId, "Rejected: out of stock");
        }

        // 2. Charge the customer
        await context.CallActivityAsync(
            nameof(ProcessPaymentActivity),
            new PaymentRequest(order.OrderId, order.TotalAmount));

        // 3. Reserve the stock
        await context.CallActivityAsync(
            nameof(UpdateInventoryActivity),
            order);

        // 4. Notify the customer
        await context.CallActivityAsync(
            nameof(NotifyCustomerActivity),
            order.CustomerId);

        return new OrderResult(order.OrderId, "Completed");
    }
}
