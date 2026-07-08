using System.Net;
using System.Text.RegularExpressions;

namespace Sshm.Validation;

public static partial class SshHostValidator
{
    [GeneratedRegex(@"%[hprunCdiklLT]")]
    private static partial Regex SshTokenRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9]|[a-zA-Z0-9]{0,62})?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9]|[a-zA-Z0-9]{0,62})?)*$")]
    private static partial Regex HostnameRegex();

    public static bool ValidateHostname(string hostname)
    {
        if (string.IsNullOrEmpty(hostname) || hostname.Length > 253)
        {
            return false;
        }

        if (hostname.StartsWith('.') || hostname.EndsWith('.'))
        {
            return false;
        }

        if (hostname.Contains(' '))
        {
            return false;
        }

        if (SshTokenRegex().IsMatch(hostname))
        {
            return true;
        }

        return HostnameRegex().IsMatch(hostname);
    }

    public static bool ValidateIp(string ip)
    {
        return IPAddress.TryParse(ip, out _);
    }

    public static bool ValidatePort(string port)
    {
        if (string.IsNullOrEmpty(port))
        {
            return true;
        }

        if (!int.TryParse(port, out int portNum))
        {
            return false;
        }

        return portNum is >= 1 and <= 65535;
    }

    public static bool ValidateHostName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 50)
        {
            return false;
        }

        return !name.Any(c => c is ' ' or '\t' or '\n' or '\r' or '#');
    }

    public static bool ValidateIdentityFile(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        if (SshTokenRegex().IsMatch(path))
        {
            return true;
        }

        bool hasUndefined = false;
        string expanded = Environment.ExpandEnvironmentVariables(path);
        if (expanded.Contains('$'))
        {
            hasUndefined = true;
        }

        if (hasUndefined)
        {
            return true;
        }

        if (expanded.StartsWith("~/"))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expanded = Path.Combine(homeDir, expanded[2..]);
        }

        return File.Exists(expanded);
    }

    public static void ValidateHost(string name, string hostname, string port, string identity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("host name is required");
        }

        if (!ValidateHostName(name))
        {
            throw new InvalidOperationException("invalid host name: cannot contain spaces or special characters");
        }

        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new InvalidOperationException("hostname/IP is required");
        }

        if (!ValidateHostname(hostname) && !ValidateIp(hostname))
        {
            throw new InvalidOperationException("invalid hostname or IP address format");
        }

        if (!ValidatePort(port))
        {
            throw new InvalidOperationException("port must be between 1 and 65535");
        }

        if (!string.IsNullOrEmpty(identity) && !ValidateIdentityFile(identity))
        {
            throw new InvalidOperationException($"identity file does not exist: {identity}");
        }
    }
}
