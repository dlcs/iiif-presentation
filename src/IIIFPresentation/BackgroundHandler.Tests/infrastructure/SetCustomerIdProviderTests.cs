using BackgroundHandler.Infrastructure;
using Core.Exceptions;
using FluentAssertions;

namespace BackgroundHandler.Tests.infrastructure;

public class SetCustomerIdProviderTests
{
    [Fact]
    public void SetCustomerId_SetsTheCustomerIdWithoutError()
    {
        // Arrange
        var sut = new SetCustomerIdProvider();
        
        //  act
        Action action = () => sut.SetCustomerId(1);
        
        // Assert
        action.Should().NotThrow();
    }
    
    [Fact]
    public void GetCustomerId_ThrowsError_WhenNoCustomerIdSet()
    {
        // Arrange
        var sut = new SetCustomerIdProvider();
        
        //  Act
        Action action = () => sut.GetCustomerId();
        
        // Assert
        action.Should().Throw<PresentationException>();
    }
    
    [Fact]
    public void GetCustomerId_RetrievesCustomerId_WhenCustomerIdSet()
    {
        // Arrange
        var sut = new SetCustomerIdProvider();
        var customerIdToSet = 1;
        sut.SetCustomerId(customerIdToSet);
        
        //  Act
        var customerId = sut.GetCustomerId();
        
        // Assert
        customerId.Should().Be(customerIdToSet);
    }
}
