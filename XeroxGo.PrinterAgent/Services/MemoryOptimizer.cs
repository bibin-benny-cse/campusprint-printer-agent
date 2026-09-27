using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace XeroxGo.PrinterAgent.Services
{
    public static class MemoryOptimizer
    {
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int SetProcessWorkingSetSize(IntPtr process, int minimumWorkingSetSize, int maximumWorkingSetSize);

        private static System.Threading.Timer? _periodicTimer;

        public static void InitializePeriodicTrimming(int intervalMinutes = 3)
        {
            _periodicTimer ??= new System.Threading.Timer(_ => TrimMemory(), null, TimeSpan.FromMinutes(intervalMinutes), TimeSpan.FromMinutes(intervalMinutes));
        }

        public static void TrimMemory()
        {
            try
            {
                // 1. Run low-pause garbage collection and compact LOH (reclaims PDF buffers)
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
                GC.WaitForPendingFinalizers();

                // 2. Request Windows Memory Manager to flush unreferenced working-set pages
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
                }
            }
            catch
            {
                // Non-critical optimization; silently ignore if OS denies access
            }
        }
    }
}
