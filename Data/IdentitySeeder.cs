using Microsoft.AspNetCore.Identity;

namespace ClaimSystem.Data
{
    public static class IdentitySeeder
    {
        public const string LecturerRole = "Lecturer";
        public const string CoordinatorRole = "ProgrammeCoordinator";
        public const string ManagerRole = "AcademicManager";

        public static async Task SeedAsync(IServiceProvider sp)
        {
            var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var userMgr = sp.GetRequiredService<UserManager<IdentityUser>>();

            foreach (var r in new[] { LecturerRole, CoordinatorRole, ManagerRole })
                if (!await roleMgr.RoleExistsAsync(r))
                    await roleMgr.CreateAsync(new IdentityRole(r));

            async Task<IdentityUser> EnsureUser(string email, string role)
            {
                var u = await userMgr.FindByEmailAsync(email);
                if (u == null)
                {
                    u = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                    await userMgr.CreateAsync(u, "Passw0rd!");
                }
                if (!await userMgr.IsInRoleAsync(u, role))
                    await userMgr.AddToRoleAsync(u, role);
                return u;
            }

            await EnsureUser("lecturer@demo.test", LecturerRole);
            await EnsureUser("coordinator@demo.test", CoordinatorRole);
            await EnsureUser("manager@demo.test", ManagerRole);
        }
    }
}
