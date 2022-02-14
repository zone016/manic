using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using Manic.Enums;
using Manic.Structs;

namespace Manic.PlatformInvoke;
public static class Kernel32
{
   [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadProcessMemory(
        SafeProcessHandle hProcess,
        IntPtr lpBaseAddress,
        IntPtr lpBuffer,
        long dwSize,
        out IntPtr lpNumberOfBytesRead
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadProcessMemory(
        SafeProcessHandle hProcess,
        IntPtr lpBaseAddress,
        [Out] byte[] lpBuffer,
        long dwSize,
        out IntPtr lpNumberOfBytesRead
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr VirtualAllocEx(
        SafeProcessHandle processHandle,
        IntPtr baseAddress,
        int allocationSize,
        AllocationType allocationType,
        MemoryProtection protectionType
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool VirtualFreeEx(
        SafeProcessHandle processHandle,
        IntPtr baseAddress,
        int freeSize,
        FreeType freeType
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool VirtualProtectEx(
        SafeProcessHandle processHandle,
        IntPtr baseAddress,
        int protectionSize,
        MemoryProtection protectionType,
        out MemoryProtection oldProtectionType
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool VirtualQueryEx(
        SafeProcessHandle processHandle,
        IntPtr baseAddress,
        out MemoryBasicInformation memoryInformation,
        int length
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteProcessMemory(
        SafeProcessHandle processHandle,
        IntPtr baseAddress,
        IntPtr bufferToWrite,
        int bytesToWriteSize,
        out IntPtr numberOfBytesWrittenBuffer
    );
    
    [DllImport("kernel32.dll")]
    internal static extern void GetNativeSystemInfo(ref SystemInfo lpSystemInfo);

    [DllImport("kernel32.dll")]
    internal static extern void GetSystemInfo(ref SystemInfo lpSystemInfo);
}
