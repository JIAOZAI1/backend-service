using System.Security.Cryptography;

namespace AdminService.Application.Common;

/// <summary>
/// 生成开户用的数据库密码：16 位，排除易混淆字符（0/O/1/l/I 等），
/// 保证大写字母、小写字母、数字、特殊符号各至少一位，使用加密安全随机数。
/// </summary>
public static class SecurePasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+";
    private const string AllChars = Uppercase + Lowercase + Digits + Symbols;
    private const int Length = 16;

    public static string Generate()
    {
        var chars = new char[Length];
        chars[0] = PickRandom(Uppercase);
        chars[1] = PickRandom(Lowercase);
        chars[2] = PickRandom(Digits);
        chars[3] = PickRandom(Symbols);

        for (var i = 4; i < Length; i++)
        {
            chars[i] = PickRandom(AllChars);
        }

        Shuffle(chars);
        return new string(chars);
    }

    private static char PickRandom(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    private static void Shuffle(char[] chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
    }
}
