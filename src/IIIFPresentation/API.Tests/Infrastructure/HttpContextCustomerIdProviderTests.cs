using API.Infrastructure;
using Core.Exceptions;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace API.Tests.Infrastructure;

public class HttpContextCustomerIdProviderTests
{
    private static readonly IHttpContextAccessor HttpContextAccessor = A.Fake<IHttpContextAccessor>();
    private static readonly ILogger<HttpContextCustomerIdProvider> logger = new NullLogger<HttpContextCustomerIdProvider>();
    private readonly HttpContextCustomerIdProvider sut = new(HttpContextAccessor, logger);
    
    [Fact]
    public void SetCustomerId_ThrowsException()
    {
        // Arrange and act
        Action action = () => sut.SetCustomerId(1);
        
        // Assert
        action.Should().Throw<PresentationException>();
    }
    
    [Fact]
    public void GetCustomerId_ThrowsException_WhenHttpContextNull()
    {
        // Arrange and act
        Action action = () => sut.GetCustomerId();
        
        // Assert
        action.Should().Throw<PresentationException>();
    }
    
    [Fact]
    public void GetCustomerId_ThrowsException_WhenCustomerIdNotInUrl()
    {
        // Arrange
        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var customerIdProvider = new HttpContextCustomerIdProvider(contextAccessor, logger);
        
        // act
        Action action = () => customerIdProvider.GetCustomerId();
        
        // Assert
        action.Should().Throw<PresentationException>();
    }
    
    [Fact]
    public void GetCustomerId_GetsCustomerId_WhenCustomerIdNotInUrl()
    {
        // Arrange
        var customerIdToTest = 1;
        
        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    RouteValues = new RouteValueDictionary
                    {
                        { "customerId", customerIdToTest }
                    }
                }
            }
        };

        var customerIdProvider = new HttpContextCustomerIdProvider(contextAccessor, logger);
        
        // act
        var customerId = customerIdProvider.GetCustomerId();
        
        // Assert
        customerId.Should().Be(customerIdToTest);
    }
}
