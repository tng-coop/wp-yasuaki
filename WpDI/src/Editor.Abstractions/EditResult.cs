// WpDI/src/Editor.Abstractions/EditResult.cs
namespace Editor.Abstractions;

/// <summary>
/// Result of a post create/update/status operation coming back from WPDI.
/// </summary>
public readonly record struct EditResult(long Id, string Link, string Status);
