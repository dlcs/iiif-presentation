using Sqids;

namespace API.Infrastructure.IdGenerator;

/// <summary>
/// Id generator using Sqids 
/// </summary>
/// <remarks>See https://github.com/sqids/sqids-dotnet </remarks>
public class SqidsGenerator(SqidsEncoder<long> sqids, ILogger<SqidsGenerator> logger) : IIdGenerator
{
    private const int ListLength = 5;
    
    public string Generate(List<long>? seed = null)
    {
        return sqids.Encode(seed ?? GenerateRandomSeed());
    }

    private List<long> GenerateRandomSeed()
    {
        logger.LogTrace("Generating random seed of length {ListLength}", ListLength);
        var rand = new Random();
        var seed = Enumerable.Range(0, ListLength)
            .Select(i => new Tuple<int, long>(rand.Next(int.MaxValue), i))
            .OrderBy(i => i.Item1)
            .Select(i => i.Item2).ToList();
        return seed;
    }
}