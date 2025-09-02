namespace Chota.Api.Services;

public interface IUrlEncoder
{
    string Encode(long id);
    long Decode(string shortCode);
}