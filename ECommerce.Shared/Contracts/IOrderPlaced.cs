namespace ECommerce.Shared.Contracts;

public interface IOrderPlaced
{
    Guid OrderId { get; }
    string UserId { get; }
    string ProductId { get; }
    int Quantity { get; }
    int PaymentMethod { get; } 
    DateTime CreatedAt { get; }
}
