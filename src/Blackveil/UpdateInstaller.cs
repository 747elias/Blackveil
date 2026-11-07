using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace Blackveil;

internal sealed record UpdatePlan(int ParentId, long ParentStartTicks, string Target, string Package, string Hash, bool Installed, string Version, bool Autostart);
internal sealed record UpdateResult(bool Success, string Message, string? Version = null)
{ public UpdateResult() : this(false, "") { } }
internal static class UpdateInstaller
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{3E29D02D-B2C1-44D7-8E90-C53380C6D534}_is1";
    private static bool MatchesInstall(RegistryHive hive, string exe)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(UninstallKey);
            if (key?.GetValue("InstallLocation") is string directory &&
                Path.GetFullPath(directory).TrimEnd('\\', '/').Equals(Path.GetDirectoryName(Path.GetFullPath(exe)), StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
    internal static bool IsMachineInstalled(string exe) => MatchesInstall(RegistryHive.LocalMachine, exe);
    internal static bool IsInstalled(string exe) => MatchesInstall(RegistryHive.CurrentUser, exe) || IsMachineInstalled(exe);
    internal static void LaunchHelper(string directory, string package, string hash, bool installed, Version version)
    {
        string target = Environment.ProcessPath!;
        if (!Path.GetFileName(target).Equals("Blackveil.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Updates require the published Blackveil.exe.");
        if ((File.GetAttributes(target) & FileAttributes.ReadOnly) != 0) throw new IOException("Blackveil.exe is read-only.");
        // Verify write access before shutting down the working instance.
        bool machine = IsMachineInstalled(target);
        string probe = Path.Combine(Path.GetDirectoryName(target)!, Guid.NewGuid() + ".tmp");
        if (!machine) { File.WriteAllText(probe, ""); File.Delete(probe); }
        string helper = Path.Combine(directory, "Blackveil-Updater.exe");
        File.Copy(target, helper, true);
        using var current = Process.GetCurrentProcess();
        var plan = new UpdatePlan(current.Id, current.StartTime.ToUniversalTime().Ticks, target, package, hash, installed, version.ToString(), Startup.Enabled);
        string path = Path.Combine(directory, "plan.json");
        File.WriteAllText(path, JsonSerializer.Serialize(plan));
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(path);
        _ = Process.Start(start) ?? throw new IOException("The update helper could not be started.");
    }
    internal static int Run(string planFile)
    {
        UpdatePlan? plan = null;
        string? backup = null;
        bool parentExited = false, machine = false;
        var storage = new UpdateStorage();
        try
        {
            string root = Path.GetFullPath(Path.Combine(storage.Root, "updates")) + Path.DirectorySeparatorChar;
            string fullPlan = Path.GetFullPath(planFile);
            if (!fullPlan.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetDirectoryName(fullPlan), Path.GetDirectoryName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid update request.");
            plan = JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(fullPlan)) ?? throw new InvalidDataException("Missing update request.");
            if (!Path.IsPathFullyQualified(plan.Target) || !Path.GetFileName(plan.Target).Equals("Blackveil.exe", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetDirectoryName(Path.GetFullPath(plan.Package)), Path.GetDirectoryName(fullPlan), StringComparison.OrdinalIgnoreCase) || !Version.TryParse(plan.Version, out _))
                throw new InvalidDataException("Invalid update paths.");
            plan = plan with { Target = Path.GetFullPath(plan.Target), Package = Path.GetFullPath(plan.Package) };
            try
            {
                using var parent = Process.GetProcessById(plan.ParentId);
                if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStartTicks ||
                    !string.Equals(parent.MainModule?.FileName, plan.Target, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update parent process does not match.");
                if (!parent.WaitForExit(60000)) throw new TimeoutException("Blackveil did not exit in time.");
            }
            catch (ArgumentException) { /* Already exited before helper initialization. */ }
            parentExited = true;
            ReleaseClient.VerifyAsync(plan.Package, plan.Hash, CancellationToken.None).GetAwaiter().GetResult();
            machine = IsMachineInstalled(plan.Target);
            backup = machine ? Path.Combine(Path.GetDirectoryName(fullPlan)!, "previous.exe") : plan.Target + ".update-backup";
            File.Copy(plan.Target, backup, true);
            if (plan.Installed)
            {
                var installer = new ProcessStartInfo(plan.Package) { UseShellExecute = machine, CreateNoWindow = !machine };
                if (machine) installer.Verb = "runas";
                foreach (string argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-", "/CLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS",
                    (machine ? "/ALLUSERS" : "/CURRENTUSER"), "/DIR=" + Path.GetDirectoryName(plan.Target), "/TASKS=" + (plan.Autostart ? "autostart" : ""),
                    "/LOG=" + Path.Combine(Path.GetDirectoryName(fullPlan)!, "installer.log") }) installer.ArgumentList.Add(argument);
                using var process = Process.Start(installer) ?? throw new IOException("The installer could not be started.");
                // Wait until completion: never restore files while an installer is still writing.
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException($"The installer returned error {process.ExitCode}.");
            }
            else
            {
                string incoming = plan.Target + ".update-new";
                File.Copy(plan.Package, incoming, true);
                File.Move(incoming, plan.Target, true);
            }
            var actual = FileVersionInfo.GetVersionInfo(plan.Target).FileVersion;
            if (actual != plan.Version + ".0") throw new InvalidDataException("The installed version does not match the update.");
            storage.Save("update-result.json", new UpdateResult(true, "Update to " + plan.Version + " installed successfully.", plan.Version));
            Restart(plan.Target);
            try { File.Delete(backup); } catch (IOException) { }
            return 0;
        }
        catch (Exception error)
        {
            Log.Write("Update installation failed", error);
            if (plan is not null && parentExited)
            {
                try
                {
                    if (!machine && backup is not null && File.Exists(backup)) File.Copy(backup, plan.Target, true);
                    storage.Save("update-result.json", new UpdateResult(false, (machine ? "The system-wide update did not finish. Use Setup to repair the installation if needed. " : "The update failed; the previous executable was kept or restored. ") + error.Message, plan.Version));
                    Restart(plan.Target);
                }
                catch (Exception recovery) { Log.Write("Update recovery failed", recovery); MessageBox.Show("Blackveil could not restart. Please start the app manually. " + error.Message, "Blackveil – Update"); }
            }
            return 1;
        }
    }
    private static void Restart(string target)
    {
        var start = new ProcessStartInfo(target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target)! };
        start.ArgumentList.Add("--after-update");
        _ = Process.Start(start) ?? throw new IOException("Restart failed.");
    }
}
