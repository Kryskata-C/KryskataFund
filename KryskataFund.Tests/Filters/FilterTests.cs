using FluentAssertions;
using KryskataFund.Filters;
using KryskataFund.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace KryskataFund.Tests.Filters
{
    public class FilterTests
    {
        private static ActionExecutingContext CreateContext(ISession session)
        {
            var httpContext = new DefaultHttpContext { Session = session };
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                new object());
        }

        // --- RequireSignInAttribute ---

        [Fact]
        public void RequireSignIn_RedirectsUnauthenticatedUsers()
        {
            var filter = new RequireSignInAttribute();
            var context = CreateContext(new MockSession());

            filter.OnActionExecuting(context);

            context.Result.Should().BeOfType<RedirectToActionResult>();
            var redirect = (RedirectToActionResult)context.Result;
            redirect.ActionName.Should().Be("SignIn");
            redirect.ControllerName.Should().Be("Account");
        }

        [Fact]
        public void RequireSignIn_AllowsAuthenticatedUsers()
        {
            var filter = new RequireSignInAttribute();
            var session = new MockSession();
            session.SetString("IsSignedIn", "true");
            var context = CreateContext(session);

            filter.OnActionExecuting(context);

            context.Result.Should().BeNull("authenticated users should not be redirected");
        }

        [Fact]
        public void RequireSignIn_RedirectsWhenSignedInIsFalse()
        {
            var filter = new RequireSignInAttribute();
            var session = new MockSession();
            session.SetString("IsSignedIn", "false");
            var context = CreateContext(session);

            filter.OnActionExecuting(context);

            context.Result.Should().BeOfType<RedirectToActionResult>();
            var redirect = (RedirectToActionResult)context.Result;
            redirect.ActionName.Should().Be("SignIn");
        }
    }
}
