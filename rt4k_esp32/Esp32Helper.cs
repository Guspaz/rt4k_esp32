using System.Threading;
using nanoFramework.Runtime.Native;

namespace rt4k_esp32
{
    internal static class Esp32Helper
    {
        public static void RebootWithDelay(int delay)
        {
            new Thread(() =>
            {
                Thread.Sleep(delay);
                Program.Log("Rebooting");
                Power.RebootDevice();
            }).Start();
        }
    }
}
