using System.IO;
using System.Security.Cryptography;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// Identifies the exact bytes that were read from a project file. The token is
/// deliberately content-based so a timestamp-resolution race cannot make an
/// unchanged file look newer or allow a changed file to be silently replaced.
/// </summary>
public sealed record ProjectFileRevision(string FullPath, long Length, string ContentHash)
{
    public static ProjectFileRevision Capture(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        return FromBytes(fullPath, File.ReadAllBytes(fullPath));
    }

    internal static ProjectFileRevision FromBytes(string fullPath, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return new ProjectFileRevision(
            fullPath,
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)));
    }

    public bool Matches(ProjectFileRevision? actual)
    {
        return actual is not null
            && string.Equals(FullPath, actual.FullPath, StringComparison.OrdinalIgnoreCase)
            && Length == actual.Length
            && string.Equals(ContentHash, actual.ContentHash, StringComparison.Ordinal);
    }
}

public sealed record ProjectFileLoadResult(ProjectDataset Dataset, ProjectFileRevision? Revision);

public enum SaveConflictDecision
{
    Reload,
    SaveAs,
    Cancel
}

public sealed record ProjectSaveConflict(
    string Path,
    string Operation,
    ProjectFileRevision? ExpectedRevision,
    ProjectFileRevision? ActualRevision);

public sealed class ProjectFileConflictException : IOException
{
    public ProjectFileConflictException(
        string path,
        string operation,
        ProjectFileRevision? expectedRevision,
        ProjectFileRevision? actualRevision)
        : base(BuildMessage(path, operation, actualRevision is null))
    {
        Path = path;
        Operation = operation;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public string Path { get; }

    public string Operation { get; }

    public ProjectFileRevision? ExpectedRevision { get; }

    public ProjectFileRevision? ActualRevision { get; }

    public ProjectSaveConflict Conflict => new(Path, Operation, ExpectedRevision, ActualRevision);

    private static string BuildMessage(string path, string operation, bool fileMissing)
    {
        var changeDescription = fileMissing
            ? "was removed or is no longer readable"
            : "was changed outside this session";

        return $"{operation} was not written because the project file '{path}' {changeDescription}. "
            + "Choose Reload to use the newer file, Save As to preserve these changes in another file, or Cancel to keep working.";
    }
}
