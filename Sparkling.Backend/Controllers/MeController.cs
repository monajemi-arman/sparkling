using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Sparkling.Backend.Dtos;
using Sparkling.Backend.Migrations;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Controllers;

[ApiController]
[Route("api/v0/[controller]")]
public class MeController(UserManager<User> userManager) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<Profile>> GetMe()
    {
        var adminOrNot = User.IsInRole("Admin");
        var user = await userManager.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        return new Profile
            {
                Name = user.Name ?? "Unknown",
                Email = user.Email ?? "Unknown",
                IsAdmin = adminOrNot,
                BalanceByHour = user.BalanceByHour
            };
    }
}