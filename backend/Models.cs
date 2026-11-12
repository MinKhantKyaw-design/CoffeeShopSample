using Microsoft.EntityFrameworkCore;

public class Users
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Customer";
}
public class Coffee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Image { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
public class OrderItem
{
    public int Id { get; set; }
    public int CoffeeId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
public class ShopDb(DbContextOptions<ShopDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Coffee> Coffees => Set<Coffee>();
    public DbSet<Order> Orders => Set<Order>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Coffee>().Property(x => x.Price).HasPrecision(10, 2);
        b.Entity<Order>().Property(x => x.Total).HasPrecision(12, 2);
        b.Entity<OrderItem>().Property(x => x.Price).HasPrecision(10, 2);
    }
}
public record AuthInput(string? Name, string? Email, string? Password);
public record CoffeeInput(string? Name, string? Description, string? Image, decimal Price, int Stock);
public record CartItem(int CoffeeId, int Quantity);
public record OrderInput(List<CartItem>? Items);
public record StatusInput(string? Status);
