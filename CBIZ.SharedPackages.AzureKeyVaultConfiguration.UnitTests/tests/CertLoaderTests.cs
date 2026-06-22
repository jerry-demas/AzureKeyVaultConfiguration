using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CBIZ.SharedPackages.AzureKeyVaultConfiguration.UnitTests.tests
{
    [TestClass]
    public class CertLoaderTests
    {

        [TestMethod]
        public void CheckForMissingCertFile()
        {
            //arrange
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddJsonFile("appsettingsCertBadPath.json");

            //act -- do not use default certs becuase there is a chance the default cert is on the dev's machine
            AzureKeyVaultAsConfigException ex = Assert.ThrowsException<AzureKeyVaultAsConfigException>(() => builder.AddAzureKeyVaultAsConfig(false));

            //assert
            Assert.AreEqual($"Unable to load Certificate for KeyVault access.", ex.Message);
        }

        [TestMethod]
        public void LoadFalseDevCert_Success() //
        {
            //arrange

            //act -- do not use default certs becuase there is a chance the default cert is on the dev's machine
            var cert = CertLoader.LoadCertificate(new CertLoaderOptions { CertPath = "falseDevAppCert.pfx", CertThumbprint = "", PasswordOverride = "password" }, false);

            //assert
            Assert.IsNotNull(cert);
        }

        [TestMethod]
        public void LoadDevCert_Success() //
        {
            //arrange

            //act -- do not use default certs becuase there is a chance the default cert is on the dev's machine
            var cert = CertLoader.LoadCertificate(new CertLoaderOptions { CertPath = "CBIZ-Dev-KvAccess.pfx", CertThumbprint = "" }, false);

            //assert
            Assert.IsNotNull(cert);
        }

        [TestMethod]
        public void LoadCertPath_Fail() //
        {
            //arrange

            //act -- do not use default certs becuase there is a chance the default cert is on the dev's machine
            AzureKeyVaultAsConfigException ex = Assert.ThrowsException<AzureKeyVaultAsConfigException>(() => CertLoader.LoadCertificate(new CertLoaderOptions { CertPath = "falseDevAppCert2.pfx", CertThumbprint = "", PasswordOverride = "password" }, false));
            AzureKeyVaultAsConfigException ex2 = Assert.ThrowsException<AzureKeyVaultAsConfigException>(() => CertLoader.LoadCertificate(new CertLoaderOptions { CertPath = "/test/falseDevAppCert2.pfx", CertThumbprint = "", PasswordOverride = "password" }, false));

            //assert
            Assert.AreEqual($"Unable to load Certificate for KeyVault access.", ex.Message);
            Assert.AreEqual($"Unable to load Certificate for KeyVault access.", ex2.Message);

        }

        [TestMethod]
        public void LoadCertThumb_Fail() //
        {
            //arrange

            //act -- do not use default certs becuase there is a chance the default cert is on the dev's machine
            AzureKeyVaultAsConfigException ex = Assert.ThrowsException<AzureKeyVaultAsConfigException>(() => CertLoader.LoadCertificate(new CertLoaderOptions { CertPath = "", CertThumbprint = "5455354345", PasswordOverride = "password" }, false));

            //assert
            Assert.AreEqual($"Unable to load Certificate for KeyVault access.", ex.Message);
        }

        //we can't test the thumbprint as it requires opening and altering a cert store
    }
}