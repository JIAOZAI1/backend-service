// Package dbcredentialcrypto 提供数据库实例密码等敏感凭证的对称加解密，
// 供 admin-service（注册数据库实例时加密写入）与 backend-job-service
// （执行作业时解密连接目标实例）等需要读写这类凭证的服务共用。
//
// 算法固定为 AES-256-GCM：GCM 自带完整性校验（AEAD），密文被篡改时 Open 会
// 返回错误而不是静默解出错误明文，且标准库原生支持，无需引入第三方依赖。
package dbcredentialcrypto

import (
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"encoding/base64"
	"errors"
	"fmt"
)

// KeySize 是 AES-256 要求的密钥长度（字节）。
const KeySize = 32

var (
	// ErrInvalidKeySize 表示传入的密钥不是 32 字节（AES-256）。
	ErrInvalidKeySize = errors.New("dbcredentialcrypto: key must be 32 bytes (AES-256)")
	// ErrCiphertextTooShort 表示密文长度不足以包含 nonce。
	ErrCiphertextTooShort = errors.New("dbcredentialcrypto: ciphertext too short")
	// ErrDecryptionFailed 表示密文篡改或密钥不匹配（GCM 认证失败）。
	ErrDecryptionFailed = errors.New("dbcredentialcrypto: decryption failed (tampered ciphertext or wrong key)")
)

// Cipher 封装了固定密钥的 AES-256-GCM 加解密。同一个 Cipher 实例可安全并发使用。
type Cipher struct {
	aead cipher.AEAD
}

// NewCipher 用 32 字节的原始密钥构造 Cipher。密钥通常从环境变量（K8s Secret 注入）
// 读取 base64 编码后的值，用 DecodeKey 解码得到这里需要的原始字节。
func NewCipher(key []byte) (*Cipher, error) {
	if len(key) != KeySize {
		return nil, ErrInvalidKeySize
	}

	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, fmt.Errorf("dbcredentialcrypto: init aes cipher: %w", err)
	}

	aead, err := cipher.NewGCM(block)
	if err != nil {
		return nil, fmt.Errorf("dbcredentialcrypto: init gcm: %w", err)
	}

	return &Cipher{aead: aead}, nil
}

// DecodeKey 把 base64 编码的密钥（如从环境变量读到的字符串）解码为原始字节，
// 供 NewCipher 使用。密钥建议用 openssl rand -base64 32 生成。
func DecodeKey(base64Key string) ([]byte, error) {
	key, err := base64.StdEncoding.DecodeString(base64Key)
	if err != nil {
		return nil, fmt.Errorf("dbcredentialcrypto: decode base64 key: %w", err)
	}
	return key, nil
}

// Encrypt 加密明文，返回可直接落库的 base64 字符串（nonce 前置拼接在密文前）。
func (c *Cipher) Encrypt(plaintext string) (string, error) {
	nonce := make([]byte, c.aead.NonceSize())
	if _, err := rand.Read(nonce); err != nil {
		return "", fmt.Errorf("dbcredentialcrypto: generate nonce: %w", err)
	}

	ciphertext := c.aead.Seal(nonce, nonce, []byte(plaintext), nil)
	return base64.StdEncoding.EncodeToString(ciphertext), nil
}

// Decrypt 解密 Encrypt 产出的 base64 字符串，还原明文。
func (c *Cipher) Decrypt(encoded string) (string, error) {
	data, err := base64.StdEncoding.DecodeString(encoded)
	if err != nil {
		return "", fmt.Errorf("dbcredentialcrypto: decode base64 ciphertext: %w", err)
	}

	nonceSize := c.aead.NonceSize()
	if len(data) < nonceSize {
		return "", ErrCiphertextTooShort
	}

	nonce, ciphertext := data[:nonceSize], data[nonceSize:]
	plaintext, err := c.aead.Open(nil, nonce, ciphertext, nil)
	if err != nil {
		return "", ErrDecryptionFailed
	}

	return string(plaintext), nil
}
