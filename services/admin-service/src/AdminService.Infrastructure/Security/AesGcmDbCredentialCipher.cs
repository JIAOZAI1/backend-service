using AdminService.Application.Interfaces;
using DbCredentialCrypto;

namespace AdminService.Infrastructure.Security;

/// <summary>
/// IDbCredentialCipher 的实现，委托给共享 SDK（packages/db-credential-crypto）。
/// 密钥来自 K8s Secret 注入的环境变量（见 DB_INSTANCE_ENCRYPTION_KEY /
/// deploy/k8s/services/admin-service/deployment.yaml），启动时未配置直接抛异常。
/// </summary>
public class AesGcmDbCredentialCipher : IDbCredentialCipher
{
    private readonly AesGcmCipher _cipher;

    public AesGcmDbCredentialCipher(string base64Key)
    {
        _cipher = AesGcmCipher.FromBase64Key(base64Key);
    }

    public string Encrypt(string plaintext) => _cipher.Encrypt(plaintext);

    public string Decrypt(string ciphertext) => _cipher.Decrypt(ciphertext);
}
