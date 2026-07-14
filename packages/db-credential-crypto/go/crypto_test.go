package dbcredentialcrypto_test

import (
	"crypto/rand"
	"encoding/base64"
	"strings"
	"testing"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"

	dbcredentialcrypto "github.com/company/db-credential-crypto"
)

func randomKey(t *testing.T) []byte {
	t.Helper()
	key := make([]byte, dbcredentialcrypto.KeySize)
	_, err := rand.Read(key)
	require.NoError(t, err)
	return key
}

func TestEncryptDecrypt_RoundTrip(t *testing.T) {
	c, err := dbcredentialcrypto.NewCipher(randomKey(t))
	require.NoError(t, err)

	plaintext := "Sup3r$ecretDbPassw0rd!"
	ciphertext, err := c.Encrypt(plaintext)
	require.NoError(t, err)
	assert.NotEqual(t, plaintext, ciphertext)

	decrypted, err := c.Decrypt(ciphertext)
	require.NoError(t, err)
	assert.Equal(t, plaintext, decrypted)
}

func TestEncrypt_ProducesDifferentCiphertextEachTime(t *testing.T) {
	c, err := dbcredentialcrypto.NewCipher(randomKey(t))
	require.NoError(t, err)

	a, err := c.Encrypt("same-plaintext")
	require.NoError(t, err)
	b, err := c.Encrypt("same-plaintext")
	require.NoError(t, err)

	assert.NotEqual(t, a, b, "random nonce must make each encryption unique")
}

func TestDecrypt_WrongKeyFails(t *testing.T) {
	c1, err := dbcredentialcrypto.NewCipher(randomKey(t))
	require.NoError(t, err)
	c2, err := dbcredentialcrypto.NewCipher(randomKey(t))
	require.NoError(t, err)

	ciphertext, err := c1.Encrypt("secret")
	require.NoError(t, err)

	_, err = c2.Decrypt(ciphertext)
	assert.ErrorIs(t, err, dbcredentialcrypto.ErrDecryptionFailed)
}

func TestDecrypt_TamperedCiphertextFails(t *testing.T) {
	c, err := dbcredentialcrypto.NewCipher(randomKey(t))
	require.NoError(t, err)

	ciphertext, err := c.Encrypt("secret")
	require.NoError(t, err)

	raw, err := base64.StdEncoding.DecodeString(ciphertext)
	require.NoError(t, err)
	raw[len(raw)-1] ^= 0xFF // flip last byte
	tampered := base64.StdEncoding.EncodeToString(raw)

	_, err = c.Decrypt(tampered)
	assert.ErrorIs(t, err, dbcredentialcrypto.ErrDecryptionFailed)
}

func TestNewCipher_RejectsWrongKeySize(t *testing.T) {
	_, err := dbcredentialcrypto.NewCipher([]byte("too-short"))
	assert.ErrorIs(t, err, dbcredentialcrypto.ErrInvalidKeySize)
}

func TestDecodeKey_RoundTripsWithGeneratedKey(t *testing.T) {
	key := randomKey(t)
	encoded := base64.StdEncoding.EncodeToString(key)

	decoded, err := dbcredentialcrypto.DecodeKey(encoded)
	require.NoError(t, err)
	assert.Equal(t, key, decoded)
}

func TestDecrypt_TooShortCiphertext(t *testing.T) {
	c, err := dbcredentialcrypto.NewCipher(randomKey(t))
	require.NoError(t, err)

	_, err = c.Decrypt(base64.StdEncoding.EncodeToString([]byte("x")))
	assert.ErrorIs(t, err, dbcredentialcrypto.ErrCiphertextTooShort)
}

func TestDecrypt_InvalidBase64(t *testing.T) {
	c, err := dbcredentialcrypto.NewCipher(randomKey(t))
	require.NoError(t, err)

	_, err = c.Decrypt("not-valid-base64!!!")
	assert.Error(t, err)
	assert.True(t, strings.Contains(err.Error(), "decode base64"))
}

func TestDotNetInterop_DecryptsCiphertextProducedByDotNetImplementation(t *testing.T) {
	// 固定密钥 + 由 .NET 版 DbCredentialCrypto（packages/db-credential-crypto/dotnet）用
	// 同一密钥实际加密产出的密文，验证两端二进制兼容——同一份密钥能互相解密，
	// 而不是分别独立实现两套互不兼容的格式。
	key, err := dbcredentialcrypto.DecodeKey("MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=")
	require.NoError(t, err)
	c, err := dbcredentialcrypto.NewCipher(key)
	require.NoError(t, err)

	const ciphertextFromDotNet = "scbBekpf6IgZ4uPe3NT1z6Jhbk289MpiQUwKOCpjmuXFykNTAD5kDDIV3aC/"

	plaintext, err := c.Decrypt(ciphertextFromDotNet)
	require.NoError(t, err)
	assert.Equal(t, "hello-from-dotnet", plaintext)
}
