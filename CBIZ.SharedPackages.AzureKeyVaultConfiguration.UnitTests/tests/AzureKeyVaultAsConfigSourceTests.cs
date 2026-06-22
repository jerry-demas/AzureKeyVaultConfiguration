using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CBIZ.SharedPackages.AzureKeyVaultConfiguration.UnitTests.tests
{
    [TestClass]
    public class AzureKeyVaultAsConfigSourceTests
    {
        [TestMethod]
        public void CheckForSettings()
        {
            //arrange
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("appsettingsBlank.json");

            //act
            OptionsValidationException ex = Assert.ThrowsException<OptionsValidationException>(() => builder.AddAzureKeyVaultAsConfig());

            //assert
            Assert.AreEqual($"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.KeyVaultNames)}: Must be populated", ex.Message);
        }

        [TestMethod]
        [DataRow("appsettingsInvalid1.json", $"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.AzureAdApplicationId)}: Must be populated")]
        [DataRow("appsettingsInvalid2.json", $"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.KeyVaultNames)}: Key Vault Names cannot be blank")]
        [DataRow("appsettingsInvalid3.json", $"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.KeyVaultNames)}: Must be populated")]
        [DataRow("appsettingsInvalid4.json", $"{nameof(AzureKeyVaultAsConfigOptions)} - Only one of {nameof(AzureKeyVaultAsConfigOptions.CertThumbprint)} or {nameof(AzureKeyVaultAsConfigOptions.CertPath)} can be populated")]
        [DataRow("appsettingsInvalid5.json", $"{nameof(AzureKeyVaultAsConfigOptions)} - {nameof(AzureKeyVaultAsConfigOptions.KeyVaultNames)}: Must be populated")]
        [DataRow("appsettingsInvalid6.json", $"{nameof(AzureKeyVaultAsConfigOptions)} - Only one of {nameof(AzureKeyVaultAsConfigOptions.CertThumbprint)} or {nameof(AzureKeyVaultAsConfigOptions.CertPath)} can be populated")]
        public void CheckForInvalidSettings(string fileName, string expectedIssue)
        {
            //arrange
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile(fileName);

            //act
            OptionsValidationException ex = Assert.ThrowsException<OptionsValidationException>(() => builder.AddAzureKeyVaultAsConfig());

            //assert
            Assert.AreEqual(expectedIssue, ex.Message);
        }

        [TestMethod]
        public void CheckForMissingCertThumb()
        {
            //arrange
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("appsettingsCertBadThumb.json");
                        
            //act -- do not use default certs becuase there is a chance the default cert is on the dev's machine
            AzureKeyVaultAsConfigException ex = Assert.ThrowsException<AzureKeyVaultAsConfigException>(() => builder.AddAzureKeyVaultAsConfig(false));

            //assert
            Assert.AreEqual($"Unable to load Certificate for KeyVault access.", ex.Message);
        }

        [TestMethod]
        public void TestDisabled()
        {
            //arrange
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("disabled.json");


            builder.AddAzureKeyVaultAsConfig();

            //if we get here, the test passed
        }

        [TestMethod] 
        public void TestEnabled()
        {
            //arrange
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("enabled.json");

            //act -- do not use default certs becuase there is a chance the default cert is on the dev's machine
            AzureKeyVaultAsConfigException ex = Assert.ThrowsException<AzureKeyVaultAsConfigException>(() => builder.AddAzureKeyVaultAsConfig());

            //assert
            Assert.IsTrue(ex.Message.StartsWith("Issue connecting to the Azure Key Vault.") );
        }
    }
}