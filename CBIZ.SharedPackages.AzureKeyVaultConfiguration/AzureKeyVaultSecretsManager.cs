using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Cbiz.SharedPackages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

namespace CBIZ.SharedPackages.AzureKeyVaultConfiguration;

public class AzureKeyVaultSecretsManager
{
    private readonly AzureKeyVaultAsConfigOptions _config;
    private readonly X509Certificate2 _keyVaultCert;
    private readonly ConcurrentDictionary<string, SecretClient> _secretClients = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AzureKeyVaultSecretsManager> _logger;

    public AzureKeyVaultSecretsManager(ILogger<AzureKeyVaultSecretsManager> logger, IOptions<AzureKeyVaultAsConfigOptions> azureKeyVaultAsConfigOptions, bool useDefaultCerts = true)
    {
        _config = azureKeyVaultAsConfigOptions.Value;
        _keyVaultCert = CertLoader.LoadCertificate(new CertLoaderOptions { CertPath = _config.CertPath, CertThumbprint = _config.CertThumbprint }, useDefaultCerts);
        _logger = logger;
    }

    private Either<SecretClient, AzureKeyVaultAsConfigException> GetSecretClient()
    {
        return GetSecretClient(0);
    }

    private Either<SecretClient, AzureKeyVaultAsConfigException> GetSecretClient(int keyVaultIndex)
    {
        if (keyVaultIndex >= 0 && keyVaultIndex < _config.KeyVaultNames.Count)
        {
            return GetSecretClient(_config.KeyVaultNames[keyVaultIndex]);
        }
        return new AzureKeyVaultAsConfigException($"Unable to determine KeyVault. {nameof(keyVaultIndex)} {keyVaultIndex} out of range. KeyVaultList size {_config.KeyVaultNames.Count}"); 
    }

    private Either<SecretClient, AzureKeyVaultAsConfigException> GetSecretClient(string keyVaultName)
    {
        _logger.LogDebug("Getting SecretClient for KeyVault: {KeyVaultName}", keyVaultName);

        return _secretClients.GetOrAdd(keyVaultName, new SecretClient(new Uri($"https://{keyVaultName}.vault.azure.net/"), new ClientCertificateCredential(
                _config.AzureAdDirectoryId,
                _config.AzureAdApplicationId,
                _keyVaultCert))
            );
    }

    #region GetSecret
    private static async Task<Either<string, AzureKeyVaultAsConfigException>> GetSecretAsync(SecretClient client, string secretName, CancellationToken cancellationToken = default)
    {
        try
        {
            Response<KeyVaultSecret> secret = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            return secret.Value.Value;
        }
        catch (Exception ex)
        {
            return new AzureKeyVaultAsConfigException($"Issue retrieving secret - '{secretName}'", ex);
        }        
    }
   
    public async Task<Either<string, AzureKeyVaultAsConfigException>> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        return await GetSecretClient().WhenFailureReturn().OnSuccess(async (client) => await GetSecretAsync(client, secretName, cancellationToken));
    }

    public async Task<Either<string, AzureKeyVaultAsConfigException>> GetSecretAsync(int keyVaultIndex, string secretName, CancellationToken cancellationToken = default)
    {
        return await GetSecretClient(keyVaultIndex).WhenFailureReturn().OnSuccess(async (client) => await GetSecretAsync(client, secretName, cancellationToken));
    }

    public async Task<Either<string, AzureKeyVaultAsConfigException>> GetSecretAsync(string keyVaultName, string secretName, CancellationToken cancellationToken = default)
    {
        return await GetSecretClient(keyVaultName).WhenFailureReturn().OnSuccess(async (client) => await GetSecretAsync(client, secretName, cancellationToken));
    }
    #endregion
        
    #region SetSecret
    private static async Task<Possible<AzureKeyVaultAsConfigException>> SetSecretAsync(SecretClient client, string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await client.SetSecretAsync(secretName, secretValue, cancellationToken);            
        }
        catch (Exception ex)
        {
            return new AzureKeyVaultAsConfigException($"Issue setting secret - '{secretName}'", ex);
        }


        return Possible.Completed;
    }

    public async Task<Possible<AzureKeyVaultAsConfigException>> SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        return await GetSecretClient().WhenFailureReturn().OnSuccess(async (client) => await SetSecretAsync(client, secretName, secretValue, cancellationToken));
    }

    public async Task<Possible<AzureKeyVaultAsConfigException>> SetSecretAsync(int keyVaultIndex, string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        return await GetSecretClient(keyVaultIndex).WhenFailureReturn().OnSuccess(async (client) => await SetSecretAsync(client, secretName, secretValue, cancellationToken));
    }

    public async Task<Possible<AzureKeyVaultAsConfigException>> SetSecretAsync(string keyVaultName, string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        return await GetSecretClient(keyVaultName).WhenFailureReturn().OnSuccess(async (client) => await SetSecretAsync(client, secretName, secretValue, cancellationToken));
    }
    #endregion
}
