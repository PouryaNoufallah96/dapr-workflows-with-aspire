using Dapr.Workflow;
using OrderApi.Activities;
using OrderApi.Models;
using OrderApi.Services;
using OrderApi.Workflows;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSingleton<IInventoryService, InMemoryInventoryService>();

builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<OrderProcessingWorkflow>();

    options.RegisterActivity<CheckInventoryActivity>();
    options.RegisterActivity<ProcessPaymentActivity>();
    options.RegisterActivity<UpdateInventoryActivity>();
    options.RegisterActivity<NotifyCustomerActivity>();
});

var app = builder.Build();

app.MapPost("/orders", async (
    OrderPayload order,
    DaprWorkflowClient workflowClient) =>
{
    string instanceId = await workflowClient.ScheduleNewWorkflowAsync(
        name: nameof(OrderProcessingWorkflow),
        instanceId: order.OrderId,
        input: order);

    return Results.Accepted($"/orders/{instanceId}", new { instanceId });
});

app.MapGet("/orders/{instanceId}", async (
    string instanceId,
    DaprWorkflowClient workflowClient) =>
{
    WorkflowState? state = await workflowClient.GetWorkflowStateAsync(instanceId);

    if (state is null || !state.Exists)
    {
        return Results.NotFound();
    }

    return Results.Ok(new
    {
        RuntimeStatus = state.RuntimeStatus.ToString(),
        Output = state.ReadOutputAs<OrderResult>()
    });
});

app.MapDefaultEndpoints();

app.Run();
