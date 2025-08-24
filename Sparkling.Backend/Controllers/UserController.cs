using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Controllers;

[ApiController]
[Route("api/v0/[controller]")]
public class UserController(SparklingDbContext sparklingDbContext, UserManager<User> userManager) : ControllerBase
{
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();
        if (await userManager.IsInRoleAsync(user, "Admin"))
            return Forbid();
        await userManager.DeleteAsync(user);
        return Ok();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registration)
    {
        var email = registration.Email;
        var emailValidator = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();

        if (string.IsNullOrEmpty(email) || !emailValidator.IsValid(email))
        {
            var error = IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email));
            return ValidationProblem(CreateValidationErrors(error));
        }

        var user = new User();
        await userManager.SetUserNameAsync(user, email);
        await userManager.SetEmailAsync(user, email);

        var result = await userManager.CreateAsync(user, registration.Password);

        if (!result.Succeeded)
        {
            return ValidationProblem(CreateValidationErrors(result));
        }

        return Ok();
    }

    private IActionResult ValidationProblem(Dictionary<string, string[]> createValidationErrors)
    {
        throw new NotImplementedException();
    }

    private Dictionary<string, string[]> CreateValidationErrors(IdentityResult result)
    {
        return new Dictionary<string, string[]>
        {
            [""] = result.Errors.Select(e => e.Description).ToArray()
        };
    }

    [HttpGet]
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUser(string? id)
    {
        if (id is not null)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();
            return Ok(new SlimUser()
            {
                Id = user.Id,
                Email = user.Email ?? "",
                IsAdmin = await userManager.IsInRoleAsync(user, "Admin"),
                BalanceByHour = user.BalanceByHour,
            });
        }
        else
        {
            var userList = userManager.Users.Select(user => new SlimUser()
            {
                Id = user.Id,
                Email = user.Email ?? "",
                IsAdmin = userManager.IsInRoleAsync(user, "Admin").GetAwaiter().GetResult(),
                BalanceByHour = user.BalanceByHour,
            }).ToArray();

            return Ok(userList);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest update)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var updateTasks = new List<Task<IdentityResult>>();

        // Handle password change
        if (!string.IsNullOrEmpty(update.NewPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await userManager.ResetPasswordAsync(user, token, update.NewPassword);
            if (!passwordResult.Succeeded)
            {
                return ValidationProblem(CreateValidationErrors(passwordResult));
            }
        }

        // Handle BalanceByHour update
        if (update.BalanceByHour.HasValue)
        {
            user.BalanceByHour = update.BalanceByHour.Value;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return ValidationProblem(CreateValidationErrors(updateResult));
            }
        }

        return Ok();
    }
}