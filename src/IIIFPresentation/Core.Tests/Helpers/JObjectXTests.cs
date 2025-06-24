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
    public void TryGetRequiredValue_ReturnsValue_IfFound()
    {
        var jobject = JObject.Parse("{ \"name\": \"John Doe\" }");

        jobject.TryGetValue("name").ToString().Should().BeEquivalentTo("John Doe");
    }
    
    [Fact]
    public void TryGetRequiredValue_Throws_IfNotFound()
    {
        var jobject = JObject.Parse("{ \"name\": \"John Doe\" }");

        jobject.TryGetValue("foo").Should().BeNull();
    }
}
