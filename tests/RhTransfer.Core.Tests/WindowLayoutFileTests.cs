using System.Collections.Generic;
using System.IO;

using RhTransfer.Core.Infrastructure;

using Xunit;

namespace RhTransfer.Core.Tests;

public sealed class WindowLayoutFileTests
{

  private const string Layout = """
    <?xml version="1.0" encoding="utf-8"?>
    <RhinoUI major_ver="1" minor_ver="0">
      <files>
        <file_name guid="c7da84fc" source="Assembly">/one/default.rui</file_name>
        <file_name guid="2531e4e5" plug_in_guid="abc" source="PlugInFolder">/two/other.rui</file_name>
      </files>
      <note>this /one/default.rui mention is not a reference</note>
    </RhinoUI>
    """;

  [Fact]
  public void ReadsEveryReference()
  {
    using TempFolder temp = new();
    string file = temp.File("containers.xml", Layout);

    Assert.Equal(["/one/default.rui", "/two/other.rui"], WindowLayoutFile.ReadReferences(file));
  }

  /// <summary>
  /// Rhino 9 rewrites these files with a blanket string replace, which also hits prose that
  /// happens to contain the same path. Only the element's own text should change.
  /// </summary>
  [Fact]
  public void RewritesOnlyTheElementText()
  {
    using TempFolder temp = new();
    string file = temp.File("containers.xml", Layout);

    int changed = WindowLayoutFile.RewriteReferences(file, p => p == "/one/default.rui" ? "UI/default.rui" : p);

    string result = File.ReadAllText(file);

    Assert.Equal(1, changed);
    Assert.Contains(">UI/default.rui</file_name>", result);
    Assert.Contains("this /one/default.rui mention is not a reference", result);
  }

  [Fact]
  public void SurvivesXmlEscapes()
  {
    using TempFolder temp = new();
    string file = temp.File("containers.xml",
      """<files><file_name source="File">/a &amp; b/tool.rui</file_name></files>""");

    Assert.Equal(["/a & b/tool.rui"], WindowLayoutFile.ReadReferences(file));

    WindowLayoutFile.RewriteReferences(file, _ => "/c & d/tool.rui");

    Assert.Equal(["/c & d/tool.rui"], WindowLayoutFile.ReadReferences(file));
    Assert.Contains("&amp;", File.ReadAllText(file));
  }

  [Fact]
  public void FindsContainersAndWorkspacesInEveryScheme()
  {
    using TempFolder temp = new();
    string settings = temp.Folder("settings");

    temp.File("settings/Scheme__Default/containers.xml", Layout);
    temp.File("settings/Scheme__Default/workspaces/a.xml", Layout);
    temp.File("settings/Scheme__ENDefault/containers.xml", Layout);
    temp.File("settings/settings-Scheme__Default.xml", "<settings id=\"2.0\" />");

    IReadOnlyList<string> found = WindowLayoutFile.Find(settings);

    Assert.Equal(3, found.Count);
  }

  [Fact]
  public void NoLayoutFilesIsNotAnError()
  {
    using TempFolder temp = new();

    Assert.Empty(WindowLayoutFile.Find(Path.Combine(temp.Path, "nothing-here")));
  }

}
