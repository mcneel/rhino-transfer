namespace RhTransfer.Core.Domain;

/// <summary>
/// One &lt;file_name&gt; entry inside a window layout file: a toolbar file the layout depends on.
/// <see cref="Path"/> is verbatim as it appeared in the XML, which is absolute on a live
/// machine and archive-relative inside a .rhs.
/// </summary>
public readonly record struct RuiReference(string Path, RuiLocation Location);
