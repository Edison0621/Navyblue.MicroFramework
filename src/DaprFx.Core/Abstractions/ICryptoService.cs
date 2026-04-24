namespace DaprFx.Core;

public interface ICryptoService
{
    Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken ct = default);
    Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, CancellationToken ct = default);
}
