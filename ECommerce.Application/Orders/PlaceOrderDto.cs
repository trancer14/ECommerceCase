using ECommerce.Domain.Entities;

namespace ECommerce.Application.Orders;

public record PlaceOrderDto(string UserId, string ProductId, int Quantity, PaymentMethod PaymentMethod);