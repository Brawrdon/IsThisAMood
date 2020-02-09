using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IsThisAMood.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Serilog;

namespace IsThisAMood.Services
{
    public class ProfileService : IProfileService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ProfileService> _logger;


        public ProfileService(UserManager<IdentityUser> userManager, ILogger<ProfileService> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var sub = context.Subject.GetSubjectId();
            var user = await _userManager.FindByIdAsync(sub);
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, user.UserName),
            };
            
            claims = claims.Where(claim => context.RequestedClaimTypes.Contains(claim.Type)).ToList();
            
            context.IssuedClaims = claims;
        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            var sub = context.Subject.GetSubjectId();
            var user = await _userManager.FindByIdAsync(sub);
            context.IsActive = user != null;
        }
    }
}