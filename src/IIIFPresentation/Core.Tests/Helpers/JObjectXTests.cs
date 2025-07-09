using Core.Helpers;
using Newtonsoft.Json.Linq;

namespace Core.Tests.Helpers;

public class JObjectXTests
{
    [Fact]
    public void GetRequiredValue_ReturnsValue_IfFound()
    {
        var jobject = JObject.Parse("{ \"name\": \"John Doe\" }");

        jobject.GetRequiredValue("name").ToString().Should().BeEquivalentTo("John Doe");
    }
    
    [Fact]
    public void GetRequiredValue_Throws_IfNotFound()
    {
        var jobject = JObject.Parse("{ \"name\": \"John Doe\" }");

        Action action = () => jobject.GetRequiredValue("foo");
        action.Should().ThrowExactly<InvalidOperationException>().WithMessage("Object missing 'foo' property");
    }
    
    [Fact]
    public void GetRequiredValueWithType_ReturnsValue_IfFound()
    {
        var jobject = JObject.Parse("{ \"name\": \"John Doe\" }");

        jobject.GetRequiredValue<string>("name").Should().BeEquivalentTo("John Doe");
    }
    
    [Fact]
    public void GetRequiredValueWithType_ThrowsError_IfNotCorrectType()
    {
        var jobject = JObject.Parse("{ \"name\": \"John Doe\" }");

        Action action = () => jobject.GetRequiredValue<int>("name");
        action.Should().ThrowExactly<FormatException>().WithMessage("The input string 'stuff' was not in a correct format.");
    }
}
