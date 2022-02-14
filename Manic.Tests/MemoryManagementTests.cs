using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Manic.Enums;

namespace Manic.Tests;

[TestClass]
public class MemoryManagementTests
{
    private readonly Manic _manic;
    private readonly Process _currentProcess;

    public MemoryManagementTests()
    {
        _currentProcess = Process.GetCurrentProcess();
        _manic = new Manic(_currentProcess.Id);
    }

    [TestMethod]
    public void TestVirtualMemoryManipulation()
    {
        var processModule = _currentProcess.MainModule;
        Assert.IsTrue(processModule != null);

        var baseAddress = processModule.BaseAddress;
        Assert.AreNotEqual(baseAddress, IntPtr.Zero);

        var allocAddress = _manic.AllocateVirtualMemory(sizeof(int), MemoryProtection.ReadWrite);
        Assert.AreNotEqual(allocAddress, IntPtr.Zero);

        const int number = int.MaxValue;
        _manic.WriteVirtualMemory(allocAddress, number);

        var readNumber = _manic.ReadVirtualMemory<int>(allocAddress);
        Assert.AreEqual(readNumber, number);

        var memoryProtection =
            _manic.ChangeVirtualMemoryProtection(allocAddress, sizeof(int), MemoryProtection.ReadOnly);
        Assert.AreEqual(memoryProtection, MemoryProtection.ReadWrite);

        var regionSize = IntPtr.Add(allocAddress, sizeof(int));
        var allocMemoryInformation = 
            _manic.GetMemoryRegions(allocAddress, regionSize).ToArray();
        Assert.IsTrue(allocMemoryInformation.Length == 1);

        _manic.FreeAllocatedVirtualMemory(allocAddress);

        var memoryInformation = _manic.GetMemoryRegion();
        Assert.AreNotEqual(memoryInformation, null);
    }

    [TestMethod]
    public void TestBinaryPatternSearch()
    {
        var processModule = _currentProcess.MainModule;
        Assert.IsTrue(processModule != null);

        var baseAddress = processModule.BaseAddress;
        Assert.AreNotEqual(baseAddress, IntPtr.Zero);

        var something = new byte[] {0x83, 0xC5, 0x40, 0xF3, 0x0F, 0x10, 0xCA, 0x83, 0xED, 0x40};
        
        var pattern = new byte[]   {0x83, 0xC5, 0x00, 0x00, 0x00, 0x10, 0xCA, 0x00, 0xED, 0x40};
        const string pattern2 = "83 C5 ?? ?? ?? 10 CA ?? ED 40";
        const string pattern3 = "83c5??????10ca??ed40";
        const string pattern4 = "83c5xxxxxx10caxxed40";
        
        var allocAddress = _manic.AllocateVirtualMemory(something.Length);
        _manic.WriteVirtualMemory(allocAddress, something);

        var lastAddress = IntPtr.Add(allocAddress, something.Length);
        var matches = _manic.BinaryPatternSearch(pattern, allocAddress, lastAddress).ToArray();
        
        Assert.IsTrue(matches.Length == 1);
        Assert.AreEqual(matches.First(), allocAddress);
        
        matches = _manic.BinaryPatternSearch(pattern2, allocAddress, lastAddress).ToArray();
        Assert.IsTrue(matches.Length == 1);
        Assert.AreEqual(matches.First(), allocAddress);
        
        matches = _manic.BinaryPatternSearch(pattern3, allocAddress, lastAddress).ToArray();
        Assert.IsTrue(matches.Length == 1);
        Assert.AreEqual(matches.First(), allocAddress);
        
        matches = _manic.BinaryPatternSearch(pattern4, allocAddress, lastAddress).ToArray();
        Assert.IsTrue(matches.Length == 1);
        Assert.AreEqual(matches.First(), allocAddress);

        _manic.FreeAllocatedVirtualMemory(allocAddress);
    }
}
