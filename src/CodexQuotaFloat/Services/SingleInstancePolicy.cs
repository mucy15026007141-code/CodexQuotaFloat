namespace CodexQuotaFloat.Services;

public static class SingleInstancePolicy
{
    public static bool ShouldContinueStartup(bool ownsMutex) => ownsMutex;
    public static string EventForArguments(IEnumerable<string> arguments) => arguments.Contains("--shutdown", StringComparer.OrdinalIgnoreCase) ? "shutdown" : arguments.Contains("--reset-window", StringComparer.OrdinalIgnoreCase) ? "reset" : "show";
}
