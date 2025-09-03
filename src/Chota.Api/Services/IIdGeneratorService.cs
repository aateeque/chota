namespace Chota.Api.Services;

public interface IIdGeneratorService
{
    long GenerateNextId();

    string HashLongUrl(string longUrl);
}