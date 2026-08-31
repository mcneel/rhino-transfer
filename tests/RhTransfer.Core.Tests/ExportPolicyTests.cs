using RhTransfer.Core.Application;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class ExportPolicyTests
{

  [Theory]
  [InlineData("settings/settings-Scheme__Default.xml")]
  [InlineData("settings/Scheme__Default/containers.xml")]
  [InlineData("settings/Scheme__Default/workspaces/a.xml")]
  [InlineData("UI/default.rui")]
  [InlineData("Plug-ins/Grasshopper (b45a29b1)/settings/settings-Scheme__Default.xml")]
  [InlineData("scripts/mine.py")]
  [InlineData("Localization/en-US/Support/colors.txt")]
  public void CarriesSettings(string path) => Assert.True(ExportPolicy.ShouldExport(path));

  [Theory]
  [InlineData("License Manager/licence.lic")]
  [InlineData("keypairs/key")]
  [InlineData("ra_cache.txt")]
  [InlineData("proxy.txt")]
  public void NeverCarriesLicensing(string path) => Assert.False(ExportPolicy.ShouldExport(path));

  [Theory]
  [InlineData("Render Content/en_US/Textures/big.exr")]
  [InlineData("Preview$Cache$/thing")]
  [InlineData("settings/.DS_Store")]
  [InlineData("Plug-ins/RhinoCycles (9bc28e9e)/data/gpus/abc123")]
  [InlineData("Localization/en-US/Rhino.txt")]
  [InlineData("legacy.ini")]
  public void SkipsShippedContentAndCache(string path) => Assert.False(ExportPolicy.ShouldExport(path));

  [Fact]
  public void BackupsKeepLicensingButNotCache()
  {
    Assert.True(ExportPolicy.ShouldBackUp("License Manager/licence.lic"));
    Assert.True(ExportPolicy.ShouldBackUp("Render Content/en_US/Textures/big.exr"));
    Assert.False(ExportPolicy.ShouldBackUp("Preview$Cache$/thing"));
  }

  [Fact]
  public void ImportLeavesLicensingInPlace()
  {
    Assert.True(ExportPolicy.IsPreservedOnImport("License Manager"));
    Assert.True(ExportPolicy.IsPreservedOnImport("keypairs"));
    Assert.False(ExportPolicy.IsPreservedOnImport("settings"));
  }

}
