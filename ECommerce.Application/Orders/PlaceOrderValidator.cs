using ECommerce.Domain.Entities;
using FluentValidation;

namespace ECommerce.Application.Orders;

public class PlaceOrderValidator : AbstractValidator<PlaceOrderDto>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).IsInEnum();
    }
}
