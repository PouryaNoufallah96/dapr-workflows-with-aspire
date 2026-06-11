namespace OrderApi.Models;

public sealed record OrderPayload(
    string OrderId,
    string CustomerId,
    string ProductId,
    int Quantity,
    decimal TotalAmount);

public sealed record OrderResult(string OrderId, string Status);

public sealed record InventoryResult(bool InStock);

public sealed record PaymentRequest(string OrderId, decimal Amount);
