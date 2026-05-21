using System.Reflection;
using Nexill.RetailStorePOS.Services.DeviceIdentity;

namespace DeviceIdentityTests;

[TestClass]
public sealed class DeviceIdentityServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CreateDeviceIdentity_UsesAppMetadata_AndStableFingerprintShape()
    {
        var service = new DeviceIdentityService();
        var identity = service.CreateDeviceIdentity();

        var assembly = typeof(DeviceIdentityService).Assembly;
        var expectedProduct = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var expectedVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0];

        TestContext.WriteLine($"AppProduct: {identity.AppProduct}");
        TestContext.WriteLine($"AppVersion: {identity.AppVersion}");
        TestContext.WriteLine($"DeviceId: {identity.DeviceId}");
        TestContext.WriteLine($"MachineFingerprintHash: {identity.MachineFingerprintHash}");
        TestContext.WriteLine($"CreatedAtUtc: {identity.CreatedAtUtc:O}");

        Assert.AreEqual(1, identity.FingerprintVersion);
        Assert.IsFalse(string.IsNullOrWhiteSpace(identity.DeviceId));
        Assert.IsTrue(identity.DeviceId.StartsWith("DEV-", StringComparison.Ordinal));
        Assert.AreEqual(12, identity.DeviceId.Length);

        Assert.IsFalse(string.IsNullOrWhiteSpace(identity.MachineFingerprintHash));
        Assert.AreEqual(64, identity.MachineFingerprintHash.Length);
        Assert.IsTrue(identity.MachineFingerprintHash.All(Uri.IsHexDigit));

        Assert.AreEqual(TimeSpan.Zero, identity.CreatedAtUtc.Offset);

        if (!string.IsNullOrWhiteSpace(expectedProduct))
        {
            Assert.AreEqual(expectedProduct, identity.AppProduct);
        }
        else
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(identity.AppProduct));
        }

        if (!string.IsNullOrWhiteSpace(expectedVersion))
        {
            Assert.AreEqual(expectedVersion, identity.AppVersion);
        }
        else
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(identity.AppVersion));
        }
    }
}
