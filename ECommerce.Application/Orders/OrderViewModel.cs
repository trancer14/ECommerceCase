using ECommerce.Domain.Entities;

namespace ECommerce.Application.Orders;

public record OrderViewModel(Guid Id, string ProductId, int Quantity, PaymentMethod PaymentMethod,string Status, DateTime CreatedAt, DateTime? ProcessedAt);
