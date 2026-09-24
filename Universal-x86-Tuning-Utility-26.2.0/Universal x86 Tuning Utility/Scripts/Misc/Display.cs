using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Universal_x86_Tuning_Utility.Views.Windows;
using static Universal_x86_Tuning_Utility.Scripts.Misc.ScreenInterrogatory;

namespace Universal_x86_Tuning_Utility.Scripts.Misc
{
    internal class Display
    {

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, uint iModeNum, ref DEVMODE lpDevMode);

        [DllImport("user32.dll")]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DISPLAY_DEVICE
        {
            public uint cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        public static string targetDisplayName = FindLaptopScreen();
        public static List<string> uniqueResolutions = GetSupportedResolutions(targetDisplayName);
        public static List<int> uniqueRefreshRates = GetSupportedRefreshRates(targetDisplayName);

        public static void setUpLists()
        {
            try
            {
                targetDisplayName = FindLaptopScreen();
                uniqueRefreshRates = GetSupportedRefreshRates(targetDisplayName);
                if (uniqueRefreshRates.Count == 0)
                {
                    uniqueRefreshRates = GetSupportedRefreshRates(null);
                }
                uniqueRefreshRates = uniqueRefreshRates.Distinct().ToList();
                uniqueRefreshRates.Sort();
                uniqueRefreshRates.Reverse();
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, "Failed to setup display lists");
            }
        }

        static List<string> GetSupportedResolutions(string targetDisplayName)
        {
            List<string> resolutions = new List<string>();
            try
            {
                DEVMODE devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);
                for (uint modeIndex = 0; EnumDisplaySettings(targetDisplayName, modeIndex, ref devMode); modeIndex++)
                {
                    string resolution = $"{devMode.dmPelsWidth} x {devMode.dmPelsHeight}";
                    if (!resolutions.Contains(resolution)) resolutions.Add(resolution);
                }
            }
            catch { }

            return resolutions;
        }

        public static List<int> GetSupportedRefreshRates(string targetDisplayName)
        {
            List<int> refreshRates = new List<int>();
            try
            {
                DEVMODE devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);

                if (!string.IsNullOrEmpty(targetDisplayName))
                {
                    for (uint modeIndex = 0; EnumDisplaySettings(targetDisplayName, modeIndex, ref devMode); modeIndex++)
                    {
                        if (devMode.dmDisplayFrequency > 0 && !refreshRates.Contains(devMode.dmDisplayFrequency))
                        {
                            refreshRates.Add(devMode.dmDisplayFrequency);
                        }
                    }
                }

                if (refreshRates.Count == 0)
                {
                    for (uint modeIndex = 0; EnumDisplaySettings(null, modeIndex, ref devMode); modeIndex++)
                    {
                        if (devMode.dmDisplayFrequency > 0 && !refreshRates.Contains(devMode.dmDisplayFrequency))
                        {
                            refreshRates.Add(devMode.dmDisplayFrequency);
                        }
                    }
                }

                // Also check current mode
                if (EnumDisplaySettings(targetDisplayName, ENUM_CURRENT_SETTINGS, ref devMode) || EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    if (devMode.dmDisplayFrequency > 0 && !refreshRates.Contains(devMode.dmDisplayFrequency))
                    {
                        refreshRates.Add(devMode.dmDisplayFrequency);
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, "Failed to get supported refresh rates");
            }

            return refreshRates;
        }

        public static bool ChangeDisplaySettings(string targetDisplayName, int newRefreshRate)
        {
            try
            {
                DEVMODE devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(devMode);

                string devName = !string.IsNullOrEmpty(targetDisplayName) ? targetDisplayName : null;

                if (EnumDisplaySettings(devName, ENUM_CURRENT_SETTINGS, ref devMode) || EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
                {
                    devMode.dmFields = (int)(DisplaySettingsFlags.DM_PELSWIDTH | DisplaySettingsFlags.DM_PELSHEIGHT | DisplaySettingsFlags.DM_DISPLAYFREQUENCY);
                    devMode.dmDisplayFrequency = newRefreshRate;

                    int result = ChangeDisplaySettingsEx(devName, ref devMode, IntPtr.Zero, (uint)(ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_RESET), IntPtr.Zero);

                    if (result != DISP_CHANGE_SUCCESSFUL && devName != null)
                    {
                        result = ChangeDisplaySettingsEx(null, ref devMode, IntPtr.Zero, (uint)(ChangeDisplaySettingsFlags.CDS_UPDATEREGISTRY | ChangeDisplaySettingsFlags.CDS_RESET), IntPtr.Zero);
                    }

                    if (result == DISP_CHANGE_SUCCESSFUL)
                    {
                        DiagnosticLogger.LogInfo($"Display refresh rate changed to {newRefreshRate} Hz successfully.");
                        return true;
                    }
                    else
                    {
                        DiagnosticLogger.LogWarning($"ChangeDisplaySettings returned code {result}");
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, $"Failed to change display settings to {newRefreshRate} Hz");
            }

            return false;
        }

        public static void ApplySettings(int newHz)
        {
            try
            {
                if (newHz <= 0) return;

                targetDisplayName = FindLaptopScreen();
                bool ok = ChangeDisplaySettings(targetDisplayName, newHz);
                if (!ok)
                {
                    ChangeDisplaySettings(null, newHz);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, $"ApplySettings failed for {newHz} Hz");
            }
        }

        const int DISP_CHANGE_SUCCESSFUL = 0;
        public const uint ENUM_CURRENT_SETTINGS = 0xFFFFFFFF;

        [Flags()]
        public enum DisplaySettingsFlags : int
        {
            DM_PELSWIDTH = 0x00080000,
            DM_PELSHEIGHT = 0x00100000,
            DM_DISPLAYFREQUENCY = 0x00400000
        }

        [Flags()]
        public enum ChangeDisplaySettingsFlags : uint
        {
            CDS_UPDATEREGISTRY = 0x00000001,
            CDS_TEST = 0x00000002,
            CDS_FULLSCREEN = 0x00000004,
            CDS_GLOBAL = 0x00000008,
            CDS_SET_PRIMARY = 0x00000010,
            CDS_RESET = 0x40000000,
            CDS_NORESET = 0x10000000
        }

        public const string defaultDevice = @"\\.\DISPLAY1";

        public static string? FindLaptopScreen(bool log = false)
        {
            string? laptopScreen = null;
            try
            {
                var screens = Screen.AllScreens;
                if (screens.Length > 0)
                {
                    laptopScreen = Screen.PrimaryScreen?.DeviceName ?? screens[0].DeviceName;
                }
                else
                {
                    laptopScreen = defaultDevice;
                }
            }
            catch (Exception)
            {
                laptopScreen = defaultDevice;
            }

            return laptopScreen;
        }
    }
}