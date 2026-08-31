using System;
using System.IO;

namespace RhTransfer.Core.Tests;

/// <summary>
/// A throwaway directory. Tests never touch a real Rhino folder.
/// </summary>
public sealed class TempFolder : IDisposable
{

  public string Path { get; }

  public TempFolder()
  {
    Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rh-transfer-tests", Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(Path);
  }

  public string File(string relativePath, string contents)
  {
    string full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
    string? folder = System.IO.Path.GetDirectoryName(full);
    if (folder is not null) Directory.CreateDirectory(folder);

    System.IO.File.WriteAllText(full, contents);
    return full;
  }

  public string Folder(string relativePath)
  {
    string full = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
    Directory.CreateDirectory(full);
    return full;
  }

  public void Dispose()
  {
    try
    {
      Directory.Delete(Path, true);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
  }

}
