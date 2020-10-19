using System;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using GitTracker.Interfaces;
using GitTracker.Providers;
using GitTracker.Tests.Factories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GitTracker.Tests
{
    public class LocalPathFactoryTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILocalPathFactory _localPathFactory;

        public LocalPathFactoryTests()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            //Mock IHttpContextAccessor
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new GenericIdentity("testUser")) };

            mockHttpContextAccessor.Setup(_ => _.HttpContext).Returns(context);

            serviceCollection.AddScoped<ILocalPathFactory, LocalPathFactory>();
            serviceCollection.Add(new ServiceDescriptor(typeof(IHttpContextAccessor), mockHttpContextAccessor.Object));

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _localPathFactory = _serviceProvider.GetService<ILocalPathFactory>();
        }

        [Fact]
        public void Test_Returns_Correct_Path_For_User()
        {
            Assert.Equal("fake-path/testUser", _localPathFactory.GetLocalPath());
        }
    }
}