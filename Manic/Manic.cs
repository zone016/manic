using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

using Manic.Enums;
using Manic.Structs;
using Manic.PlatformInvoke;

namespace Manic;

public class Manic : IDisposable
{
    private readonly SafeProcessHandle _handle;

    public Manic(int processId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException();

        var process = Process.GetProcessById(processId) ?? throw new ArgumentException(
            "The specified PID do not represent a valid process", nameof(processId)
        );
        _handle = process.SafeHandle;
    }

    #region Virtual Memory Management

    public IntPtr AllocateVirtualMemory(IntPtr baseAddress, int size,
        MemoryProtection protectionType = MemoryProtection.ExecuteReadWrite)
    {
        const AllocationType allocationType = AllocationType.Commit | AllocationType.Reserve;
        
        var allocAddress = Kernel32.VirtualAllocEx(_handle, baseAddress, size, allocationType, protectionType);
        if (allocAddress != IntPtr.Zero) return allocAddress;
        
        var error = Marshal.GetLastWin32Error();
        throw new Win32Exception($"Unable to alloc memory at 0x{baseAddress:X}, returned {error}.");
    }

    public IntPtr AllocateVirtualMemory(int size, MemoryProtection protectionType = MemoryProtection.ExecuteReadWrite)
    {
        const AllocationType allocationType = AllocationType.Commit | AllocationType.Reserve;
        var allocAddress = Kernel32.VirtualAllocEx(_handle, IntPtr.Zero, size, allocationType, protectionType);
        if (allocAddress != IntPtr.Zero) return allocAddress;

        var error = Marshal.GetLastWin32Error();
        throw new Win32Exception($"Unable to alloc memory, returned {error}.");
    }

    public void FreeAllocatedVirtualMemory(IntPtr baseAddress)
    {
        if (Kernel32.VirtualFreeEx(_handle, baseAddress, 0, FreeType.Release)) return;
        
        var error = Marshal.GetLastWin32Error();
        throw new Win32Exception($"Unable to free memory at 0x{baseAddress:X}, returned {error}.");
    }
    
    public MemoryProtection ChangeVirtualMemoryProtection(IntPtr baseAddress, int size, MemoryProtection protectionType)
    {
        if (Kernel32.VirtualProtectEx(_handle, baseAddress, size, protectionType, out var oldProtectionType))
            return oldProtectionType;
        
        var error = Marshal.GetLastWin32Error();
        throw new Win32Exception($"Unable to change memory protection at 0x{baseAddress:X}, returned {error}.");
    }

    public void WriteVirtualMemory(IntPtr baseAddress, byte[] bytesToWrite)
    {
        //TODO:
        // - Query memory space to check if is possible to write instead of waiting an error from WriteProcessMemory.
        
        var bufferHandle = GCHandle.Alloc(bytesToWrite, GCHandleType.Pinned);
        
        if (!Kernel32.WriteProcessMemory(_handle, baseAddress, bufferHandle.AddrOfPinnedObject(), 
                bytesToWrite.Length, out var writtenBuffer))
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception($"Unable to write memory at 0x{baseAddress:X}, returned {error}.");
        }
        
        bufferHandle.Free();
        
        if (writtenBuffer.ToInt32() != bytesToWrite.Length)
        {
            throw new Win32Exception(
                "Written bytes differ from the API " +
                $"(struct size is {bytesToWrite.Length}, but the return was {writtenBuffer.ToInt32()})"
            );
        }
    }
    
    public void WriteVirtualMemory<TStruct>(IntPtr baseAddress, TStruct structToWrite) where TStruct : struct
    {
        //TODO:
        // - Query memory space to check if is possible to write instead of waiting an error from WriteProcessMemory.
        
        var size = Marshal.SizeOf<TStruct>();
        var bufferHandle = GCHandle.Alloc(structToWrite, GCHandleType.Pinned);
        
        if (!Kernel32.WriteProcessMemory(_handle, baseAddress, 
                bufferHandle.AddrOfPinnedObject(), size, out var writtenBuffer))
        {
            bufferHandle.Free();
            
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception($"Unable to write memory at 0x{baseAddress:X}, returned {error}.");
        }
        
        bufferHandle.Free();

        if (writtenBuffer.ToInt32() != size)
        {
            throw new Win32Exception($"Written bytes differ from the API (struct size is {size}, " +
                                     $"but the return was {writtenBuffer.ToInt32()})");
        }
    }

    public byte[] ReadVirtualMemory(IntPtr baseAddress, long size, bool reThornWin32Exception = true)
    {
        var buffer = new byte[size];
        
        if (!Kernel32.ReadProcessMemory(_handle, baseAddress, buffer, buffer.Length, out var totalBytesRead))
        {
            if (!reThornWin32Exception) return Array.Empty<byte>();
            
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception($"Unable to read memory at 0x{baseAddress:X}, returned {error}.");

        }
        
        if (totalBytesRead.ToInt64() != size)
        {
            throw new Win32Exception($"Read bytes differ from the API (struct size is {size}, " +
                                     $"but the return was {totalBytesRead.ToInt32()})");
        }

        return buffer;
    }
    
    public TStruct ReadVirtualMemory<TStruct>(IntPtr baseAddress) where TStruct : struct
    {
        //TODO:
        // - Query memory space to check if is possible to read instead of waiting an error from ReadVirtualMemory.
        
        var size = Marshal.SizeOf<TStruct>();
        var buffer = Marshal.AllocHGlobal(size);

        // There is no way to get a precise size of a managed object.
        if (!Kernel32.ReadProcessMemory(_handle, baseAddress, buffer, size, out _))
        {
            Marshal.FreeHGlobal(buffer);
            
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception($"Unable to read memory at 0x{baseAddress:X}, returned {error}.");
        }

        try
        {
            var entity = Marshal.PtrToStructure<TStruct>(buffer);
            return entity;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    #endregion

    #region Virtual Memory Region

    public IEnumerable<MemoryBasicInformation> GetMemoryRegions(IntPtr baseAddress = new(), IntPtr finalAddress = new(),
        Func<MemoryProtection, bool>? hasRestrictivePermissions = null, bool reThrownWin32Exceptions = false)
    {
        if (finalAddress != IntPtr.Zero && finalAddress.ToInt64() <= baseAddress.ToInt64())
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseAddress),
                "Your base address is less or equal than your final address."
            );
        }
        
        hasRestrictivePermissions ??= HasRestrictivePermissions;
        
        var systemInfo = new SystemInfo();

        if (Environment.Is64BitProcess) Kernel32.GetNativeSystemInfo(ref systemInfo);
        else Kernel32.GetSystemInfo(ref systemInfo);

        var memoryInformation = GetMemoryRegion(baseAddress);
        if (!hasRestrictivePermissions(memoryInformation.Protect)) yield return memoryInformation;
        
        var minimumAppAddress = systemInfo.lpMinimumApplicationAddress;
        var maximumAppAddress = systemInfo.lpMaximumApplicationAddress;

        // Maybe I should do a unsafe pointer arithmetic instead of fighting with vALU?
        if (memoryInformation.BaseAddress != IntPtr.Zero &&
            memoryInformation.BaseAddress.ToInt64() < minimumAppAddress.ToInt64())
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseAddress),
                "Your base address resolved to a memory region bellow the minimum."
            );
        }

        // Same here, but who knows, maybe in the future I'll do something with better performance and nice to read c:
        var maximumRange = new IntPtr(memoryInformation.BaseAddress.ToInt64() + memoryInformation.RegionSize.ToInt64());
        if (maximumRange.ToInt64() > maximumAppAddress.ToInt64())
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseAddress),
                "Your base address resolved to a memory region after the limit."
            );
        }

        while (true)
        {
            try
            {
                // (:
                var deeperAddress =
                    new IntPtr(memoryInformation.BaseAddress.ToInt64() + memoryInformation.RegionSize.ToInt64());
                if (finalAddress != IntPtr.Zero && deeperAddress.ToInt64() > finalAddress.ToInt64()) break;
                
                memoryInformation = GetMemoryRegion(deeperAddress);
            }
            catch (Win32Exception)
            {
                if (reThrownWin32Exceptions)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new Win32Exception($"Unable to query memory region, returned {error}.");
                }
            
                break;
            }

            if (!hasRestrictivePermissions(memoryInformation.Protect)) yield return memoryInformation;
        }
    }

    #region Non Invasive P/Invoke

    public MemoryBasicInformation GetMemoryRegion(IntPtr baseAddress = new())
    {
        var systemInfo = new SystemInfo();

        if (Environment.Is64BitProcess) Kernel32.GetNativeSystemInfo(ref systemInfo);
        else Kernel32.GetSystemInfo(ref systemInfo);
        
        var minimumAppAddress = systemInfo.lpMinimumApplicationAddress;

        // Maybe I should do a unsafe pointer arithmetic instead of fighting with vALU?
        if (baseAddress != IntPtr.Zero && baseAddress.ToInt64() < minimumAppAddress.ToInt64())
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseAddress),
                "Your base address resolved to a memory region bellow the minimum."
            );
        }
        
        var size = Marshal.SizeOf<MemoryBasicInformation>();
        if (Kernel32.VirtualQueryEx(_handle, baseAddress, out var memoryInformation, size)) return memoryInformation;
        
        var error = Marshal.GetLastWin32Error();
        throw new Win32Exception($"Unable to query memory region, returned {error}.");
    }

    #endregion

    #endregion

    #region Binary Pattern Search

    public ImmutableList<IntPtr> BinaryPatternSearch(string pattern, IntPtr baseAddress = new(),
        IntPtr finalAddress = new(), Func<MemoryProtection, bool>? hasRestrictivePermissions = null,
        bool reThrownWin32Exceptions = false)
    {
        pattern = pattern.Trim();
        pattern = pattern.Replace(" ", string.Empty);
        pattern = pattern.ToUpperInvariant();
        
        if (string.IsNullOrEmpty(pattern) || pattern.Length % 2 != 0 || pattern.Contains(Environment.NewLine))
        {
            throw new InvalidEnumArgumentException("Your pattern must have a even length.");
        }

        var parsedPattern = pattern
            .ToCharArray()
            .Chunk(2)
            .Select(c => string.Join(string.Empty, c)
                .Replace("??", "00")
                .Replace("XX", "00"))
            .Select(s => Convert.ToByte(s, 16))
            .ToArray();

        return BinaryPatternSearch(
            parsedPattern, baseAddress,
            finalAddress, hasRestrictivePermissions,
            reThrownWin32Exceptions
        );
    }
    
    public ImmutableList<IntPtr> BinaryPatternSearch(byte[] pattern, IntPtr baseAddress = new(), 
        IntPtr finalAddress = new(), Func<MemoryProtection, bool>? hasRestrictivePermissions = null, 
        bool reThrownWin32Exceptions = false)
    { 
        var matches = new List<IntPtr>();
        
        var regions = GetMemoryRegions(
            baseAddress, finalAddress,
            hasRestrictivePermissions, 
            reThrownWin32Exceptions
        ).ToArray();
        
        regions.AsParallel()
            .ForAll(memoryInformation =>
            {
                if (memoryInformation.BaseAddress == IntPtr.Zero) return;
                
                var memoryDump = ReadVirtualMemory(
                    memoryInformation.BaseAddress,
                    memoryInformation.RegionSize.ToInt64(),
                    false
                );

                // Simplistic, need to be improved.

                var scan = Scan(memoryDump, pattern, memoryInformation.BaseAddress).ToList();
                if (scan.Any()) matches.AddRange(scan);
                // for (var i = 0; i < memoryDump.Length; i++)
                // {
                //     if (memoryDump[i] != pattern[0]) continue;
                //     for (var j = 0; j < pattern.Length; j++)
                //     {
                //         if (pattern[j] != 0x00 && memoryDump[i + j] != pattern[j]) break;
                //
                //         if (j + 1 != pattern.Length) continue;
                //         var address = IntPtr.Add(baseAddress, i);
                //         matches.Add(address);
                //     }
                // }
            });

        return matches.ToImmutableList();
    }

    #endregion

    #region Local Helpers

    private static IEnumerable<IntPtr> Scan(IReadOnlyList<byte> memoryDump, 
        IReadOnlyList<byte> bytesSignature, IntPtr baseAddress)
    {
        var signatureMatches = new List<IntPtr>();
    
        for (var i = 0; i < memoryDump.Count; i++)
        {
            if (memoryDump[i] != bytesSignature[0]) continue;
            for (var j = 0; j < bytesSignature.Count; j++)
            {
                var k = i + j;
                if (k > memoryDump.Count)
                {
                    var diff = k - memoryDump.Count;
                    k -= diff;

                    if (k == memoryDump.Count) k = memoryDump.Count - 1;
                }

                if (bytesSignature[j] != 0x00 && memoryDump[k] != bytesSignature[j]) break;

                if (j + 1 != bytesSignature.Count) continue;
                var address = IntPtr.Add(baseAddress, i);
                signatureMatches.Add(address);
            }
        }

        return signatureMatches;
    }
    
    private static bool HasRestrictivePermissions(MemoryProtection memoryProtection) =>
        memoryProtection switch
        {
            MemoryProtection.ZeroAccess => true,
            MemoryProtection.Guard => true,
            MemoryProtection.NoAccess => true,
            MemoryProtection.ReadWriteGuard => true,
            MemoryProtection.ReadOnly => false,
            MemoryProtection.ReadWrite => false,
            MemoryProtection.WriteCopy => false,
            MemoryProtection.Execute => false,
            MemoryProtection.ExecuteRead => false,
            MemoryProtection.ExecuteReadWrite => false,
            MemoryProtection.ExecuteWriteCopy => false,
            MemoryProtection.NoCache => false,
            _ => false
        };
    
    #endregion
    
    public void Dispose()
    {
        _handle.Dispose();
        GC.SuppressFinalize(this);
    }
}