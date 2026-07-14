namespace AdminService.Application.Interfaces;

/// <summary>
/// 数据库实例密码等敏感凭证的加解密。实现基于共享 SDK（packages/db-credential-crypto），
/// 放在 Infrastructure 层是因为密钥来自环境变量/K8s Secret，属于基础设施配置关注点，
/// Application 层只依赖这个接口，不关心具体加密算法与密钥来源。
/// </summary>
public interface IDbCredentialCipher
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
