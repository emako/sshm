namespace Sshm.Config;

internal static class FilePermissions
{
    internal static void SetSecureFilePermissions(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(filePath);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
