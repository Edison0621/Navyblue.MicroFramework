using System.Net.Http.Json;
using Dapr.Client;
using DaprFx.Core;

namespace DaprFx.Cryptography;

public sealed class DaprCryptoService(DaprClient client) : ICryptoService
{
    private readonly DaprClient _client = client;

    public async Task<byte[]> EncryptAsync(string keyName, byte[] plaintext, CancellationToken ct = default)
    {
        var endpoint = $"/v1.0-alpha1/crypto/{keyName}/encrypt";
        var payload = new { data = Convert.ToBase64String(plaintext) };
        using var httpClient = DaprClient.CreateInvokeHttpClient(appId: null);
        var response = await httpClient.PostAsJsonAsync(endpoint, payload, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CryptoResponse>(cancellationToken: ct);
        return Convert.FromBase64String(body?.Data ?? string.Empty);
    }

    public async Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, CancellationToken ct = default)
    {
        var endpoint = $"/v1.0-alpha1/crypto/{keyName}/decrypt";
        var payload = new { data = Convert.ToBase64String(ciphertext) };
        using var httpClient = DaprClient.CreateInvokeHttpClient(appId: null);
        var response = await httpClient.PostAsJsonAsync(endpoint, payload, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CryptoResponse>(cancellationToken: ct);
        return Convert.FromBase64String(body?.Data ?? string.Empty);
    }

    private sealed class CryptoResponse
    {
        public string Data { get; set; } = string.Empty;
    }
}
