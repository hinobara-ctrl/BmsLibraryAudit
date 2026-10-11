using System.Security.Cryptography;
using System.Text;

namespace BmsLibraryAudit.Core;

internal sealed class PhaseTwoWriter : IDisposable
{
    private readonly Mutex mutex;
    private PhaseTwoWriter(Mutex mutex) => this.mutex = mutex;
    internal static PhaseTwoWriter Acquire(string path)
    {
        var resolved = PhaseTwoSafety.Resolve(path);
        var name = "Global\\BmsLibraryAudit.Phase2." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resolved)));
        var mutex = new Mutex(false, name);
        try
        {
            try { if (!mutex.WaitOne(0)) throw new InvalidOperationException("Another Phase 2 database writer is active."); }
            catch (AbandonedMutexException) { /* Exclusion acquired; interrupted rows are handled explicitly. */ }
            return new(mutex);
        }
        catch { mutex.Dispose(); throw; }
    }
    public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
}
