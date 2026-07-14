using System.Security.Cryptography;
using Shouldly;

namespace DbCredentialCrypto.Tests;

public class AesGcmCipherTests
{
    private static byte[] RandomKey() => RandomNumberGenerator.GetBytes(AesGcmCipher.KeySizeBytes);

    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        var cipher = new AesGcmCipher(RandomKey());
        const string plaintext = "Sup3r$ecretDbPassw0rd!";

        var ciphertext = cipher.Encrypt(plaintext);
        ciphertext.ShouldNotBe(plaintext);

        var decrypted = cipher.Decrypt(ciphertext);
        decrypted.ShouldBe(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachTime()
    {
        var cipher = new AesGcmCipher(RandomKey());

        var a = cipher.Encrypt("same-plaintext");
        var b = cipher.Encrypt("same-plaintext");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Decrypt_WrongKeyThrows()
    {
        var cipher1 = new AesGcmCipher(RandomKey());
        var cipher2 = new AesGcmCipher(RandomKey());

        var ciphertext = cipher1.Encrypt("secret");

        Should.Throw<CryptographicException>(() => cipher2.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_TamperedCiphertextThrows()
    {
        var cipher = new AesGcmCipher(RandomKey());
        var ciphertext = cipher.Encrypt("secret");

        var raw = Convert.FromBase64String(ciphertext);
        raw[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(raw);

        Should.Throw<CryptographicException>(() => cipher.Decrypt(tampered));
    }

    [Fact]
    public void Constructor_RejectsWrongKeySize()
    {
        Should.Throw<ArgumentException>(() => new AesGcmCipher("too-short"u8.ToArray()));
    }

    [Fact]
    public void FromBase64Key_RoundTripsWithGeneratedKey()
    {
        var key = RandomKey();
        var encoded = Convert.ToBase64String(key);

        var cipher = AesGcmCipher.FromBase64Key(encoded);

        var ciphertext = cipher.Encrypt("hello");
        cipher.Decrypt(ciphertext).ShouldBe("hello");
    }

    [Fact]
    public void Decrypt_TooShortCiphertextThrows()
    {
        var cipher = new AesGcmCipher(RandomKey());

        Should.Throw<CryptographicException>(() => cipher.Decrypt(Convert.ToBase64String("x"u8.ToArray())));
    }

    [Fact]
    public void Decrypt_InvalidBase64Throws()
    {
        var cipher = new AesGcmCipher(RandomKey());

        Should.Throw<FormatException>(() => cipher.Decrypt("not-valid-base64!!!"));
    }

    [Fact]
    public void GoInterop_DecryptsCiphertextProducedByGoImplementation()
    {
        // 固定密钥 + 由 Go 版 dbcredentialcrypto（packages/db-credential-crypto/go）用
        // 同一密钥实际加密产出的密文，验证两端二进制兼容——同一份密钥能互相解密，
        // 而不是分别独立实现两套互不兼容的格式。
        var key = Convert.FromBase64String("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var cipher = new AesGcmCipher(key);

        const string ciphertextFromGo = "UTOYDoFvJCqZPBIYjZ7aG7UcNIr3dNk69NTj3Vlfi87Kjc7kUlN3c85oyJbxEssj";

        cipher.Decrypt(ciphertextFromGo).ShouldBe("hello-cross-language");
    }
}
