using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;

namespace OrderProcessing.Tests;

public sealed record OrderRequest(
    string OrderId,
    string CustomerId,
    string ProductId,
    int Quantity,
    decimal TotalAmount);

public sealed class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // The statestore.yaml component points at the fixed Valkey port (16379),
        // so the testing builder must not randomize ports.
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.OrderApi_AppHost>(["DcpPublisher:RandomizePorts=false"]);

        App = await appHost.BuildAsync();
        await App.StartAsync();

        await App.ResourceNotifications
            .WaitForResourceAsync("order-api", KnownResourceStates.Running)
            .WaitAsync(StartupTimeout);
    }

    public async Task DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }
}

public class OrderWorkflowTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    private static readonly TimeSpan WorkflowTimeout = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task OrderWorkflow_Completes_WhenProductIsInStock()
    {
        using var client = fixture.App.CreateHttpClient("order-api");
        var order = new OrderRequest(
            OrderId: $"order-{Guid.NewGuid():N}",
            CustomerId: "cust-42",
            ProductId: "pro-plan",
            Quantity: 2,
            TotalAmount: 49.99m);

        string instanceId = await StartOrderAsync(client, order);
        JsonElement result = await WaitForOutputAsync(client, instanceId);

        Assert.Equal(order.OrderId, instanceId);
        Assert.Equal("Completed", result.GetProperty("output").GetProperty("status").GetString());
        Assert.Equal(order.OrderId, result.GetProperty("output").GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task OrderWorkflow_RejectsOrder_WhenProductIsOutOfStock()
    {
        using var client = fixture.App.CreateHttpClient("order-api");
        var order = new OrderRequest(
            OrderId: $"order-{Guid.NewGuid():N}",
            CustomerId: "cust-42",
            ProductId: "rare-item",
            Quantity: 500, // in-memory inventory holds 100 per product
            TotalAmount: 9_999.99m);

        string instanceId = await StartOrderAsync(client, order);
        JsonElement result = await WaitForOutputAsync(client, instanceId);

        Assert.Equal("Rejected: out of stock", result.GetProperty("output").GetProperty("status").GetString());
    }

    [Fact]
    public async Task StatusEndpoint_ReturnsNotFound_ForUnknownInstance()
    {
        using var client = fixture.App.CreateHttpClient("order-api");

        using var response = await client.GetAsync($"/orders/does-not-exist-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DiagridDashboard_IsReachable()
    {
        await fixture.App.ResourceNotifications
            .WaitForResourceAsync("diagrid-dashboard", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(5));

        using var client = fixture.App.CreateHttpClient("diagrid-dashboard");

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The sidecar's workflow engine needs a few seconds after the API reports
    // Running (placement/actor setup), so the first POST is retried.
    private static async Task<string> StartOrderAsync(HttpClient client, OrderRequest order)
    {
        var deadline = DateTime.UtcNow + WorkflowTimeout;

        while (true)
        {
            try
            {
                using var response = await client.PostAsJsonAsync("/orders", order);

                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                    return body.GetProperty("instanceId").GetString()!;
                }
            }
            catch (HttpRequestException)
            {
                // API or sidecar not ready yet.
            }
            catch (TaskCanceledException)
            {
                // Request timed out while the sidecar was starting.
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Could not schedule the workflow in time.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    private static async Task<JsonElement> WaitForOutputAsync(HttpClient client, string instanceId)
    {
        var deadline = DateTime.UtcNow + WorkflowTimeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync($"/orders/{instanceId}");

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var body = await response.Content.ReadFromJsonAsync<JsonElement>();

                    if (body.TryGetProperty("output", out var output) &&
                        output.ValueKind != JsonValueKind.Null)
                    {
                        return body;
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Transient connection issue; retry below.
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Workflow '{instanceId}' did not produce output in time.");
    }
}
