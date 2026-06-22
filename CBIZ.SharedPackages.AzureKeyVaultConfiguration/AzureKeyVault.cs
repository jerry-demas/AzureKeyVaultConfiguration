using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using CBIZ.SharedPackages.AzureKeyVaultConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography.X509Certificates;

namespace CBIZ.SharedPackages;

public sealed class AzureKeyVaultAsConfigException(string message, Exception? innerException = null) : Exception(message, innerException) { }


public sealed record AzureKeyVaultAsConfigOptions
{
    [Obsolete("This property is deprecated. Please use KeyVaultNames instead.")]
    public string KeyVaultName { get { return KeyVaultNames?.FirstOrDefault() ?? string.Empty; } init { if (!string.IsNullOrWhiteSpace(value)) { KeyVaultNames = new List<string> { value }; } } }
    public required List<string> KeyVaultNames { get; init; }
    public string AzureAdApplicationId { get; init; } = "291a7bc8-49b1-46b8-800b-b0d90639a31a";
    //AzureAdDirectoryId is read only becuase we should only have one Azure Ad in CBIZ. Change to init if this changes, then users can customize.
    public string AzureAdDirectoryId { get; } = "257e71c6-c844-4df3-b72a-56f8afd013ae"; 
    public string CertThumbprint { get; init; } = "";
    public string CertPath { get; init; } = "";
    public int? RefreshIntervalInMinutes { get; init; } = null;
    public bool Enabled { get; init; } = true;
}

public class AzureKeyVaultAsConfigOptionsValidation : IValidateOptions<AzureKeyVaultAsConfigOptions>
{
    const int REFRESH_INTERVAL_IN_MINUTES_MIN = 1;
    const int REFRESH_INTERVAL_IN_MINUTES_MAX = 1440; //24 hrs
    public ValidateOptionsResult Validate(string? name, AzureKeyVaultAsConfigOptions options)
    {
        
        List<string> fails = new List<string>();
        if (options.KeyVaultNames is null || options.KeyVaultNames.Count == 0)
        {
            fails.Add($"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.KeyVaultNames)}: Must be populated");
        }

        if (options.KeyVaultNames?.Any(x => string.IsNullOrWhiteSpace(x)) ?? false)
        {
            fails.Add($"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.KeyVaultNames)}: Key Vault Names cannot be blank");
        }

        if (String.IsNullOrWhiteSpace(options.AzureAdApplicationId))
        {
            fails.Add($"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.AzureAdApplicationId)}: Must be populated");
        }

        if (String.IsNullOrWhiteSpace(options.AzureAdDirectoryId))
        {
            fails.Add($"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.AzureAdDirectoryId)}: Must be populated");
        }

        if (!String.IsNullOrWhiteSpace(options.CertThumbprint) && !String.IsNullOrWhiteSpace(options.CertPath))
        {
            fails.Add($"{nameof(AzureKeyVaultAsConfigOptions)} - Only one of {nameof(AzureKeyVaultAsConfigOptions.CertThumbprint)} or {nameof(AzureKeyVaultAsConfigOptions.CertPath)} can be populated");
        }

        if (options.RefreshIntervalInMinutes is not null && (options.RefreshIntervalInMinutes < REFRESH_INTERVAL_IN_MINUTES_MIN || options.RefreshIntervalInMinutes > REFRESH_INTERVAL_IN_MINUTES_MAX))
        {
            fails.Add($"{nameof(AzureKeyVaultAsConfigOptions.RefreshIntervalInMinutes)} must be a number between {REFRESH_INTERVAL_IN_MINUTES_MIN.ToString()} and {REFRESH_INTERVAL_IN_MINUTES_MAX.ToString()}, or null to signify no refresh");
        }

        if (fails.Count > 0) return ValidateOptionsResult.Fail(fails);


        return ValidateOptionsResult.Success;
    }
}

public static class AzureKeyVaultExtensions
{
    public static IHostApplicationBuilder AddAzureKeyVaultAsConfig(this IHostApplicationBuilder builder, bool useDefaultCerts = true)
    {
        AzureKeyVaultAsConfigOptions config = builder.Configuration.GetSection(nameof(AzureKeyVaultAsConfigOptions)).Get<AzureKeyVaultAsConfigOptions>() ?? new AzureKeyVaultAsConfigOptions { KeyVaultNames = []};
        if (config.Enabled)
        {
            ValidateOptionsResult validation = new AzureKeyVaultAsConfigOptionsValidation().Validate(string.Empty, config);

            if (validation.Failed) throw new OptionsValidationException(nameof(AzureKeyVaultAsConfigOptions), typeof(AzureKeyVaultAsConfigOptions), validation.Failures);
       
            X509Certificate2 x509Certificate = CertLoader.LoadCertificate(new CertLoaderOptions { CertPath = config.CertPath, CertThumbprint = config.CertThumbprint }, useDefaultCerts);
            try
            {
                config.KeyVaultNames.ForEach(keyVaultName =>
                {
                    builder.Configuration.AddAzureKeyVault(
                       new Uri($"https://{keyVaultName}.vault.azure.net/"),
                       new ClientCertificateCredential(
                           config.AzureAdDirectoryId,
                           config.AzureAdApplicationId,
                           x509Certificate),
                       new AzureKeyVaultConfigurationOptions { ReloadInterval = 
                            config.RefreshIntervalInMinutes is null ? 
                            null: TimeSpan.FromMinutes((double)config.RefreshIntervalInMinutes) }
                       );
                });
            }
            catch (Exception ex)
            {
                throw new AzureKeyVaultAsConfigException($"Issue connecting to the Azure Key Vault.", ex);
            }

            builder.Services.AddOptions<AzureKeyVaultAsConfigOptions>().BindConfiguration(nameof(AzureKeyVaultAsConfigOptions)).ValidateOnStart();
            builder.Services.AddScoped<AzureKeyVaultSecretsManager>();
        }

        return builder;
    }       
}


