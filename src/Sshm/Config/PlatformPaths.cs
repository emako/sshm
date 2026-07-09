namespace Sshm.Config;

public static class PlatformPaths
{
    public static string GetHomeDir()
    {
        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
        {
            return home;
        }

        home = Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrEmpty(home))
        {
            return home;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public static string GetDefaultSSHConfigPath()
    {
        string homeDir = GetHomeDir();
        return Path.Combine(homeDir, ".ssh", "config");
    }

    public static string GetSSHMConfigDir()
    {
        string homeDir = GetHomeDir();

        if (OperatingSystem.IsWindows())
        {
            string? appData = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrEmpty(appData))
            {
                return Path.Combine(appData, "sshm");
            }

            return Path.Combine(homeDir, ".config", "sshm");
        }

        string? xdgConfigDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfigDir))
        {
            return Path.Combine(xdgConfigDir, "sshm");
        }

        return Path.Combine(homeDir, ".config", "sshm");
    }

    public static string GetSSHMBackupDir()
    {
        return Path.Combine(GetSSHMConfigDir(), "backups");
    }

    public static string GetSSHDirectory()
    {
        return Path.Combine(GetHomeDir(), ".ssh");
    }

    public static string GetAppConfigPath()
    {
        return Path.Combine(GetSSHMConfigDir(), "config.json");
    }
}
