namespace VanDao.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherForm(LauncherUpdateRequest.FromArgs(args)));
    }
}
