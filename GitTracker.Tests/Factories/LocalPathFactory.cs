using System.Security.Claims;
using GitTracker.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GitTracker.Tests.Factories
{
    public class LocalPathFactory : ILocalPathFactory
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LocalPathFactory(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetLocalPath()
        {
            string userId = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Name).Value;
            return $"fake-path/{userId}";
        }
    }
}