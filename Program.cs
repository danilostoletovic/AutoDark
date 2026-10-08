namespace AutoDark;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Branch before WinForms initialization: scheduled runs never create windows or message loops.
        if (args.Length != 0)
        {
            try
            {
                if (args is ["--self-test"]) return SelfTests.Run();
                if (args is ["--validate-task"]) { Scheduler.ValidateDefinition(); Console.WriteLine($"Native task XML valid; enabled task exists: {Scheduler.Exists()}"); return 0; }
                if (args is ["--render-ui", var directory]) { SelfTests.RenderForms(directory); return 0; }
                if (args is ["--scheduled"]) return Automation.Scheduled();
                return 2;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Console.Error.WriteLine(ex);
                try
                {
                    Directory.CreateDirectory(Preferences.DirectoryPath);
                    File.WriteAllText(Path.Combine(Preferences.DirectoryPath, "last-error.txt"), $"{DateTimeOffset.Now:O}\n{ex}\n");
                }
                catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
                return 1;
            }
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
        return 0;
    }
}
