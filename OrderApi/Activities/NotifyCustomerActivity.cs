using Dapr.Workflow;

namespace OrderApi.Activities;

internal sealed class NotifyCustomerActivity(ILogger<NotifyCustomerActivity> logger)
    : WorkflowActivity<string, object?>
{
    public override Task<object?> RunAsync(
        WorkflowActivityContext context,
        string customerId)
    {
        logger.LogInformation(
            "Sending order confirmation to customer {CustomerId}",
            customerId);

        return Task.FromResult<object?>(null);
    }
}
