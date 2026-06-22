using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CBIZ.SharedPackages.AzureKeyVaultConfiguration.UnitTests.tests
{
    [TestClass]
    public class SecretManagerTests
    {
        private static AzureKeyVaultSecretsManager SecretsManager(params string[] keyVaultNames)
        {
            AzureKeyVaultAsConfigOptions options = new()
            {
                KeyVaultNames = keyVaultNames.ToList(),
                CertPath = "CBIZ-Dev-KvAccess.pfx"
            };

            return new AzureKeyVaultSecretsManager(NullLogger<AzureKeyVaultSecretsManager>.Instance, Options.Create(options));
        }

        [TestMethod]
        public void Constructor_WhenCertificateCannotBeLoaded_ThrowsAzureKeyVaultAsConfigException()
        {
            AzureKeyVaultAsConfigOptions options = new()
            {
                KeyVaultNames = ["test-vault"],
                CertPath = "missing-cert.pfx"                  
            };

            AzureKeyVaultAsConfigException ex = Assert.ThrowsException<AzureKeyVaultAsConfigException>(() => new AzureKeyVaultSecretsManager(NullLogger<AzureKeyVaultSecretsManager>.Instance, Options.Create(options), false));

            Assert.AreEqual("Unable to load Certificate for KeyVault access.", ex.Message);
        }

        [TestMethod]
        public async Task GetSecretAsync_WhenNoKeyVaultNamesAreConfigured_ReturnsFailure()
        {
            AzureKeyVaultSecretsManager subject = SecretsManager();

            AzureKeyVaultAsConfigException? failure = null;

            (await subject.GetSecretAsync("test-secret"))
                .WhenFailure((_, ex) => failure = ex);

            Assert.IsNotNull(failure);
            Assert.AreEqual("Unable to determine KeyVault. keyVaultIndex 0 out of range. KeyVaultList size 0", failure.Message);
        }

        [TestMethod]
        public async Task SetSecretAsync_WhenNoKeyVaultNamesAreConfigured_ReturnsFailure()
        {
            AzureKeyVaultSecretsManager subject = SecretsManager();

            AzureKeyVaultAsConfigException? failure = null;

            (await subject.SetSecretAsync("test-secret", "test-value"))
                .WhenFailure((_, ex) => failure = ex);

            Assert.IsNotNull(failure);
            Assert.AreEqual("Unable to determine KeyVault. keyVaultIndex 0 out of range. KeyVaultList size 0", failure.Message);
        }

        [TestMethod]
        public async Task GetSecretAsync_WhenKeyVaultIndexIsNegative_ReturnsFailure()
        {
            AzureKeyVaultSecretsManager subject = SecretsManager("test-vault");

            AzureKeyVaultAsConfigException? failure = null;

            (await subject.GetSecretAsync(-1, "test-secret"))
                .WhenFailure((_, ex) => failure = ex);

            Assert.IsNotNull(failure);
            Assert.AreEqual("Unable to determine KeyVault. keyVaultIndex -1 out of range. KeyVaultList size 1", failure.Message);
        }

        [TestMethod]
        public async Task GetSecretAsync_WhenKeyVaultIndexIsGreaterThanConfiguredCount_ReturnsFailure()
        {
            AzureKeyVaultSecretsManager subject = SecretsManager("test-vault");

            AzureKeyVaultAsConfigException? failure = null;

            (await subject.GetSecretAsync(1, "test-secret"))
                .WhenFailure((_, ex) => failure = ex);

            Assert.IsNotNull(failure);
            Assert.AreEqual("Unable to determine KeyVault. keyVaultIndex 1 out of range. KeyVaultList size 1", failure.Message);
        }

        [TestMethod]
        public async Task SetSecretAsync_WhenKeyVaultIndexIsNegative_ReturnsFailure()
        {
            AzureKeyVaultSecretsManager subject = SecretsManager("test-vault");

            AzureKeyVaultAsConfigException? failure = null;

            (await subject.SetSecretAsync(-1, "test-secret", "test-value"))
                .WhenFailure((_, ex) => failure = ex);

            Assert.IsNotNull(failure);
            Assert.AreEqual("Unable to determine KeyVault. keyVaultIndex -1 out of range. KeyVaultList size 1", failure.Message);
        }

        [TestMethod]
        public async Task SetSecretAsync_WhenKeyVaultIndexIsGreaterThanConfiguredCount_ReturnsFailure()
        {
            AzureKeyVaultSecretsManager subject = SecretsManager("test-vault");

            AzureKeyVaultAsConfigException? failure = null;

            (await subject.SetSecretAsync(1, "test-secret", "test-value"))
                .WhenFailure((_, ex) => failure = ex);

            Assert.IsNotNull(failure);
            Assert.AreEqual("Unable to determine KeyVault. keyVaultIndex 1 out of range. KeyVaultList size 1", failure.Message);
        }
    }
}