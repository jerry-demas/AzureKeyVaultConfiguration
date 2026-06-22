# Introduction
Package designed to simplify connecting to Azure Key Vault to pull secrets into configuration and to read or update secrets directly through `AzureKeyVaultSecretsManager`.

# Usage - Dev and UAT
1. Locally, in Dev, and in UAT you will use the local certs and the default dev application id to connect to Key Vault. In most cases you only need to set `KeyVaultNames`.
2. In your `appsettings.json` file, set the names of the vaults you will be pulling from. You must provide at least one, but can provide multiple.
	```json
	{
		"AzureKeyVaultAsConfigOptions": {
			"KeyVaultNames": ["xxxxx", "xxxxy"]
		}
	}
	```
3. Add the following line to the dependency injection configuration.
	```csharp
	builder.AddAzureKeyVaultAsConfig();
	```

# Usage - Prod
In Prod you will need additional information to connect to Key Vault. This includes the Application Id and either the Cert Thumbprint or the Cert Path. This will be done via the Prod `appsettings.json` or environment variable overrides.

	```json
	{
		"AzureKeyVaultAsConfigOptions": {
			"KeyVaultNames": ["xxxxx"],
			"AzureAdApplicationId": "xxxxx",
			"CertThumbprint": "xxxxx",
			"CertPath": "xxxxx"
		}
	}
	```

Note: only one of `CertThumbprint` or `CertPath` can be populated.

Note: if you are hosting in a Linux container, you may not need to provide `CertPath` or `CertThumbprint`, as the cert can be loaded into the container to a standard location by your release pipeline.

## Override `KeyVaultNames` with environment variables - Our containers use environment variables for configuration overrides. We include .env files in our repos for this purpose.

If you want to override `KeyVaultNames` from environment variables, use the standard .NET configuration array indexing format.

For a single Key Vault:

```
AzureKeyVaultAsConfigOptions__KeyVaultNames__0="kv-appdev-main"
```

or the older style which is also supported for backwards compatibility:

```
AzureKeyVaultAsConfigOptions__KeyVaultName="kv-appdev-main"
```

For multiple Key Vaults:

```
AzureKeyVaultAsConfigOptions__KeyVaultNames__0="kv-appdev-main"
AzureKeyVaultAsConfigOptions__KeyVaultNames__1="kv-appdev-shared"
AzureKeyVaultAsConfigOptions__KeyVaultNames__2="kv-appdev-reporting"
```

This produces the equivalent configuration:

```json
{
	"AzureKeyVaultAsConfigOptions": {
		"KeyVaultNames": [
			"kv-appdev-main",
			"kv-appdev-shared",
			"kv-appdev-reporting"
		]
	}
}
```

The indexes are zero-based. If multiple vaults contain the same secret name, the last vault added takes precedence.


# Refresh configuration from Key Vault

If you want configuration values loaded through `AddAzureKeyVaultAsConfig()` to be refreshed automatically, set `RefreshIntervalInMinutes`.

```json
{
	"AzureKeyVaultAsConfigOptions": {
		"KeyVaultNames": ["xxxxx"],
		"RefreshIntervalInMinutes": 5
	}
}
```

- `null` disables automatic refresh.
- Valid values are from `1` to `1440` minutes.
- Refresh applies to configuration values loaded through the Azure Key Vault configuration provider.

# Usage - Disable

If you need to disable the Key Vault configuration source for any reason, set `Enabled` to `false` in `appsettings.json` or environment variable overrides.

```json
{
	"AzureKeyVaultAsConfigOptions": {
		"Enabled": false,
		"KeyVaultNames": ["xxxxx", "xxxxy"]
	}
}
```

One use for this is when there is an issue connecting to Key Vault and you need the compiled application to run without attempting to load Key Vault configuration.

# AzureKeyVaultSecretsManager

`AddAzureKeyVaultAsConfig()` also registers `AzureKeyVaultSecretsManager` so you can read and update secrets directly.

The manager supports:

- using the first configured vault by default
- targeting a vault by index from `KeyVaultNames`
- targeting a vault by name
- reusing `SecretClient` instances internally for each vault name

## Read a secret

```csharp
using CBIZ.SharedPackages.AzureKeyVaultConfiguration;

var secretsManager = serviceProvider.GetRequiredService<AzureKeyVaultSecretsManager>();

var secret = await secretsManager.GetSecretAsync("MySecretName");
```

Overloads are available for:

- `GetSecretAsync(string secretName)`
- `GetSecretAsync(string secretName, CancellationToken cancellationToken)`
- `GetSecretAsync(int keyVaultIndex, string secretName)`
- `GetSecretAsync(int keyVaultIndex, string secretName, CancellationToken cancellationToken)`
- `GetSecretAsync(string keyVaultName, string secretName)`
- `GetSecretAsync(string keyVaultName, string secretName, CancellationToken cancellationToken)`

These methods return `Either<string, AzureKeyVaultAsConfigException>`.

## Read a secret with a CancellationToken

```csharp
using CBIZ.SharedPackages.AzureKeyVaultConfiguration;

var secretsManager = serviceProvider.GetRequiredService<AzureKeyVaultSecretsManager>();
using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var secret = await secretsManager.GetSecretAsync(
	"kv-appsdev-testapp1",
	"MessageLoopOptions--MessagePrefix",
	cancellationTokenSource.Token);
```

## Set a secret

```csharp
using CBIZ.SharedPackages.AzureKeyVaultConfiguration;

var secretsManager = serviceProvider.GetRequiredService<AzureKeyVaultSecretsManager>();

var result = await secretsManager.SetSecretAsync("MySecretName", "MySecretValue");
```

Overloads are available for:

- `SetSecretAsync(string secretName, string secretValue)`
- `SetSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)`
- `SetSecretAsync(int keyVaultIndex, string secretName, string secretValue)`
- `SetSecretAsync(int keyVaultIndex, string secretName, string secretValue, CancellationToken cancellationToken)`
- `SetSecretAsync(string keyVaultName, string secretName, string secretValue)`
- `SetSecretAsync(string keyVaultName, string secretName, string secretValue, CancellationToken cancellationToken)`

These methods return `Possible<AzureKeyVaultAsConfigException>`.

# Note - Multiple Key Vault conflicts

If you are using multiple key vaults, be aware that if the same secret name exists in multiple vaults, the last one added will take precedence. Use unique secret names across your key vaults to avoid unexpected overrides.

# Note - Multiple Key Vaults and `AzureKeyVaultSecretsManager` - Debug
If you turn on Debug level in the logger (atleast for AzureKeyVaultSecretsManager), you will see logs indicating which vault is being used when you call `GetSecretAsync` or `SetSecretAsync` on `AzureKeyVaultSecretsManager`. This can be helpful for troubleshooting which vault is being targeted, especially if you are using multiple vaults.

# Reference

You can look at the ConsoleTester project in this repo for diffrent ways of how to use this package.

# Key Vault Setup

1. Contact DevOps to have a Key Vault setup or if you want access to an existing Key Vault.
2. Before going to Prod (or UAT if you are using Production secrets in UAT), you will need to register your app in Prod.
   1. Contact DevOps to have the application setup in Prod. This will setup your Entra application, your cert, and your Prod Key Vault.
   2. You will need to provide the following information:
	  - Application Name
	  - KeyVaultNames
	  - Initial vault values
   3. You will be provided with several pieces of information:
	  - AzureAdApplicationId
	  - CertName/Thumbprint
3. Setting Key Vault secrets can be done through the Azure Portal or through in this package. 
4. If you want to use `AzureKeyVaultSecretsManager` to set a value, the Entra App Registration you operate as will need to have Key Vault Secrets Officer rights applied to the secret (not entire vault). You will need to submit a request to the DevOps team to do this or make it part of your orginal key vault setup.
   

# Setup

To debug locally, you will need to install the dev cert on your machine. See the wiki for details.
"# AzureKeyVaultConfiguration" 
