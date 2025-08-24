using Microsoft.AspNetCore.Identity;

namespace Sparkling.Backend.Models;

public class User: IdentityUser
{
    [PersonalData]
    public string? Name { get; set; }
    
    public decimal BalanceByHour { get; set; } = 0.0m;
}