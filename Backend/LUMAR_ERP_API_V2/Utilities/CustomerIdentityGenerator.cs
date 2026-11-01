using System.Security.Cryptography;

namespace LUMAR_ERP_API_V2.Utilities;

public sealed class CustomerIdentityGenerator
{
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public Guid GenerateGlobalCustomerUid() => Guid.CreateVersion7();

    public string GenerateCustomerCode()
    {
        Span<char> code = stackalloc char[18];
        code[0] = 'C';
        for (var index = 1; index < code.Length; index++)
            code[index] = index is 1 or 7 or 13
                ? '-'
                : Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(code);
    }

    public bool ValidateNewCustomerCode(string? code)
    {
        if (code is null || code.Length != 18 || code[0] != 'C') return false;
        for (var index = 1; index < code.Length; index++)
        {
            if (index is 1 or 7 or 13)
            {
                if (code[index] != '-') return false;
            }
            else if (!Alphabet.Contains(code[index])) return false;
        }
        return true;
    }
}