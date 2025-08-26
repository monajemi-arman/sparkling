using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Quartz;
using Quartz.AspNetCore;
using Sparkling.Backend.Jobs;
using Sparkling.Backend.Models;
using Sparkling.Backend.Services;

namespace Sparkling.Backend;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthorization();
        
        builder
            .Services
            .AddControllers();
        
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Sparkling", Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."

            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    []
                }
            });
        });

        
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();
        
        builder.Services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        });

        builder
            .Services
            .AddIdentityApiEndpoints<User>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SparklingDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddDbContext<SparklingDbContext>(
            options => options.UseSqlite("Data Source=mydb.db;"));
        
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("MyCors",
                policy =>
                {
                    policy
                        .WithOrigins("http://localhost")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
        });

        builder.Services.AddSingleton<ILogService, LogService>();
        
        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 6;
            options.Password.RequiredUniqueChars = 1;
        });
 
        builder.Services.AddQuartz(q =>
        {
            q.AddJob<SessionKillerJob>(
                j => 
                    j.WithIdentity(SessionKillerJob.Key)
                        .DisallowConcurrentExecution()
                );

            q.AddTrigger(opts =>
            {
                opts
                    .ForJob(SessionKillerJob.Key)
                    .WithIdentity("SessionKillerTrigger")
                    .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever().Build())
                    .WithDescription("Runs every 5 minutes to kill sessions with zero balance");
            });
        });
        builder.Services.AddQuartzServer(options =>
        {
            // when shutting down we want jobs to complete gracefully
            options.WaitForJobsToComplete = true;
        });
        
        
        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var dbContext = services.GetRequiredService<SparklingDbContext>();
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext.Database.MigrateAsync();
            });

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            await roleManager.CreateAsync(new IdentityRole("Admin"));

            var userManager = services.GetRequiredService<UserManager<User>>();
            var user = new User {
                Name = "Sparkling Admin",
            };
            await userManager.SetUserNameAsync(user, "info@sparklean.io");
            await userManager.SetEmailAsync(user, "info@sparklean.io");
            _ = await userManager.CreateAsync(user, "123456Aa!@#");
            await userManager.AddToRolesAsync(user, ["Admin"]);

        }

        Extensions.IdentityApiEndpointRouteBuilderExtensions.MapIdentityApi<User>(app);
        
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        
        app.UseCors("MyCors");
        
        app.UseHttpsRedirection();
        
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.MapControllers();

        app.Run();
    }
}