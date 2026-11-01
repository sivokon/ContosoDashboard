using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Tests.TestSupport;

// Seeded users 1-4 come from ApplicationDbContext; 5-7 are added by TestDb
public static class TestUsers
{
    public const int Admin = 1;                 // IT
    public const int ProjectManager = 2;        // Engineering, manages project 1
    public const int TeamLead = 3;              // Engineering, member of project 1
    public const int Employee = 4;              // Engineering, member of project 1
    public const int EngineeringOutsider = 5;   // Engineering employee, NOT a project member
    public const int SalesEmployee = 6;         // Sales employee
    public const int SalesTeamLead = 7;         // Sales team lead

    public const int SampleProject = 1;
    public const int SampleTask = 1;            // task in project 1
}

public class TestDbContext : ApplicationDbContext
{
    public TestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public bool FailOnSave { get; set; }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (FailOnSave)
            throw new DbUpdateException("Simulated save failure.");

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}

public static class TestDb
{
    public static TestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TestDbContext(options);
        context.Database.EnsureCreated();

        context.Users.AddRange(
            NewUser(TestUsers.EngineeringOutsider, "Pat Outsider", "Engineering", UserRole.Employee),
            NewUser(TestUsers.SalesEmployee, "Sam Sales", "Sales", UserRole.Employee),
            NewUser(TestUsers.SalesTeamLead, "Tess Lead", "Sales", UserRole.TeamLead));
        context.SaveChanges();

        return context;
    }

    private static User NewUser(int id, string name, string department, UserRole role) => new()
    {
        UserId = id,
        Email = $"user{id}@contoso.com",
        DisplayName = name,
        Department = department,
        Role = role,
        CreatedDate = DateTime.UtcNow
    };
}
