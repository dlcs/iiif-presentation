using API.Infrastructure.IdGenerator;
using Microsoft.Extensions.Logging.Abstractions;
using Sqids;

namespace API.Tests.Infrastructure.IdGenerator;

public class SqidsGeneratorTests
{
    private readonly SqidsGenerator sqidsGenerator;
    private readonly SqidsEncoder<long> sqidsEncoder;
    
    public SqidsGeneratorTests()
    {
        sqidsEncoder = new SqidsEncoder<long>();
        sqidsGenerator = new SqidsGenerator(sqidsEncoder, new NullLogger<SqidsGenerator>());
    }

    [Fact]
    public void Generate_GeneratesId_WithSeed()
    {
        // Arrange
        var seed = new List<long>()
        {
            1,
            2,
            3,
            4
        };
        
        // Act
        var id = sqidsGenerator.Generate(seed);
        var decoded = sqidsEncoder.Decode(id);
        
        // Assert
        id.Should().NotBeNullOrEmpty();
        decoded.Should().Equal(seed);
    }
    
    [Fact]
    public void Generate_GeneratesId_WithoutSeed()
    {
        // Act
        var id = sqidsGenerator.Generate();
        var decoded = sqidsEncoder.Decode(id);
        
        // Assert
        id.Should().NotBeNullOrEmpty();
        decoded.Count.Should().Be(5);
    }
}