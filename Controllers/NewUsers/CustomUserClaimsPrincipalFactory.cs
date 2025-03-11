namespace Agromarket.Controllers.NewUsers;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Threading.Tasks;

public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<IdentityUser, IdentityRole>
{
    private readonly UserManager<IdentityUser> _userManager;

    public CustomUserClaimsPrincipalFactory(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
        _userManager = userManager;
    }

    public override async Task<ClaimsPrincipal> CreateAsync(IdentityUser user)
    {
        var principal = await base.CreateAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            await _userManager.AddToRoleAsync(user, "client");
        }
        return principal;
    }
}
