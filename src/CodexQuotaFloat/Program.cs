using CodexQuotaFloat.Services;

namespace CodexQuotaFloat;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        BootstrapLog.Write("PROCESS_ENTRY");
        var app = new App();
        BootstrapLog.Write("APP_CREATE_END");
        app.InitializeComponent();
        BootstrapLog.Write("APP_RUN_BEGIN");
        app.Run();
        BootstrapLog.Write("PROCESS_EXIT");
    }
}
