namespace BadmintonShop.Core.Entities;

public static class OrderRules
{
    public static readonly string[] Statuses = ["Pending", "Confirmed", "Shipping", "Completed", "Cancelled"];
    public static bool CanTransition(string from, string to) => (from, to) is
        ("Pending", "Confirmed") or ("Confirmed", "Shipping") or ("Shipping", "Completed")
        or ("Pending", "Cancelled") or ("Confirmed", "Cancelled");
    public static decimal Total(IEnumerable<OrderItem> items) => items.Sum(x => x.Quantity * x.UnitPrice);
}

