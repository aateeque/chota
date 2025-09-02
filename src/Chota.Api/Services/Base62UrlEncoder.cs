namespace Chota.Api.Services;

public class Base62Encoder : IUrlEncoder
{
    private const string Characters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public string Encode(long input)
    {
        if (input == 0)
        {
            return "0";
        }

        var result = new char[11]; // Max length for long in Base62
        var index = result.Length - 1;

        while (input > 0 && index >= 0)
        {
            result[index--] = Characters[(int)(input % 62)];
            input /= 62;
        }

        return new string(result, index + 1, result.Length - index - 1);
    }

    public long Decode(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return 0;
        }

        long result = 0;
        foreach (var c in encoded)
        {
            var index = Characters.IndexOf(c);
            if (index < 0)
            {
                throw new ArgumentException($"Invalid Base62 character '{c}'");
            }
            result = result * 62 + index;
        }

        return result;
    }
}
