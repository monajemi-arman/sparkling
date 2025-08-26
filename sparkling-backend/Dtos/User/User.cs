public class SlimUser
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public bool IsAdmin { get; set; }
    public decimal BalanceByHour { get; set; }
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string? NewPassword { get; set; }
    public decimal? BalanceByHour { get; set; }
}