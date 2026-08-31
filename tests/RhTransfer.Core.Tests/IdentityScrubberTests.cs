using System.IO;

using RhTransfer.Core.Application;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class IdentityScrubberTests
{

  private const string SettingsXml = """
    <?xml version="1.0" encoding="utf-8"?>
    <settings id="2.0">
      <settings>
        <entry key="name">Steven Rickards</entry>
        <entry key="email">someone@example.com</entry>
        <entry key="address1">3 Lambourn Close</entry>
        <entry key="phone">07979752794</entry>
        <entry key="LicenseRefresh">{"Status": 0}</entry>
        <entry key="RecentFiles"><list><value>D:\work\secret.3dm</value></list></entry>
        <entry key="Telemetry.LastSent">Monday</entry>
        <entry key="gradientHatch_enabled">False</entry>
        <child key="SomePlugIn">
          <entry key="name">a plug-in setting called name</entry>
          <entry key="EMail">tech@mcneel.com</entry>
        </child>
      </settings>
    </settings>
    """;

  [Fact]
  public void RemovesRegistrationAndMachineNoise()
  {
    using TempFolder temp = new();
    string file = temp.File("settings-Scheme__Default.xml", SettingsXml);

    int? removed = IdentityScrubber.Scrub(file);

    Assert.Equal(7, removed);

    string result = File.ReadAllText(file);
    Assert.DoesNotContain("Steven Rickards", result);
    Assert.DoesNotContain("someone@example.com", result);
    Assert.DoesNotContain("Lambourn", result);
    Assert.DoesNotContain("07979752794", result);
    Assert.DoesNotContain("LicenseRefresh", result);
    Assert.DoesNotContain("secret.3dm", result);
    Assert.DoesNotContain("Telemetry.LastSent", result);
  }

  [Fact]
  public void KeepsRealSettings()
  {
    using TempFolder temp = new();
    string file = temp.File("settings-Scheme__Default.xml", SettingsXml);

    IdentityScrubber.Scrub(file);

    Assert.Contains("gradientHatch_enabled", File.ReadAllText(file));
  }

  /// <summary>
  /// A plug-in is entitled to its own key called "name", and to a vendor support address.
  /// Only the root settings element belongs to the licensing component.
  /// </summary>
  [Fact]
  public void LeavesNestedPlugInSettingsAlone()
  {
    using TempFolder temp = new();
    string file = temp.File("settings-Scheme__Default.xml", SettingsXml);

    IdentityScrubber.Scrub(file);

    string result = File.ReadAllText(file);
    Assert.Contains("a plug-in setting called name", result);
    Assert.Contains("tech@mcneel.com", result);
  }

  [Fact]
  public void ReportsUnreadableFilesRatherThanThrowing()
  {
    using TempFolder temp = new();
    string file = temp.File("settings-Scheme__Default.xml", "this is not xml");

    Assert.Null(IdentityScrubber.Scrub(file));
  }

}
