using CodexQuotaFloat.Models;

namespace CodexQuotaFloat.Services;

public enum StartupPresentation { Exit, SetupWizard, FloatingWindow }

public static class StartupFlow
{
    public static StartupPresentation InitialPresentation(bool shutdownRequested, bool setupCompleted) => shutdownRequested ? StartupPresentation.Exit : setupCompleted ? StartupPresentation.FloatingWindow : StartupPresentation.SetupWizard;
    public static bool StartMonitorAfterWizard(SetupCheckResult result) => result.IsReady;
    public static bool NeedsVisibleFallback(bool wizardVisible, bool floatingVisible, bool trayInitialized) => !wizardVisible && !floatingVisible && !trayInitialized;
}
