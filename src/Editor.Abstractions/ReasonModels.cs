// WpDI/src/Editor.Abstractions/ReasonModels.cs
namespace Editor.Abstractions;

public enum ReasonCode
{
    Unknown = 0,
    NotFound,
    Trashed,
    Conflict
}

public sealed record ReasonArgs(string Kind, long Id);

