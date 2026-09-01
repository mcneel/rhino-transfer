using System.Text.Json.Serialization;

using RhTransfer.Core.Domain;

namespace RhTransfer.Core.Infrastructure;

/// <summary>
/// Source generated serialization for the archive's provenance. Reflection based
/// System.Text.Json cannot survive trimming or ahead of time compilation, and this is the only
/// thing in the tool that serializes anything.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(RhsMetadata))]
internal sealed partial class RhsMetadataContext : JsonSerializerContext;
