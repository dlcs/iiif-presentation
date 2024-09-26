using Sqids;

namespace API.Infrastructure.IdGenerator;

public class SqidsGenerator(SqidsEncoder<long> sqids) : IIdGenerator
{
    private const int ListLength = 5;
    
    public string Generate(List<long>? seed = null)
    {
        if (seed == null)
        {
            Random rand = new Random();
            seed = Enumerable.Range(0, ListLength)
                .Select(i => new Tuple<int, long>(rand.Next(int.MaxValue), i))
                .OrderBy(i => i.Item1)
                .Select(i => i.Item2).ToList();
        }
        
        return sqids.Encode(seed);
    }
}