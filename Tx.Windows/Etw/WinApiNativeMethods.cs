using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Tx.Windows.Etw
{
    internal static class WinApiNativeMethods
    {
        private const int ERROR_SUCCESS = 0;

        public static void CheckBool(this bool v)
        {
            if (!v)
            {
                throw new Win32Exception();
            }
        }

        public static void CheckError(this int err)
        {
            if (err != ERROR_SUCCESS)
            {
                throw new Win32Exception(err);
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
        public unsafe static extern void ZeroMemory(byte* destination, int length);
    }
}
