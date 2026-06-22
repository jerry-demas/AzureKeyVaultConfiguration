using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CBIZ.SharedPackages.AzureKeyVaultConfiguration;

public sealed record CertLoaderOptions
{
    public required string CertThumbprint { get; init; }
    public required string CertPath { get; init; }
    public string? PasswordOverride { get; init; } = null;
}

public static class CertLoader
{
    const string WINDOWS_DEV_CERT_THUMBPRINT = "6e145c082506e0f7d2b4993d0572a0a90a155887";
    const string DEFAULT_LINUX_CONTAINER_CERT_LOCATION = "/run/secrets/appCert.pfx";
    const string DEFAULT_LINUX_DEV_CERT_LOCATION = "/etc/ssl/certs/DevAppCert.pfx";
    const string DEFAULT_CERT_PASSWORD = null;

    public static X509Certificate2 LoadCertificate(CertLoaderOptions config, bool useDefaultCerts = true)
    {
        try
        {
            X509Certificate2? retVal = null;
            string certPassword = GetCertPassword(config);

            retVal = LoadCertificateFromStore(config.CertThumbprint) ?? LoadCertificateFromFile(config.CertPath, certPassword);

            if (retVal is null && useDefaultCerts)
            {
                if (OperatingSystem.IsWindows()) retVal = LoadCertificateFromStore(WINDOWS_DEV_CERT_THUMBPRINT);
                if (OperatingSystem.IsLinux()) retVal = 
                        LoadCertificateFromFile(DEFAULT_LINUX_CONTAINER_CERT_LOCATION, certPassword) 
                        ?? LoadCertificateFromFile(DEFAULT_LINUX_DEV_CERT_LOCATION, certPassword);
            }

            if (retVal is null) throw new AzureKeyVaultAsConfigException($"Unable to load Certificate for KeyVault access.");

            return retVal;
        }
        catch (Exception ex)
        {
            throw new AzureKeyVaultAsConfigException($"Unable to load Certificate for KeyVault access.", ex);
        }
    }
    private static X509Certificate2? LoadCertificateFromStore(string certThumbprint)
    {
        if (string.IsNullOrWhiteSpace(certThumbprint)) return null;

        return LoadCertificateFromStore(certThumbprint, StoreLocation.CurrentUser) ?? // local devs
               LoadCertificateFromStore(certThumbprint, StoreLocation.LocalMachine);  // production servers          
    }

    private static X509Certificate2? LoadCertificateFromStore(string certThumbprint, StoreLocation storeLocation)
    {
        using X509Store tempstore = new X509Store(storeLocation);
        tempstore.Open(OpenFlags.ReadOnly);

        return tempstore
            .Certificates.Find(X509FindType.FindByThumbprint, certThumbprint, validOnly: false)
            .OfType<X509Certificate2>()
            .FirstOrDefault();
    }

    private static string GetCertPassword(CertLoaderOptions config)
    {
        if(!string.IsNullOrWhiteSpace(config.PasswordOverride))
        {
            return config.PasswordOverride;
        }
        else
        {
            return DEFAULT_CERT_PASSWORD;
        }
    }

    private static X509Certificate2? LoadCertificateFromFile(string certPath, string password)
    {
        if (string.IsNullOrWhiteSpace(certPath)) return null;

        try
        {
            #if NET8_0
            return new X509Certificate2(certPath, password);
            #elif NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12FromFile(certPath, password);
            #endif
        }
        catch (Exception ex) when (ex is CryptographicException)
        {
            return null;
        }   
    }
}