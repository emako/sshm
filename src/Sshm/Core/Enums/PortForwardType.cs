namespace Sshm.Core.Enums;

public enum PortForwardType
{
    Local,
    Remote,
    Dynamic,
}

public static class PortForwardTypeExtensions
{
    public static string ToDisplayString(this PortForwardType type)
    {
        return type switch
        {
            PortForwardType.Remote => "Remote (-R)",
            PortForwardType.Dynamic => "Dynamic (-D)",
            _ => "Local (-L)",
        };
    }

    public static string ToHistoryType(this PortForwardType type)
    {
        return type switch
        {
            PortForwardType.Remote => "remote",
            PortForwardType.Dynamic => "dynamic",
            _ => "local",
        };
    }

    public static PortForwardType FromHistoryType(string? type)
    {
        return type switch
        {
            "remote" => PortForwardType.Remote,
            "dynamic" => PortForwardType.Dynamic,
            _ => PortForwardType.Local,
        };
    }
}
