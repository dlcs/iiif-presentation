using Services.Search;

namespace Services.Tests.Search;

public class TypesenseSettingsTests
{
    [Fact]
    public void IsCustomerIncluded_UsesWhitelist_WhenPresent()
    {
        var settings = new TypesenseSettings
        {
            WhitelistCustomerIds = [2],
            BlacklistCustomerIds = [2, 3]
        };

        settings.IsCustomerIncluded(2).Should().BeTrue();
        settings.IsCustomerIncluded(3).Should().BeFalse();
        settings.IsCustomerIncluded(4).Should().BeFalse();
    }

    [Fact]
    public void IsCustomerIncluded_UsesBlacklist_WhenWhitelistIsEmpty()
    {
        var settings = new TypesenseSettings
        {
            BlacklistCustomerIds = [3]
        };

        settings.IsCustomerIncluded(2).Should().BeTrue();
        settings.IsCustomerIncluded(3).Should().BeFalse();
    }
}
