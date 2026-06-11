using System.Net.Http.Json;
using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDapr();

// Pin the password. Aspire generates a random one on every run otherwise,
// and the Dapr component file below has to know it.
var statePassword = builder.AddParameter(
    "statestore-password", "state-store-123", secret: true);

// Valkey (a Redis fork) as the workflow state store
var statestore = builder
    .AddValkey("statestore", 16379, statePassword)
    .WithDataVolume();

var orderApi = builder.AddProject<Projects.OrderApi>("order-api")
    .WithDaprSidecar(new DaprSidecarOptions
    {
        ResourcesPaths = ["./Resources"]
    })
    .WaitFor(statestore);

orderApi
    .WithCommand(
        name: "place-order",
        displayName: "Place test order",
        executeCommand: _ => PlaceOrderAsync(productId: "pro-plan", quantity: 2),
        commandOptions: new CommandOptions
        {
            Description = "Starts an order workflow that completes successfully.",
            IconName = "Play",
            IsHighlighted = true,
            UpdateState = OnUpdateState,
        })
    .WithCommand(
        name: "place-rejected-order",
        displayName: "Place out-of-stock order",
        executeCommand: _ => PlaceOrderAsync(productId: "rare-item", quantity: 500),
        commandOptions: new CommandOptions
        {
            Description = "Starts an order workflow that gets rejected (insufficient stock).",
            IconName = "Warning",
            UpdateState = OnUpdateState,
        });

builder.AddContainer("diagrid-dashboard", "ghcr.io/diagridio/diagrid-dashboard:latest")
    .WithBindMount("./Resources", "/app/components")
    .WithEnvironment("COMPONENT_FILE", "/app/components/dashboard-store.yaml")
    .WithEnvironment("APP_ID", "diagrid-dashboard")
    .WithHttpEndpoint(targetPort: 8080)
    .WaitFor(statestore);

builder.Build().Run();

async Task<ExecuteCommandResult> PlaceOrderAsync(string productId, int quantity)
{
    using var client = new HttpClient();

    var order = new
    {
        orderId = $"order-{Guid.NewGuid():N}"[..20],
        customerId = "cust-42",
        productId,
        quantity,
        totalAmount = 49.99m
    };

    var response = await client.PostAsJsonAsync(
        $"{orderApi.GetEndpoint("http").Url}/orders", order);

    return response.IsSuccessStatusCode
        ? CommandResults.Success()
        : CommandResults.Failure($"The API returned {(int)response.StatusCode}.");
}

ResourceCommandState OnUpdateState(UpdateCommandStateContext context) =>
    context.ResourceSnapshot.State?.Text == KnownResourceStates.Running
        ? ResourceCommandState.Enabled
        : ResourceCommandState.Disabled;
