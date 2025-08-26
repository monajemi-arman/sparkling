using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Jobs;

public class SessionKillerJob(UserManager<User> userManager, SparklingDbContext sparklingDbContext): IJob
{
    internal static JobKey Key = new("SessionKillerJob");
    
    public async Task Execute(IJobExecutionContext context)
    {
        var sessions = await sparklingDbContext.WorkSessions
            .Where(s => s.EndTime == null && s.Status == WorkSessionStatus.Running)
            .ToListAsync();
        
        foreach (var session in sessions)
        {
            var user = await userManager.FindByIdAsync(session.UserId);
            
            if (user == null)
            {
                // User not found, end the session
                session.Status = WorkSessionStatus.Ended;
                session.EndTime = DateTime.UtcNow;
            }
            else
            {
                if (await userManager.IsInRoleAsync(user, "Admin"))
                {
                    // Admins can keep their sessions running indefinitely
                    continue;
                }
                
                if ((decimal)(DateTime.UtcNow - session.StartTime).TotalHours > user.BalanceByHour)
                {
                    // User has exceeded their balance, end the session
                    session.Status = WorkSessionStatus.Stopped;
                    session.EndTime = DateTime.UtcNow;
                    user.BalanceByHour -= user.BalanceByHour;
                    sparklingDbContext.Entry(user).State = EntityState.Modified;
                }
                else
                {
                    continue;
                }
            }
            
            sparklingDbContext.Entry(session).State = EntityState.Modified;

            // Save per session to avoid long transactions
            await sparklingDbContext.SaveChangesAsync();
        }
    }
}