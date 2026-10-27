using System.Text;
using PowerPlanTray;

internal static class CodexLogTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Reproduce a background poll holding the gate while the menu asks for
        // its status. The status read must finish before that poll is released.
        using (var guard = new CodexStandbyGuard())
        using (var entered = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        {
            var gate = typeof(CodexStandbyGuard).GetField("_gate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(guard)!;
            var poll = Task.Run(() => { lock (gate) { entered.Set(); release.Wait(); } });
            try
            {
                entered.Wait();
                var status = Task.Run(() => guard.Status);
                check(status.Wait(TimeSpan.FromSeconds(2)), "Tray status remains readable while a background poll holds its lock");
            }
            finally { release.Set(); poll.GetAwaiter().GetResult(); }
        }
        var directory = Path.Combine(Path.GetTempPath(), "PowerPlanTray-log-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "session.jsonl");
            var buffer = new byte[64 * 1024];
            var log = new CodexSessionLog();
            string Event(string kind, DateTimeOffset time) =>
                $"{{\"timestamp\":\"{time:O}\",\"type\":\"event_msg\",\"payload\":{{\"type\":\"{kind}\"}}}}\n";
            var old = DateTimeOffset.UtcNow.AddDays(-5);
            File.WriteAllText(path, Event("task_started", old) + Event("task_complete", old), new UTF8Encoding(false));
            log.Read(path, buffer);
            check(!log.Active && log.GraceStart == old, "Historical completion retains its original timestamp");
            check(log.Read(path, buffer) == 0, "An unchanged stale log is not reparsed");
            var position = log.Position;
            File.AppendAllText(path, Event("task_started", DateTimeOffset.UtcNow));
            check(log.Read(path, buffer) == new FileInfo(path).Length - position && log.Active,
                "Resumed old sessions read only appended bytes and become active");
            var complete = Event("task_complete", DateTimeOffset.UtcNow);
            File.AppendAllText(path, complete[..^1]);
            log.Read(path, buffer);
            check(log.Active, "A partial completion record cannot end a task prematurely");
            File.AppendAllText(path, "\n");
            log.Read(path, buffer);
            check(!log.Active && log.GraceStart.HasValue, "A completed partial line starts the grace period");
            File.AppendAllText(path, "[]\n{\"type\":42}\n{\"type\":\"event_msg\",\"payload\":null}\ninvalid\n" +
                Event("task_started", DateTimeOffset.UtcNow));
            log.Read(path, buffer);
            check(log.Active, "Malformed and unsupported records do not stop subsequent event processing");
            File.WriteAllText(path, "{}\n", new UTF8Encoding(false));
            log.Read(path, buffer);
            check(!log.Active && log.GraceStart is null && log.Position == 3, "Truncation resets the previous task state and byte offset");
            File.WriteAllText(path, new string('x', 3 * 1024 * 1024) + "\n" + Event("task_started", DateTimeOffset.UtcNow));
            var firstRead = log.Read(path, buffer);
            check(firstRead <= 256 * 1024 && log.HasMore && log.Position < new FileInfo(path).Length,
                "A large existing log cannot monopolize a single polling pass");
            while (log.Position < new FileInfo(path).Length) log.Read(path, buffer);
            check(log.Active && !log.HasMore, "Oversized records are skipped and the following valid event is preserved");
            check(log.Read(path, buffer) == 0, "A fully consumed large log stays at EOF on subsequent polls");
            File.AppendAllText(path, Event("turn_aborted", DateTimeOffset.UtcNow));
            log.Read(path, buffer);
            check(!log.Active && log.GraceStart.HasValue, "An aborted turn ends activity and starts grace");

            var started = DateTimeOffset.UtcNow.AddMinutes(-6);
            File.AppendAllText(path, Event("task_started", started));
            log.Read(path, buffer);
            check(!log.IsActive(DateTimeOffset.UtcNow), "An orphaned start stops blocking standby after five minutes without work");
            File.AppendAllText(path, Event("thread_settings_applied", DateTimeOffset.UtcNow));
            log.Read(path, buffer);
            check(!log.IsActive(DateTimeOffset.UtcNow) && log.LastActivity == started,
                "Settings changes cannot renew an orphaned task");
            File.AppendAllText(path, Event("item_completed", DateTimeOffset.UtcNow));
            log.Read(path, buffer);
            check(log.IsActive(DateTimeOffset.UtcNow), "Actual task progress renews activity even after a silent interval");
            File.AppendAllText(path, Event("task_complete", DateTimeOffset.UtcNow));
            log.Read(path, buffer);
            check(!log.IsActive(DateTimeOffset.UtcNow) && log.GraceStart.HasValue,
                "Normal completion still transitions to the configured grace period");

            File.AppendAllText(path, Event("task_started", DateTimeOffset.UtcNow) + new string(' ', 300 * 1024) + "\n" +
                Event("task_complete", DateTimeOffset.UtcNow));
            log.Read(path, buffer);
            check(log.HasMore && !log.IsActive(DateTimeOffset.UtcNow), "Historical starts cannot block while the reader is still catching up to a completion");
        }
        finally
        {
            File.Delete(Path.Combine(directory, "session.jsonl"));
            Directory.Delete(directory);
        }
    }
}
