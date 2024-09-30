namespace API.Infrastructure.IdGenerator;

public interface IIdGenerator
{
    string Generate(List<long>? seed = null);
}