# db-credential-crypto

数据库实例密码等敏感凭证的对称加解密 SDK，供需要读写这类凭证的服务共用（如 admin-service 注册数据库实例时加密写入、backend-job-service 执行作业时解密连接目标实例）。按仓库规范第 10 章，公共代码统一放在 `packages/`，服务间不允许直接互相引用内部代码，只能通过这里的 SDK。

## 目录结构

```bash
db-credential-crypto/
├── go/           # Go 实现（module: github.com/company/db-credential-crypto）
├── dotnet/       # .NET 实现（DbCredentialCrypto 类库 + 单元测试）
└── README.md
```

按语言分实现而非单一语言，命名仍以能力命名（`db-credential-crypto`），符合第 10.1 节"包名表达能力而非语言实现"的要求。

## 算法

固定 **AES-256-GCM**：

* Go：`crypto/aes` + `cipher.NewGCM`（标准库，无第三方依赖）
* .NET：`System.Security.Cryptography.AesGcm`（标准库，无第三方依赖）
* GCM 自带认证（AEAD），密文被篡改或密钥不匹配时解密直接报错，不会静默返回错误的明文
* 两端密文格式二进制兼容：`base64(nonce[12字节] + ciphertext + tag[16字节])`，同一份密钥可互相加解密（各自测试套件都有一个用对方语言实际生成的密文做解密验证的用例）

## 密钥

* 密钥固定 32 字节（AES-256），建议用 `openssl rand -base64 32` 生成，以 base64 字符串形式存放
* 通过 K8s Secret 注入到使用方服务的环境变量（如 admin-service 的 `db-instance-encryption-key`，见 [deploy/k8s/base/secret-example.yaml](../../deploy/k8s/base/secret-example.yaml)），不写入仓库、不硬编码
* 两端都提供从 base64 字符串直接构造 cipher 的入口（Go: `DecodeKey` + `NewCipher`；.NET: `AesGcmCipher.FromBase64Key`）

## 用法

### Go

```go
import dbcredentialcrypto "github.com/company/db-credential-crypto"

key, err := dbcredentialcrypto.DecodeKey(os.Getenv("DB_INSTANCE_ENCRYPTION_KEY"))
cipher, err := dbcredentialcrypto.NewCipher(key)

encrypted, err := cipher.Encrypt("plaintext-password")
decrypted, err := cipher.Decrypt(encrypted)
```

### .NET

```csharp
using DbCredentialCrypto;

var cipher = AesGcmCipher.FromBase64Key(configuration["DbInstanceEncryptionKey"]);

var encrypted = cipher.Encrypt("plaintext-password");
var decrypted = cipher.Decrypt(encrypted);
```

## 测试

```bash
# Go
cd go && go test ./... -race -cover

# .NET
cd dotnet && dotnet test
```
