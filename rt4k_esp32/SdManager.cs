using System;
using System.Device.Gpio;
using System.Threading;
using nanoFramework.Hardware.Esp32;
using nanoFramework.System.IO.FileSystem;

namespace rt4k_esp32
{
    internal class SdManager
    {
        internal delegate void LogDelegate(string message);

        // Delay releasing SD until idle for this many milliseconds
        private const int sdReleaseTimeout = 1000;

        // Don't touch the SD card for the first X microseconds after boot
        private const ulong RT4K_BOOT = 10 * 1000 * 1000;
        private const int CS_SENSE_PIN = 33;
        private const int SD_SWITCH_PIN = 26;

        private readonly GpioController gpio = new(PinNumberingScheme.Logical);
        private readonly SDCard SDCard = new(new SDCard.SDCardSpiParameters { spiBus = 1, chipSelectPin = 13, enableCardDetectPin = false });
        //private readonly SDCard SDCard = new SDCard(new SDCard.SDCardMmcParameters { dataWidth = SDCard.SDDataWidth._4_bit, enableCardDetectPin = false });

        private readonly GpioPin CS_SENSE;
        private GpioPin SD_SWITCH;
        private bool isSdGrabbed = false;
        private ulong timerStart = 0;
        private readonly Timer releaseTimer;
        private bool pinsReady = false;
        private readonly LogDelegate Log;

        internal SdManager(LogDelegate logDelegate)
        {
            Log = logDelegate;
            Log("SdManager starting up");

            releaseTimer = new Timer(ReleaseCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Init CS_SENSE pin
            if (!gpio.IsPinOpen(CS_SENSE_PIN))
            {
                CS_SENSE = gpio.OpenPin(CS_SENSE_PIN, PinMode.Input);
            }
            else if (CS_SENSE.GetPinMode() != PinMode.Input)
            {
                CS_SENSE.SetPinMode(PinMode.Input);
            }

            // Set up ESP32 for SPI operation
            Configuration.SetPinFunction(2, DeviceFunction.SPI1_MISO);
            Configuration.SetPinFunction(15, DeviceFunction.SPI1_MOSI);
            Configuration.SetPinFunction(14, DeviceFunction.SPI1_CLOCK);
        }

        private bool IsSdAvailable()
        {
            return HighResTimer.GetCurrent() > RT4K_BOOT && CS_SENSE.Read() == PinValue.High;
        }

        private void InitSdSwitch()
        {
            // Init SD_SWITCH pin
            if (!gpio.IsPinOpen(SD_SWITCH_PIN))
            {
                SD_SWITCH = gpio.OpenPin(SD_SWITCH_PIN, PinMode.Output);
            }
            else if (SD_SWITCH.GetPinMode() != PinMode.Output)
            {
                SD_SWITCH.SetPinMode(PinMode.Output);
            }
        }

        internal void GrabSD()
        {
            Monitor.Enter(gpio);

            if (!pinsReady)
            {
                pinsReady = true;
                InitSdSwitch();
            }

            releaseTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (isSdGrabbed)
            {
                //Log($"[{Thread.CurrentThread.ManagedThreadId}] Soft grabbing SD card");
            }
            else
            {
                Log($"[{Thread.CurrentThread.ManagedThreadId}] Trying to grab SD card");
                // Run the GC before grabbing the SD card to reduce the chance it runs while we're holding it
                nanoFramework.Runtime.Native.GC.Run(true);

                // Wait for the RT4K to be done with the SD card
                ulong waitStartTime = HighResTimer.GetCurrent();

                bool sdAvailable = IsSdAvailable();

                if (!sdAvailable)
                {
                    Log("RT4K is using the SD card, waiting for it to be free");
                }

                ResetTimer();

                while (!sdAvailable)
                {
                    Thread.Sleep(100);
                    sdAvailable = IsSdAvailable();
                }

                Log($"Grabbed SD card after {TimeElapsed} ms");

                // Reset the timer, so we can measure how long we held the SD card for
                ResetTimer();

                SD_SWITCH.Write(PinValue.Low);

                try
                {
                    SDCard.Mount();
                }
                catch (Exception ex)
                {
                    Log("Failed to mount SD");
                    Log($"IsMounted: {SDCard.IsMounted}");
                    LogException(ex);
                }

                isSdGrabbed = true;
            }
        }


        internal void ReleaseSD(bool instantRelease = false)
        {
            if (instantRelease)
            {
                ReleaseCallback(null);
            }
            else
            {
                //Log($"[{Thread.CurrentThread.ManagedThreadId}] Soft releasing SD card");
                releaseTimer.Change(sdReleaseTimeout, Timeout.Infinite);
            }

            Monitor.Exit(gpio);
        }

        private void ReleaseCallback(object state)
        {
            isSdGrabbed = false;
            releaseTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (SDCard.IsMounted)
            {
                SDCard.Unmount();
            }

            SD_SWITCH.Write(PinValue.High);

            Log($"[{Thread.CurrentThread.ManagedThreadId}] Released SD card after {TimeElapsed} ms");
        }

        private void LogException(Exception ex)
        {
            Log(ex.Message);
            Log(ex.StackTrace);
            if (ex.InnerException != null)
            {
                Log("Inner Exception:");
                Log(ex.InnerException.Message);
                Log(ex.InnerException.StackTrace);
            }
        }

        private float TimeElapsed => (HighResTimer.GetCurrent() - timerStart) / 1000.0f;
        private void ResetTimer() => timerStart = HighResTimer.GetCurrent();
    }
}