using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkling.Backend.Models;
using Sparkling.Backend.Requests;

namespace Sparkling.Backend.Controllers;

[ApiController]
[Route("api/v0/[controller]")]
public class WorkController(SparklingDbContext sparklingDbContext, UserManager<User> userManager, IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<WorkSessionDto>>> Get()
    {
        IQueryable<WorkSession> query;
        if (User.IsInRole("Admin"))
        {
            query = sparklingDbContext.WorkSessions;
        }
        else
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new Exception("Wat?");
            query = sparklingDbContext.WorkSessions.Where(ws => ws.UserId == userId);
        }

        var workSessions = await query
            .Include(ws => ws.JupyterContainer)
            .AsNoTracking()
            .Select(ws => new WorkSessionDto
            {
                Id = ws.Id,
                UserId = ws.UserId,
                StartTime = ws.StartTime,
                EndTime = ws.EndTime,
                Status = ws.Status,
                JupyterContainerId = ws.JupyterContainerId,
                JupyterToken = ws.JupyterToken != null ? ws.JupyterToken : null,
                JupyterPort = ws.JupyterPort != null ? ws.JupyterPort : null
            })
            .ToListAsync();

        return workSessions;
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<WorkSessionDto>> GetById(string id)
    {
        var guidId = new Guid(id);

        IQueryable<WorkSession> query;
        if (User.IsInRole("Admin"))
        {
            query = sparklingDbContext.WorkSessions.Where(ws => ws.Id == guidId);
        }
        else
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new Exception("Wat?");
            query = sparklingDbContext.WorkSessions.Where(ws => ws.UserId == userId && ws.Id == guidId);
        }

        var workSession = await query
            .Include(ws => ws.JupyterContainer)
            .AsNoTracking()
            .Select(ws => new WorkSessionDto
            {
                Id = ws.Id,
                UserId = ws.UserId,
                StartTime = ws.StartTime,
                EndTime = ws.EndTime,
                Status = ws.Status,
                JupyterContainerId = ws.JupyterContainerId,
                JupyterToken = ws.JupyterToken,
                JupyterPort = ws.JupyterPort
            })
            .FirstOrDefaultAsync();

        if (workSession == null)
        {
            return NotFound();
        }
        return workSession;
    }

    [HttpPut]
    [Authorize]
    public async Task<ActionResult<Guid>> Create()
    {
        var user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return Unauthorized("User not found.");
        }

        var workSessionExists =
            await sparklingDbContext
                .WorkSessions
                .AnyAsync(ws =>
                    ws.UserId == user.Id &&
                    (ws.Status == WorkSessionStatus.Starting ||
                     ws.Status == WorkSessionStatus.Running));

        if (workSessionExists)
            return Conflict();

        var workSession = new WorkSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            StartTime = DateTime.UtcNow,
            EndTime = null,
            Status = WorkSessionStatus.Starting
        };

        await sparklingDbContext.WorkSessions.AddAsync(workSession);
        await sparklingDbContext.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Publish(new CreateJupyterContainerRequest { WorkSessionId = workSession.Id });
        });

        return workSession.Id;
    }


    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Stop(Guid id)
    {
        var workSession = await sparklingDbContext.WorkSessions.FindAsync(id);

        if (workSession == null)
        {
            return NotFound();
        }

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            throw new UnauthorizedAccessException();

        var isNormalUser = !User.IsInRole("Admin");
        if (workSession.UserId != userId && isNormalUser)
        {
            return Forbid("You are not allowed to delete this work session.");
        }

        if (workSession.Status != WorkSessionStatus.Starting && workSession.Status != WorkSessionStatus.Running)
        {
            return BadRequest("Work session is not in a state that can be deleted.");
        }

        var user = await sparklingDbContext.Users.FindAsync(workSession.UserId);

        if (user == null)
        {
            return NotFound("User not found, should not happen.");
        }

        workSession.Status = WorkSessionStatus.Ended;
        workSession.EndTime = DateTime.UtcNow;

        if (isNormalUser)
            user.BalanceByHour -= (decimal)(workSession.EndTime - workSession.StartTime).Value.TotalHours;

        sparklingDbContext.Entry(user).State = EntityState.Modified;

        sparklingDbContext.Entry(workSession).State = EntityState.Modified;

        await sparklingDbContext.SaveChangesAsync();

        return NoContent();
    }
}