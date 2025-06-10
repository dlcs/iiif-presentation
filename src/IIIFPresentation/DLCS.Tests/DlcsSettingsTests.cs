namespace DLCS.Tests;

public class DlcsSettingsTests
{
    [Fact]
    public void GetOrchestratorUri_Throws_IfNoDefaultAndNoOverrides()
    {
        var settings = GetSettings(settings => settings.OrchestratorUri = null);

        Action act = () => settings.GetOrchestratorUri(10);
        act.Should().ThrowExactly<ArgumentNullException>();
    }
    
    [Fact]
    public void GetOrchestratorUri_ReturnsDefault_IfNoOverride()
    {
        var defaultUri = new Uri("https://dlcs.default");
        var settings = GetSettings(settings => settings.OrchestratorUri = defaultUri);

        settings.GetOrchestratorUri(100).Should().Be(defaultUri, "Default returned, no overrides");
    }
    
    [Fact]
    public void GetOrchestratorUri_ReturnsDefault_IfCustomerOverrideForDifferentCustomer()
    {
        var defaultUri = new Uri("https://dlcs.default");
        var customerUri = new Uri("https://dlcs.customer");
        const int customerId = 100;
        var settings = GetSettings(settings =>
        {
            settings.OrchestratorUri = defaultUri;
            settings.CustomerOrchestratorUri[customerId + 100] = customerUri; 
        });

        settings.GetOrchestratorUri(customerId).Should().Be(defaultUri, "Default returned, no override for customer");
    }
    
    [Fact]
    public void GetOrchestratorUri_ReturnsCustomerSpecific_IfFound()
    {
        var defaultUri = new Uri("https://dlcs.default");
        var customerUri = new Uri("https://dlcs.customer");
        const int customerId = 100;
        var settings = GetSettings(settings =>
        {
            settings.OrchestratorUri = defaultUri;
            settings.CustomerOrchestratorUri[customerId] = customerUri; 
        });
        
        settings.GetOrchestratorUri(customerId).Should().Be(customerUri, "Customer specific returned");
    }

    // Get and setup settings with default values set
    private DlcsSettings GetSettings(Action<DlcsSettings> setup)
    {
        var settings = new DlcsSettings
        {
            ApiUri = new Uri("https://dlcs.api"),
        };
        setup(settings);
        return settings;
    }
}
