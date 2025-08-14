namespace ECommerce.Domain.Entities
{
    public enum PaymentMethod { CreditCard = 0, BankTransfer = 1 }

    public class Order
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string UserId { get; private set; } = default!;
        public string ProductId { get; private set; } = default!;
        public int Quantity { get; private set; }
        public PaymentMethod PaymentMethod { get; private set; }
        public string Status { get; private set; } = "Pending";
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; private set; }

        private Order() { } 

        public Order(string userId, string productId, int quantity, PaymentMethod method)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId");
            if (string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("productId");
            if (quantity <= 0) throw new ArgumentException("quantity");

            UserId = userId.Trim();
            ProductId = productId.Trim();
            Quantity = quantity;
            PaymentMethod = method;
        }

        public void MarkProcessed()
        {
            Status = "Processed";
            ProcessedAt = DateTime.UtcNow;
        }
    }
}
