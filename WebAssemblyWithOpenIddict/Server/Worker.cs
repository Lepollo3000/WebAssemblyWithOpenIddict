using WebAssemblyWithOpenIddict.Server.Models;
using WebAssemblyWithOpenIddict.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WebAssemblyWithOpenIddict.Server;

public class Worker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public Worker(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();

        var dbcontext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var usermanager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var rolemanager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Worker>>();

        await InitializeDatabase(dbcontext, usermanager, rolemanager, logger);
        await CreateAppDescriptor(scope);
    }

    private async Task InitializeDatabase(ApplicationDbContext dbcontext, UserManager<ApplicationUser> usermanager, RoleManager<ApplicationRole> rolemanager, ILogger<Worker> logger)
    {
        if (await TryToMigrate(dbcontext, logger))
        {
            await SeedDefaultUsersAndRoles(usermanager, rolemanager, logger);

            // A PARTIR DE AQUI ESTARIA LA INICIALIZACION DE VALORES PREDETERMINADOS PARA LA BD
        }
    }

    private static async Task<bool> TryToMigrate(ApplicationDbContext dbcontext, ILogger<Worker> logger)
    {
        try
        {
            await dbcontext.Database.MigrateAsync();
        }
        catch (Exception)
        {
            logger.Log(LogLevel.Error, "Error al migrar la base de datos.");

            return false;
        }

        return true;
    }

    private async Task SeedDefaultUsersAndRoles(UserManager<ApplicationUser> usermanager, RoleManager<ApplicationRole> rolemanager, ILogger<Worker> logger)
    {
        try
        {
            var adminRole = "Admin";
            var adminUser = new TempUser(name: "administrador", email: "usuario.administrador@gmail.com", password: "Pa55w.rd", roles: new List<string>() { adminRole });

            var roles = new List<string>() { adminRole };
            var users = new List<TempUser>() { { adminUser } };

            await CreateRolesIfDontExist(rolemanager, roles);
            await CreateUsersIfDontExist(usermanager, users);
        }
        catch (Exception)
        {
            logger.Log(LogLevel.Error, "Error al crear roles y usuarios.");
        }
    }

    private static async Task CreateRolesIfDontExist(RoleManager<ApplicationRole> rolemanager, IEnumerable<string> roles)
    {
        foreach (string role in roles)
        {
            ApplicationRole? oRole = await rolemanager.FindByNameAsync(role);

            if (oRole == null)
            {
                oRole = new ApplicationRole()
                {
                    Id = Guid.NewGuid(),
                    Name = role
                };

                await rolemanager.CreateAsync(oRole);
            }
        }
    }

    private static async Task CreateUsersIfDontExist(UserManager<ApplicationUser> usermanager, IEnumerable<TempUser> users)
    {
        foreach (TempUser user in users)
        {
            ApplicationUser? oUser = await usermanager.FindByNameAsync(user.Name);

            if (oUser == null)
            {
                oUser = new ApplicationUser()
                {
                    Id = Guid.NewGuid(),
                    UserName = user.Name,
                    Email = user.Email
                };

                // CREATE USER
                await usermanager.CreateAsync(oUser, user.Password);
                //CONFIRM EMAIL
                var token = await usermanager.GenerateEmailConfirmationTokenAsync(oUser);
                await usermanager.ConfirmEmailAsync(oUser, token);
            }

            if (oUser != null && user.Roles.Any())
            {
                await usermanager.AddToRolesAsync(oUser, user.Roles);
            }
        }
    }

    private static async Task CreateAppDescriptor(AsyncServiceScope scope)
    {
        var openIddictManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await openIddictManager.FindByClientIdAsync("blazor-client") is null)
        {
            await openIddictManager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "blazor-client",
                ConsentType = ConsentTypes.Explicit,
                DisplayName = "Blazor Client Application",
                Type = ClientTypes.Public,
                PostLogoutRedirectUris = { new Uri("https://localhost:44310/authentication/logout-callback") },
                RedirectUris = { new Uri("https://localhost:44310/authentication/login-callback") },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Logout,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles
                },
                Requirements =
                {
                    Requirements.Features.ProofKeyForCodeExchange
                }
            });
        }
    }

    private class TempUser
    {
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public IEnumerable<string> Roles { get; set; } = null!;

        public TempUser(string name, string email, string password, IEnumerable<string> roles)
        {
            Name = name;
            Email = email;
            Password = password;
            Roles = roles;
        }
    }
}