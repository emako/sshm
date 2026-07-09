namespace Sshm.Core.Enums;

public enum SortMode
{
    ByName,
    ByLastUsed,
}

public static class SortModeExtensions
{
    public static string ToDisplayString(this SortMode mode)
    {
        return mode switch
        {
            SortMode.ByLastUsed => "Last Login",
            _ => "Name (A-Z)",
        };
    }
}
