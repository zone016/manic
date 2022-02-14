using System.Runtime.InteropServices;

// ReSharper disable MemberCanBePrivate.Global

namespace Manic.Structs;

/*
typedef struct _SYSTEM_INFO {
  union {
    DWORD dwOemId;
    struct {
      WORD wProcessorArchitecture;
      WORD wReserved;
    } DUMMYSTRUCTNAME;
  } DUMMYUNIONNAME;
  DWORD     dwPageSize;
  LPVOID    lpMinimumApplicationAddress;
  LPVOID    lpMaximumApplicationAddress;
  DWORD_PTR dwActiveProcessorMask;
  DWORD     dwNumberOfProcessors;
  DWORD     dwProcessorType;
  DWORD     dwAllocationGranularity;
  WORD      wProcessorLevel;
  WORD      wProcessorRevision;
} SYSTEM_INFO, *LPSYSTEM_INFO;
 */

[StructLayout(LayoutKind.Sequential)]
public struct SystemInfo
{
  public readonly ushort wProcessorArchitecture;
  public readonly ushort wReserved;
  public readonly uint dwPageSize;
  public readonly IntPtr lpMinimumApplicationAddress;
  public readonly IntPtr lpMaximumApplicationAddress;
  public readonly UIntPtr dwActiveProcessorMask;
  public readonly uint dwNumberOfProcessors;
  public readonly uint dwProcessorType;
  public readonly uint dwAllocationGranularity;
  public readonly ushort wProcessorLevel;
  public readonly ushort wProcessorRevision;
}