using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Extensions;

public static class UserManagerExtensions
{
    public static async Task<decimal> GetBalanceAsync(this UserManager<User> userManager, ClaimsPrincipal claimsPrincipal)
    {
        if (claimsPrincipal == null) throw new ArgumentNullException(nameof(claimsPrincipal));

        var user = await userManager.GetUserAsync(claimsPrincipal);
        
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        
        return isAdmin ? decimal.MaxValue : user.BalanceByHour;
    }
}