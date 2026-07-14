using System.Security.Cryptography;

namespace DbCredentialCrypto;

/// <summary>
/// 数据库实例密码等敏感凭证的对称加解密，供 admin-service（注册数据库实例时加密写入）
/// 与 backend-job-service（执行作业时解密连接目标实例）等需要读写这类凭证的服务共用。
/// 算法固定为 AES-256-GCM，与本包的 Go 实现（packages/db-credential-crypto/go）二进制兼容：
/// 密文格式均为 base64(nonce(12字节) + ciphertext + tag(16字节))，同一份密钥两边可互相加解密。
/// </summary>
public sealed class AesGcmCipher
{
    public const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesGcmCipher(byte[] key)
    {
        if (key.Length != KeySizeBytes)
        {
            throw new ArgumentException($"key must be {KeySizeBytes} bytes (AES-256)", nameof(key));
        }

        _key = key;
    }

    /// <summary>用 base64 编码的密钥（如从环境变量/K8s Secret 读到的字符串）构造。</summary>
    public static AesGcmCipher FromBase64Key(string base64Key) => new(Convert.FromBase64String(base64Key));

    /// <summary>加密明文，返回可直接落库的 base64 字符串（nonce 前置拼接在密文前）。</summary>
    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aesGcm = new AesGcm(_key, TagSizeBytes))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        var result = new byte[NonceSizeBytes + ciphertext.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSizeBytes + ciphertext.Length, TagSizeBytes);

        return Convert.ToBase64String(result);
    }

    /// <summary>解密 Encrypt 产出的 base64 字符串，还原明文。密文被篡改或密钥不匹配时抛 CryptographicException。</summary>
    public string Decrypt(string encoded)
    {
        byte[] data;
        try
        {
            data = Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new FormatException("dbcredentialcrypto: invalid base64 ciphertext", ex);
        }

        if (data.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new CryptographicException("dbcredentialcrypto: ciphertext too short");
        }

        var nonce = data[..NonceSizeBytes];
        var tag = data[^TagSizeBytes..];
        var ciphertext = data[NonceSizeBytes..^TagSizeBytes];
        var plaintextBytes = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        // AesGcm.Decrypt 在认证失败（篡改密文或密钥不对）时抛 CryptographicException，
        // 不会静默返回错误明文，与 Go 实现的 ErrDecryptionFailed 语义一致。
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
