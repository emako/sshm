using Sshm.Config;
using Sshm.Core;
using Sshm.Core.Models;
using Sshm.UI;

namespace Sshm;

internal static class Program
{
    private const string AppVersion = "1.0.0-dev";

    public static int Main(string[] args)
    {
        ConsoleEncoding.EnsureUtf8();

        string configFile = string.Empty;
        bool searchMode = false;
        bool noUpdateCheck = false;

        List<string> positional = [];
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--version":
                case "-V":
                    Console.WriteLine($"sshm version {AppVersion}");
                    return 0;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
                case "--config":
                case "-c":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Error: --config requires a path argument.");
                        return 1;
                    }

                    configFile = args[++i];
                    break;
                case "--search":
                case "-s":
                    searchMode = true;
                    break;
                case "--no-update-check":
                    noUpdateCheck = true;
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        Console.Error.WriteLine($"Error: unknown flag '{arg}'");
                        return 1;
                    }

                    positional.Add(arg);
                    break;
            }
        }

        if (positional.Count > 0)
        {
            return ConnectToHost(positional[0], configFile);
        }

        return RunInteractive(configFile, searchMode, noUpdateCheck);
    }

    private static int RunInteractive(string configFile, bool searchMode, bool noUpdateCheck)
    {
        List<SshHost> hosts;
        try
        {
            hosts = string.IsNullOrEmpty(configFile)
                ? SshConfigParser.ParseSSHConfig()
                : SshConfigParser.ParseSSHConfigFile(configFile);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading SSH config file: {ex.Message}");
            return 1;
        }

        if (hosts.Count == 0)
        {
            Console.WriteLine("No SSH hosts found in your ~/.ssh/config file.");
            Console.Write("Would you like to add a new host now? [y/N]: ");
            string? response = Console.ReadLine();
            if (response is "y" or "Y")
            {
                Console.WriteLine("Please add hosts using the TUI (press 'a') after creating at least one host manually, or run with a populated config.");
            }

            Console.WriteLine("No hosts available, exiting.");
            return 1;
        }

        try
        {
            TuiApplication.RunInteractiveMode(hosts, configFile, searchMode, AppVersion, noUpdateCheck);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error running TUI: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static int ConnectToHost(string hostName, string configFile)
    {
        bool exists = string.IsNullOrEmpty(configFile)
            ? SshConfigQuery.QuickHostExists(hostName)
            : SshConfigQuery.QuickHostExistsInFile(hostName, configFile);

        if (!exists)
        {
            Console.Error.WriteLine($"Error: Host '{hostName}' not found in SSH configuration.");
            Console.Error.WriteLine("Use 'sshm' to see available hosts.");
            return 1;
        }

        try
        {
            History.HistoryManager.Create().RecordConnection(hostName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Could not record connection history: {ex.Message}");
        }

        List<string> sshArgs = [];
        if (!string.IsNullOrEmpty(configFile))
        {
            sshArgs.Add("-F");
            sshArgs.Add(configFile);
        }

        sshArgs.Add(hostName);
        Console.WriteLine($"Connecting to {hostName}...");

        System.Diagnostics.ProcessStartInfo psi = new()
        {
            FileName = "ssh",
            UseShellExecute = false,
        };
        foreach (string arg in sshArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ssh process");
        process.WaitForExit();
        return process.ExitCode;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("SSHM - SSH Manager (.NET)");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  sshm                           Open interactive TUI");
        Console.WriteLine("  sshm <host>                    Connect to host");
        Console.WriteLine("  sshm -c <config>               Use custom SSH config");
        Console.WriteLine("  sshm -s                        Focus search at startup");
        Console.WriteLine("  sshm --no-update-check         Disable update check");
        Console.WriteLine("  sshm --version                 Show version");
    }
}
