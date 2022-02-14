using System.Diagnostics;
using static System.Console;

namespace Manic.Example;

internal static class Program
{
    private static async Task Main()
    {
        if (!Environment.Is64BitProcess)
        {
            WriteLine("e: You must compile only for x64_86!");
            Environment.Exit(1);
        }

        var process = Process.GetProcessesByName("valheim").FirstOrDefault();
        if (process is null)
        {
            WriteLine("e: Unable to find Valheim process!");
            Environment.Exit(1);
        }

        WriteLine("i: Valheim found on PID {0} (with {1} modules)", process.Id, process.Modules.Count);
        WriteLine("i: Also the game is using about {0:F}GB of your RAM", process.WorkingSet64 / 1e+9);
    
        const string pattern =
            "48xxxxxxxxxxxxxxxxxx49xxxxxxxxxxxxxxxxxx41xxxxxxxxxxxx48xxxxxxxxxxxxxxxxxxc6xxxxxxxxxxxxxxxxxxxxxxxxc7";
        
        var manic = new Manic(process.Id);
        var results = manic.BinaryPatternSearch(pattern).ToArray();
        
        if (results.Length != 1)
        {
            WriteLine("e: Unable to find a unique signature match!");
            Environment.Exit(1);
        }

        var player = results[0];

        player = IntPtr.Add(player, 0x02);
        player = manic.ReadVirtualMemory<IntPtr>(player);
        player = IntPtr.Subtract(player, 0x08);
        player = manic.ReadVirtualMemory<IntPtr>(player);
        
        WriteLine("i: Player found at 0x{0:X}", player);
        WriteLine("i: Have a good game pwning session");
        WriteLine("i: You can reach further distances");
        WriteLine("i: Press CTRL+C to exit Manic Example");
        WriteLine();
        
        var staminaAddress = IntPtr.Add(player, 0x0590);
        var maxStaminaAddress = IntPtr.Add(player, 0x0594);
        var maxInteractDistanceAddress = IntPtr.Add(player, 0x0528);

        while (true)
        {
            var stamina = manic.ReadVirtualMemory<float>(staminaAddress);

            if (stamina <= 15)
            {
                var maxStamina = manic.ReadVirtualMemory<float>(maxStaminaAddress);

                WriteLine("i: Low stamina detected ({0:F1}/{1:F0}), solving ;)", stamina, maxStamina);
                manic.WriteVirtualMemory(staminaAddress, maxStamina);    
            }
            
            manic.WriteVirtualMemory(maxInteractDistanceAddress, 25.5);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
    }
}
