using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace ProjectCostForecast.App.Services;

/// <summary>
/// Coordinates revision-aware project writes for cooperating instances on the
/// same Windows interactive session. The name is derived from the canonical
/// destination path, so the operating system owns the lifetime of the lock
/// rather than this process retaining a path-keyed dictionary.
/// </summary>
internal static class ProjectFileWriteLock
{
    private const string MutexNamePrefix = "Local\\ProjectCostForecast.ProjectFileWrite.";

    public static ProjectFileWriteLockLease Acquire(string path)
    {
        var fullPath = CanonicalizePath(path);
        var mutexName = BuildMutexName(fullPath);
        Mutex? mutex = null;
        var ownsMutex = false;

        try
        {
            mutex = new Mutex(initiallyOwned: false, mutexName);
            var wasAbandoned = false;
            try
            {
                ownsMutex = mutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                // WaitOne grants ownership when the previous owner abandoned
                // the mutex. Atomic replacement means the next writer can
                // safely recover the boundary and continue.
                ownsMutex = true;
                wasAbandoned = true;
            }

            if (!ownsMutex)
            {
                throw new ProjectFileWriteLockException(
                    fullPath,
                    mutexName,
                    "The project write boundary returned without ownership.");
            }

            return new ProjectFileWriteLockLease(fullPath, mutexName, mutex, wasAbandoned);
        }
        catch (ProjectFileWriteLockException)
        {
            ReleaseAfterFailedAcquire(mutex, ownsMutex);
            throw;
        }
        catch (Exception ex) when (IsLockFailure(ex))
        {
            ReleaseAfterFailedAcquire(mutex, ownsMutex);
            throw new ProjectFileWriteLockException(
                fullPath,
                mutexName,
                "The project write boundary could not be acquired.",
                ex);
        }
    }

    internal static string CanonicalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        return OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;
    }

    private static string BuildMutexName(string canonicalFullPath)
    {
        var pathHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalFullPath)));
        return MutexNamePrefix + pathHash;
    }

    private static bool IsLockFailure(Exception exception)
    {
        return exception is UnauthorizedAccessException
            or IOException
            or ArgumentException
            or InvalidOperationException
            or PlatformNotSupportedException
            or SecurityException
            or ThreadInterruptedException;
    }

    private static void ReleaseAfterFailedAcquire(Mutex? mutex, bool ownsMutex)
    {
        if (mutex is null)
        {
            return;
        }

        try
        {
            if (ownsMutex)
            {
                mutex.ReleaseMutex();
            }
        }
        finally
        {
            mutex.Dispose();
        }
    }
}

internal sealed class ProjectFileWriteLockLease : IDisposable
{
    private Mutex? _mutex;

    internal ProjectFileWriteLockLease(
        string fullPath,
        string mutexName,
        Mutex mutex,
        bool wasAbandoned)
    {
        FullPath = fullPath;
        MutexName = mutexName;
        _mutex = mutex;
        WasAbandoned = wasAbandoned;
    }

    internal string FullPath { get; }

    internal string MutexName { get; }

    internal bool WasAbandoned { get; }

    public void Dispose()
    {
        var mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex is null)
        {
            return;
        }

        try
        {
            mutex.ReleaseMutex();
        }
        catch (Exception ex) when (ex is ApplicationException or UnauthorizedAccessException)
        {
            throw new ProjectFileWriteLockException(
                FullPath,
                MutexName,
                "The project write boundary could not be released.",
                ex);
        }
        finally
        {
            mutex.Dispose();
        }
    }
}

/// <summary>
/// Indicates that a project write boundary could not be acquired or released.
/// A recovered abandoned mutex is reported on the internal lease and is not
/// converted into this exception because ownership has been recovered.
/// </summary>
public sealed class ProjectFileWriteLockException : IOException
{
    internal ProjectFileWriteLockException(
        string fullPath,
        string mutexName,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FullPath = fullPath;
        MutexName = mutexName;
    }

    public string FullPath { get; }

    public string MutexName { get; }
}

/// <summary>
/// Internal, instance-scoped test seam for deterministic writer interleavings.
/// Production uses the no-op implementation; no mutable global hook is used.
/// </summary>
internal interface IProjectFileWriteInterleaving
{
    void BeforeWriterLock(string fullPath);

    void AfterExpectedRevisionCheck(string fullPath);
}

internal sealed class NoOpProjectFileWriteInterleaving : IProjectFileWriteInterleaving
{
    public static NoOpProjectFileWriteInterleaving Instance { get; } = new();

    private NoOpProjectFileWriteInterleaving()
    {
    }

    public void BeforeWriterLock(string fullPath)
    {
    }

    public void AfterExpectedRevisionCheck(string fullPath)
    {
    }
}
