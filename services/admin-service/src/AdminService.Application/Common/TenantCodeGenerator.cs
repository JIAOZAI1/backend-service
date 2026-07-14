using System.Security.Cryptography;

namespace AdminService.Application.Common;

/// <summary>
/// 生成全局唯一的租户 code：12 位小写 base32（排除易混淆字符 0/1/o/l），
/// 用于拼接租户数据库名/数据库用户名（tenant_{code}）。
/// 唯一性最终由 tenants.tenant_code 唯一索引兜底，本生成器只保证碰撞概率极低。
/// </summary>
public static class TenantCodeGenerator
{
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyz23456789";
    private const int Length = 12;

    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }
}
