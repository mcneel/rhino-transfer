using System.Collections.Generic;
using System.IO;
using System.Linq;

using RhTransfer.Core.Domain;
using RhTransfer.Core.Infrastructure;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class RhinoInstallLocatorTests
{

  private static string Bundle(TempFolder temp, string name, string identifier)
  {
    temp.File($"Applications/{name}/Contents/Info.plist", $"""
      <?xml version="1.0" encoding="UTF-8"?>
      <plist version="1.0">
      <dict>
        <key>CFBundleName</key>
        <string>{name}</string>
        <key>CFBundleIdentifier</key>
        <string>{identifier}</string>
      </dict>
      </plist>
      """);

    return Path.Combine(temp.Path, "Applications");
  }

  /// <summary>
  /// The bundle identifier, not the bundle name: a prerelease is called RhinoBETA.app and its
  /// name says nothing about which Rhino it is.
  /// </summary>
  [Fact]
  public void ReadsTheVersionFromTheBundleIdentifier()
  {
    using TempFolder temp = new();

    Bundle(temp, "Rhino 7.app", "com.mcneel.rhinoceros.7");
    Bundle(temp, "Rhino 8.app", "com.mcneel.rhinoceros.8");
    string applications = Bundle(temp, "RhinoBETA.app", "com.mcneel.rhinoceros.9");

    int[] found = [.. RhinoInstallLocator.FindOnMac(applications).Select(v => v.Major)];

    Assert.Equal([7, 8, 9], found);
  }

  [Fact]
  public void IgnoresApplicationsThatAreNotRhino()
  {
    using TempFolder temp = new();

    Bundle(temp, "Rhino 8.app", "com.mcneel.rhinoceros.8");
    Bundle(temp, "Grasshopper.app", "com.mcneel.grasshopper");
    string applications = Bundle(temp, "Safari.app", "com.apple.Safari");

    Assert.Equal([8], RhinoInstallLocator.FindOnMac(applications).Select(v => v.Major));
  }

  [Fact]
  public void MissingApplicationsFolderIsEmptyRatherThanAnError()
  {
    using TempFolder temp = new();

    Assert.Empty(RhinoInstallLocator.FindOnMac(Path.Combine(temp.Path, "nope")));
  }

  /// <summary>
  /// An uninstall can leave the folder behind, so the executable is what proves an install.
  /// </summary>
  [Fact]
  public void WindowsNeedsTheExecutableNotJustTheFolder()
  {
    using TempFolder temp = new();

    temp.File("Program Files/Rhino 8/System/Rhino.exe", "exe");
    temp.File("Program Files/Rhino 9 WIP/System/Rhino.exe", "exe");
    temp.Folder("Program Files/Rhino 6");
    temp.Folder("Program Files/Rhinoceros");
    temp.File("Program Files/Some App/System/Rhino.exe", "exe");

    string programFiles = Path.Combine(temp.Path, "Program Files");

    int[] found = [.. RhinoInstallLocator.FindOnWindows(programFiles).Select(v => v.Major)];

    Assert.Equal([8, 9], found);
  }

}
