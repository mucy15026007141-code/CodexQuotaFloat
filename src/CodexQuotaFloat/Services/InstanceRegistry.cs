using System.Diagnostics;
using System.Text.Json;

namespace CodexQuotaFloat.Services;

public sealed record InstanceMetadata(int ProcessId, DateTimeOffset ProcessStartTimeUtc, int SessionId);

public static class InstanceRegistry
{
    private static readonly string FilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuotaFloat", "instance.json");
    public static void WriteCurrent()
    {
        try { using var p = Process.GetCurrentProcess(); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!); System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(new InstanceMetadata(p.Id, p.StartTime.ToUniversalTime(), p.SessionId))); }
        catch (Exception ex) { BootstrapLog.Write("INSTANCE_METADATA_WRITE_FAILED", ex.GetType().Name); }
    }
    public static InstanceMetadata? ReadLive()
    {
        try { var m = JsonSerializer.Deserialize<InstanceMetadata>(System.IO.File.ReadAllText(FilePath)); using var p = Process.GetProcessById(m!.ProcessId); if (p.SessionId == m.SessionId && Math.Abs((p.StartTime.ToUniversalTime() - m.ProcessStartTimeUtc).TotalSeconds) < 1) return m; }
        catch { }
        Clear(); BootstrapLog.Write("STALE_INSTANCE_METADATA"); return null;
    }
    public static bool WaitForExit(InstanceMetadata m, TimeSpan timeout) { try { using var p = Process.GetProcessById(m.ProcessId); return p.WaitForExit((int)timeout.TotalMilliseconds); } catch (ArgumentException) { return true; } }
    public static void Clear() { try { if (System.IO.File.Exists(FilePath)) System.IO.File.Delete(FilePath); } catch { } }
    public static bool MetadataMatches(InstanceMetadata? metadata, int processId, DateTimeOffset startTimeUtc, int sessionId)
        => metadata is not null && metadata.ProcessId == processId && metadata.SessionId == sessionId && Math.Abs((metadata.ProcessStartTimeUtc - startTimeUtc).TotalSeconds) < 1;

    public static bool TryDeleteCurrent()
    {
        BootstrapLog.Write("INSTANCE_METADATA_DELETE_BEGIN");
        try
        {
            if (!System.IO.File.Exists(FilePath)) { BootstrapLog.Write("INSTANCE_METADATA_DELETE_END", "missing"); return true; }
            var metadata = JsonSerializer.Deserialize<InstanceMetadata>(System.IO.File.ReadAllText(FilePath));
            using var process = Process.GetCurrentProcess();
            if (!MetadataMatches(metadata, process.Id, process.StartTime.ToUniversalTime(), process.SessionId))
            {
                BootstrapLog.Write("INSTANCE_METADATA_DELETE_FAILED", "OwnershipMismatch");
                return false;
            }
            System.IO.File.Delete(FilePath);
            BootstrapLog.Write("INSTANCE_METADATA_DELETE_END");
            return true;
        }
        catch (Exception ex)
        {
            BootstrapLog.Write("INSTANCE_METADATA_DELETE_FAILED", ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }
}
