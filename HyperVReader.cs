using System.Management;

namespace PowerPlanTray;

internal sealed record HyperVMachine(string Id, string Name, bool? Running)
{
    public override string ToString() => $"{Name} · {(Running is null ? "Status nicht geladen" : Running.Value ? "Läuft" : "Nicht gestartet")}";
}
internal sealed record HyperVSnapshot(IReadOnlyList<HyperVMachine>? Machines, string? Error);

internal static class HyperVReader
{
    private static Task<HyperVSnapshot>? _pending;
    // Called on the UI timer. Query off-thread and consume each result only once;
    // an unavailable snapshot must never be mistaken for all VMs stopping.
    public static HyperVSnapshot? Poll()
    {
        if (_pending is null) { _pending = Task.Run(Read); return null; }
        if (!_pending.IsCompleted) return null;
        var snapshot = _pending.GetAwaiter().GetResult();
        _pending = Task.Run(Read);
        return snapshot;
    }

    public static HyperVSnapshot Read()
    {
        try
        {
            var options = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(4), ReturnImmediately = true };
            using var searcher = new ManagementObjectSearcher(new ManagementScope(@"\\.\root\virtualization\v2"),
                new ObjectQuery("SELECT Name, ElementName, EnabledState FROM Msvm_ComputerSystem"), options);
            using var results = searcher.Get();
            var machines = new List<HyperVMachine>();
            foreach (ManagementObject vm in results)
                using (vm)
                {
                    var id = Convert.ToString(vm["Name"]) ?? "";
                    if (!Guid.TryParse(id, out _)) continue; // Exclude the host computer.
                    machines.Add(new(id, Convert.ToString(vm["ElementName"]) ?? id, Convert.ToUInt16(vm["EnabledState"]) == 2));
                }
            return new(machines.OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), null);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            return new(null, "Hyper-V nicht erreichbar. Ist Hyper-V installiert und Ihr Konto für die Hyper-V-Verwaltung berechtigt? (Hyper-V-Administratoren)");
        }
    }
}
