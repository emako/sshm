using System.Runtime.InteropServices;
using System.Text;

namespace Sshm.Core;

public static class ConsoleEncoding
{
    private const uint Utf8CodePage = 65001;

    public static void EnsureUtf8()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                SetConsoleOutputCP(Utf8CodePage);
                SetConsoleCP(Utf8CodePage);
            }
            catch
            {
                // 部分宿主环境不允许修改代码页，继续设置 .NET 编码
            }
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCP(uint wCodePageID);
}
