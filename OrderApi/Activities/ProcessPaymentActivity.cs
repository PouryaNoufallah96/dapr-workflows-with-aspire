using Dapr.Workflow;
using OrderApi.Models;

namespace OrderApi.Activities;

internal sealed class ProcessPaymentActivity(ILogger<ProcessPaymentActivity> logger)
    : WorkflowActivity<PaymentRequest, object?>
{
    public override Task<object?> RunAsync(
        WorkflowActivityContext context,
        PaymentRequest payment)
    {
        logger.LogInformation(
            "Charging {Amount:C} for order {OrderId}",
            payment.Amount,
            payment.OrderId);

        return Task.FromResult<object?>(null);
    }
}
