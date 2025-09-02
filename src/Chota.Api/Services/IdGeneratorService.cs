using IdGen;

namespace Chota.Api.Services;

public class IdGeneratorService(IIdGenerator<long> idGenerator) : IIdGeneratorService
{
    public long GenerateNextId() => idGenerator.CreateId();
}