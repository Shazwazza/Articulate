#nullable enable
using System.Security.Claims;
using Articulate.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Articulate.Tests.Services
{
    [TestFixture]
    public class BackOfficeAuthServiceTests
    {
        [Test]
        public async Task IsBackOfficeLoggedInAsync_requires_the_requested_authentication_scheme()
        {
            const string requestedScheme = "BackOffice";
            ClaimsPrincipal principal =
                new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "editor")], requestedScheme));
            AuthenticationTicket ticket = new(principal, "DifferentScheme");
            Mock<IAuthenticationService> authentication = new();
            authentication
                .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), requestedScheme))
                .ReturnsAsync(AuthenticateResult.Success(ticket));

            DefaultHttpContext context = new()
            {
                RequestServices = new ServiceCollection()
                    .AddSingleton(authentication.Object)
                    .BuildServiceProvider()
            };

            var result = await CreateSut().IsBackOfficeLoggedInAsync(context, requestedScheme);

            Assert.That(result, Is.False);
        }

        [Test]
        public void HasPermissions_requires_all_requested_permissions()
        {
            Mock<IUser> user = new();
            user.SetupGet(x => x.Id).Returns(1);
            IReadOnlyUserGroup group = Mock.Of<IReadOnlyUserGroup>();
            user.SetupGet(x => x.Groups).Returns([group]);
            Mock<IContent> content = new();
            content.SetupGet(x => x.Path).Returns("-1,100");
            Mock<IUserService> userService = new();
            userService
                .Setup(x => x.GetPermissionsForPath(user.Object, "-1,100"))
                .Returns(new EntityPermissionSet(
                    100,
                    new EntityPermissionCollection { new EntityPermission(1, 100, new HashSet<string> { "A" }) }));

            var result = CreateSut(userService.Object).HasPermissions(user.Object, content.Object, ["A", "B"]);

            Assert.That(result, Is.False);
        }

        [Test]
        public void HasPermissions_fails_closed_when_no_permissions_are_requested()
        {
            var result = CreateSut().HasPermissions(Mock.Of<IUser>(), Mock.Of<IContent>(), []);

            Assert.That(result, Is.False);
        }

        private static BackOfficeAuthService CreateSut(IUserService? userService = null) =>
            new(
                Mock.Of<IBackOfficeSecurityAccessor>(),
                userService ?? Mock.Of<IUserService>(),
                NullLogger<BackOfficeAuthService>.Instance);
    }
}
