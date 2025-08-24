namespace Sparkling.Backend.Dtos;

public class Profile
{
    public bool IsAdmin { get; set; } = false;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal BalanceByHour { get; set; } = 0.0m;
}