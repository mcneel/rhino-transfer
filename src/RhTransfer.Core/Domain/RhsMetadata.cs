using System;

namespace RhTransfer.Core.Domain;

/// <summary>
/// Provenance for an archive, stored in the zip's comment rather than as a file. Rhino 9
/// imports a .rhs by extracting every entry straight into the data folder, so a metadata file
/// would land there as litter; a comment is invisible to it.
/// </summary>
public sealed record RhsMetadata(string Tool, string ToolVersion, int SourceMajor, string SourceOs, DateTimeOffset CreatedOn)
{
  public const string ToolName = "rh-transfer";
}
