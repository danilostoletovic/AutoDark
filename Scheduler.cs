using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using System.Text;

namespace AutoDark;

internal static class Scheduler
{
    internal static string UserSid => WindowsIdentity.GetCurrent().User?.Value
        ?? throw new InvalidOperationException("Cannot identify the current Windows user.");
    internal static string TaskName => "AutoDark-" + UserSid;
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private static XElement E(string name, object? content) => new(Ns + name, content);

    internal static string Definition(string sid, string executable, DateTimeOffset next, DateTimeOffset now)
    {
        string boundary = next.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        var triggers = E("Triggers", new object[]
        {
            E("TimeTrigger", new object[] { E("StartBoundary", boundary), E("Enabled", true) }),
            E("CalendarTrigger", new object[] { E("StartBoundary", now.UtcDateTime.Date.AddDays(1).AddHours(12).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)), E("Enabled", true), E("ScheduleByDay", E("DaysInterval", 1)) }),
            E("LogonTrigger", new object[] { E("Enabled", true), E("Delay", "PT10S"), E("UserId", sid) }),
            E("SessionStateChangeTrigger", new object[] { E("Enabled", true), E("UserId", sid), E("StateChange", "SessionUnlock") }),
            E("EventTrigger", new object[] { E("Enabled", true), E("Subscription", "<QueryList><Query Id=\"0\" Path=\"System\"><Select Path=\"System\">*[System[Provider[@Name='Microsoft-Windows-Power-Troubleshooter'] and EventID=1]]</Select><Select Path=\"System\">*[System[Provider[@Name='Microsoft-Windows-Kernel-General'] and (EventID=1 or EventID=22)]]</Select></Query></QueryList>"), E("Delay", "PT10S") })
        });
        return new XDocument(new XElement(Ns + "Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Description", "AutoDark local sunrise/sunset theme switching for this user.")),
            triggers,
            E("Principals", new XElement(Ns + "Principal", new XAttribute("id", "CurrentUser"),
                E("UserId", sid), E("LogonType", "InteractiveToken"), E("RunLevel", "LeastPrivilege"))),
            E("Settings", new object[] { E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", false),
                E("StopIfGoingOnBatteries", false), E("StartWhenAvailable", true), E("RunOnlyIfNetworkAvailable", false),
                E("ExecutionTimeLimit", "PT2M"), E("Enabled", true), E("WakeToRun", false),
                E("RestartOnFailure", new object[] { E("Interval", "PT1M"), E("Count", 3) }) }),
            new XElement(Ns + "Actions", new XAttribute("Context", "CurrentUser"),
                E("Exec", new object[] { E("Command", executable), E("Arguments", "--scheduled"), E("WorkingDirectory", Path.GetDirectoryName(executable)) })))).ToString();
    }

    internal static void Register(DateTimeOffset next)
    {
        string executable = ExecutionPath();
        WithFolder(folder =>
        {
            object task = folder.RegisterTask(TaskName, Definition(UserSid, executable, next, DateTimeOffset.UtcNow), 6, UserSid, null, 3, null);
            try
            {
                if (!(bool)((dynamic)task).Enabled) throw new IOException("AutoDark task was registered but is disabled.");
            }
            finally { Marshal.FinalReleaseComObject(task); }
        });
    }

    internal static string ExecutionPath()
    {
        uint length = 0;
        int result = GetCurrentPackageFamilyName(ref length, null);
        if (result == 15700) // APPMODEL_ERROR_NO_PACKAGE: portable behavior stays unchanged.
        {
            string path = Environment.ProcessPath ?? throw new IOException("Cannot locate AutoDark.exe.");
            if (!Path.GetFileName(path).Equals("AutoDark.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run AutoDark.exe directly to enable scheduling.");
            return path;
        }
        if (result != 122) throw new System.ComponentModel.Win32Exception(result);
        var family = new StringBuilder((int)length);
        result = GetCurrentPackageFamilyName(ref length, family);
        if (result != 0) throw new System.ComponentModel.Win32Exception(result);
        string alias = PackagedExecutionPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), family.ToString());
        if (!File.Exists(alias))
            throw new IOException("AutoDark's Windows app execution alias is unavailable. Enable AutoDark.Store.exe in Windows Settings > Apps > Advanced app settings > App execution aliases, then try again.");
        return alias;
    }

    internal static string PackagedExecutionPath(string localAppData, string family) =>
        Path.Combine(localAppData, "Microsoft", "WindowsApps", family, "AutoDark.Store.exe");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref uint length, StringBuilder? family);

    internal static void ValidateDefinition() => WithFolder(folder =>
    {
        // TASK_VALIDATE_ONLY checks the native schema without creating or changing a task.
        object? result = folder.RegisterTask(TaskName, Definition(UserSid, Environment.ProcessPath!, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow), 1, UserSid, null, 3, null);
        if (result != null) Marshal.FinalReleaseComObject(result);
    });

    internal static bool Exists()
    {
        bool found = false;
        WithFolder(folder =>
        {
            object? task = null;
            try { task = folder.GetTask(TaskName); found = (bool)((dynamic)task).Enabled; }
            catch (Exception ex) when ((uint)ex.HResult == 0x80070002) { }
            finally { if (task != null) Marshal.FinalReleaseComObject(task); }
        });
        return found;
    }

    internal static void Remove() => WithFolder(folder =>
    {
        try { folder.DeleteTask(TaskName, 0); }
        catch (Exception ex) when ((uint)ex.HResult == 0x80070002) { }
    });

    private static void WithFolder(Action<dynamic> action)
    {
        object service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Windows Task Scheduler is unavailable."))!;
        object? folder = null;
        try
        {
            ((dynamic)service).Connect();
            folder = ((dynamic)service).GetFolder(@"\");
            action(folder);
        }
        finally
        {
            if (folder != null) Marshal.FinalReleaseComObject(folder);
            Marshal.FinalReleaseComObject(service);
        }
    }
}


