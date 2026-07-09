namespace Sshm.Config;

internal static class ConfigLock
{
    internal static readonly object SyncRoot = new();
}
