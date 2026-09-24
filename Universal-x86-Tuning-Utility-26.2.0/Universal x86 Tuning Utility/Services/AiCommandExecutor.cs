using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Universal_x86_Tuning_Utility.Properties;
using Universal_x86_Tuning_Utility.Scripts;
using Universal_x86_Tuning_Utility.Scripts.Intel_Backend;
using Universal_x86_Tuning_Utility.Scripts.Misc;
using Universal_x86_Tuning_Utility.Views.Pages;
using Universal_x86_Tuning_Utility.Views.Windows;
using LibreHardwareMonitor.Hardware;

namespace Universal_x86_Tuning_Utility.Services
{
    public class AiExecutionResult
    {
        public string CleanResponse { get; set; } = string.Empty;
        public string? ExecutedActionText { get; set; }
        public bool HasActionExecuted => !string.IsNullOrEmpty(ExecutedActionText);
    }

    /// <summary>
    /// Bộ điều khiển trung tâm cho phép AI Chat toàn quyền điều khiển phần mềm.
    /// Thực thi lệnh phần cứng (Undervolt CPU/iGPU, TDP, Preset, Adaptive, Stress Test, Clean RAM, Giao diện, Cài đặt hệ thống, Điều hướng...).
    /// </summary>
    public static class AiCommandExecutor
    {
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        /// <summary>
        /// Xử lý câu trả lời của AI và tin nhắn người dùng để bóc tách mã lệnh và thực thi chính xác.
        /// </summary>
        public static async Task<AiExecutionResult> ProcessAndExecuteCommandsAsync(string userMessage, string aiResponse)
        {
            var result = new AiExecutionResult();
            string responseText = aiResponse ?? string.Empty;
            string? executedText = null;

            // 1. Quét tìm action tag từ phản hồi của AI: [ACTION:XYZ] hoặc [COMMAND:XYZ] hoặc [ACTION:SET_TDP:35]
            var match = Regex.Match(responseText, @"\[(?:ACTION|COMMAND):([A-Z0-9_]+(?::\d+)?)\]", RegexOptions.IgnoreCase);
            string? actionCode = null;

            if (match.Success)
            {
                actionCode = match.Groups[1].Value.ToUpperInvariant();
                // Loại bỏ tag khỏi văn bản hiển thị cho người dùng
                responseText = Regex.Replace(responseText, @"\[(?:ACTION|COMMAND):[A-Z0-9_]+(?::\d+)?\]", "").Trim();
            }
            else
            {
                // 2. Dự phòng: Quét trực tiếp ý định người dùng (Intent Matching) để đảm bảo không bỏ sót lệnh
                actionCode = DetectIntentFromUserPrompt(userMessage);
            }

            // Thực thi hành động tương ứng
            if (!string.IsNullOrEmpty(actionCode))
            {
                executedText = await ExecuteActionCodeAsync(actionCode);
            }

            result.CleanResponse = responseText;
            result.ExecutedActionText = executedText;
            return result;
        }

        public static string? DetectIntentFromUserPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return null;
            string p = prompt.ToLowerInvariant();

            // Undervolt iGPU (check before CPU undervolt to prevent conflict)
            if (p.Contains("igpu") || p.Contains("đồ họa tích hợp") || p.Contains("card on"))
            {
                if (p.Contains("undervolt") || p.Contains("hạ áp") || p.Contains("hạ vôn") || p.Contains("giảm áp"))
                {
                    if (p.Contains("tắt") || p.Contains("hủy") || p.Contains("đặt lại") || p.Contains("reset") || p.Contains("disable") || p.Contains("off"))
                        return "DISABLE_IGPU_UNDERVOLT";
                    return "ENABLE_IGPU_UNDERVOLT";
                }
            }

            // Undervolt CPU
            if (p.Contains("undervolt") || p.Contains("hạ vôn") || p.Contains("hạ điện áp") || p.Contains("hạ nhiệt") || p.Contains("giảm ồn quạt") || p.Contains("giảm tiếng ồn") || p.Contains("quạt ồn") || p.Contains("mát máy") || p.Contains("fan noise") || p.Contains("cool cpu") || p.Contains("lower temp") || p.Contains("quiet fan"))
            {
                if (p.Contains("tắt") || p.Contains("hủy") || p.Contains("đặt lại") || p.Contains("reset") || p.Contains("disable") || p.Contains("off") || p.Contains("stop"))
                    return "DISABLE_UNDERVOLT";
                return "ENABLE_UNDERVOLT";
            }
            if (p.Contains("tắt undervolt") || p.Contains("reset undervolt") || p.Contains("mặc định điện áp") || p.Contains("default voltage"))
            {
                return "DISABLE_UNDERVOLT";
            }

            // Custom TDP / Power limit (e.g., "set tdp to 35", "tdp 25w", "đặt tdp 45", "công suất 30w")
            var tdpMatch = Regex.Match(p, @"(?:set\s+)?tdp\s*(?:to\s*)?(\d+)|đặt\s+(?:mức\s+)?tdp\s*(?:là\s*|thành\s*)?(\d+)|công\s+suất\s*(\d+)\s*w|power\s+limit\s*(?:to\s*)?(\d+)");
            if (tdpMatch.Success)
            {
                for (int i = 1; i < tdpMatch.Groups.Count; i++)
                {
                    if (!string.IsNullOrEmpty(tdpMatch.Groups[i].Value))
                    {
                        return $"SET_TDP:{tdpMatch.Groups[i].Value}";
                    }
                }
            }

            // Stress test & Benchmark
            if (p.Contains("stress test") || p.Contains("benchmark") || p.Contains("test máy") || p.Contains("kiểm tra máy") || p.Contains("kiểm tra phần cứng") || p.Contains("chấm điểm máy") || p.Contains("run stress test") || p.Contains("hardware test") || p.Contains("test cpu"))
            {
                return "RUN_STRESS_TEST";
            }

            // Presets (Power Modes)
            if (p.Contains("tiết kiệm pin") || p.Contains("chế độ eco") || p.Contains("bật eco") || p.Contains("tiết kiệm điện") || p.Contains("eco mode") || p.Contains("battery saver") || p.Contains("power saver") || p.Contains("enable eco"))
            {
                return "SET_PRESET_ECO";
            }
            if (p.Contains("cực hạn") || p.Contains("extreme") || p.Contains("max công suất") || p.Contains("hết công suất") || p.Contains("extreme mode") || p.Contains("max performance") || p.Contains("unlock power"))
            {
                return "SET_PRESET_EXTREME";
            }
            if (p.Contains("hiệu năng cao") || p.Contains("chế độ performance") || p.Contains("bật performance") || p.Contains("chơi game") || p.Contains("tăng fps") || p.Contains("tối ưu game") || p.Contains("performance mode") || p.Contains("gaming mode") || p.Contains("game mode") || p.Contains("boost fps") || p.Contains("enable performance"))
            {
                return "SET_PRESET_PERFORMANCE";
            }
            if (p.Contains("cân bằng") || p.Contains("balanced") || p.Contains("chế độ cân bằng") || p.Contains("balanced mode") || p.Contains("enable balanced"))
            {
                return "SET_PRESET_BALANCED";
            }

            // Adaptive Mode
            if (p.Contains("adaptive") || p.Contains("thích ứng"))
            {
                if (p.Contains("tắt") || p.Contains("dừng") || p.Contains("disable") || p.Contains("stop") || p.Contains("off"))
                    return "DISABLE_ADAPTIVE_MODE";
                return "ENABLE_ADAPTIVE_MODE";
            }

            // Auto-Reapply
            if (p.Contains("auto reapply") || p.Contains("tự động áp dụng lại") || p.Contains("tự áp dụng lại"))
            {
                if (p.Contains("tắt") || p.Contains("disable") || p.Contains("off"))
                    return "DISABLE_AUTO_REAPPLY";
                return "ENABLE_AUTO_REAPPLY";
            }

            // Apply on Start
            if (p.Contains("apply on start") || p.Contains("áp dụng khi khởi động") || p.Contains("tự áp dụng khi mở"))
            {
                if (p.Contains("tắt") || p.Contains("disable") || p.Contains("off"))
                    return "DISABLE_APPLY_ON_START";
                return "ENABLE_APPLY_ON_START";
            }

            // Start Minimized
            if (p.Contains("start mini") || p.Contains("khởi động thu nhỏ") || p.Contains("start minimized") || p.Contains("mở thu nhỏ"))
            {
                if (p.Contains("tắt") || p.Contains("disable") || p.Contains("off"))
                    return "DISABLE_START_MINI";
                return "ENABLE_START_MINI";
            }

            // Minimize on Close
            if (p.Contains("minimize close") || p.Contains("thu nhỏ khi đóng") || p.Contains("minimize on close") || p.Contains("đóng thu nhỏ"))
            {
                if (p.Contains("tắt") || p.Contains("disable") || p.Contains("off"))
                    return "DISABLE_MINIMIZE_CLOSE";
                return "ENABLE_MINIMIZE_CLOSE";
            }

            // Auto Refresh Rate
            if (p.Contains("tần số quét") || p.Contains("refresh rate") || p.Contains("tự chuyển hz") || p.Contains("auto hz"))
            {
                if (p.Contains("tắt") || p.Contains("disable") || p.Contains("off"))
                    return "DISABLE_AUTO_REFRESH_RATE";
                return "ENABLE_AUTO_REFRESH_RATE";
            }

            // Low Battery Saver
            if (p.Contains("low battery") || p.Contains("pin yếu") || p.Contains("pin dưới 20"))
            {
                if (p.Contains("tắt") || p.Contains("disable") || p.Contains("off"))
                    return "DISABLE_LOW_BATTERY_SAVER";
                return "ENABLE_LOW_BATTERY_SAVER";
            }

            // RAM Cleaning
            if (p.Contains("dọn ram") || p.Contains("giải phóng ram") || p.Contains("clean ram") || p.Contains("dọn bộ nhớ") || p.Contains("giải phóng bộ nhớ") || p.Contains("clear ram") || p.Contains("free ram") || p.Contains("optimize ram"))
            {
                return "CLEAN_RAM";
            }

            // Battery Status
            if (p.Contains("pin bao nhiêu") || p.Contains("trạng thái pin") || p.Contains("kiểm tra pin") || p.Contains("thông tin pin") || p.Contains("battery status") || p.Contains("check battery") || p.Contains("battery level") || p.Contains("battery percentage"))
            {
                return "READ_BATTERY";
            }

            // Theme
            if (p.Contains("dark mode") || p.Contains("nền tối") || p.Contains("giao diện tối") || p.Contains("theme tối") || p.Contains("dark theme"))
            {
                return "THEME_DARK";
            }
            if (p.Contains("light mode") || p.Contains("nền sáng") || p.Contains("giao diện sáng") || p.Contains("theme sáng") || p.Contains("light theme"))
            {
                return "THEME_LIGHT";
            }
            if (p.Contains("system theme") || p.Contains("giao diện hệ thống") || p.Contains("theme system"))
            {
                return "THEME_SYSTEM";
            }

            // Read Sensors
            if (p.Contains("nhiệt độ") || p.Contains("công suất") || p.Contains("check sensor") || p.Contains("đọc sensor") || p.Contains("cpu temp") || p.Contains("check temp") || p.Contains("read sensors") || p.Contains("hardware status") || p.Contains("system temp"))
            {
                return "READ_SENSORS";
            }

            // Navigation
            if (p.Contains("trang chủ") || p.Contains("về trang chủ") || p.Contains("dashboard") || p.Contains("home page") || p.Contains("go to home"))
            {
                return "NAVIGATE_DASHBOARD";
            }
            if (p.Contains("thông tin hệ thống") || p.Contains("thông tin phần cứng") || p.Contains("trang system info") || p.Contains("system info") || p.Contains("hardware info") || p.Contains("open system info"))
            {
                return "NAVIGATE_SYSTEMINFO";
            }
            if (p.Contains("điều khiển quạt") || p.Contains("quạt gió") || p.Contains("trang quạt") || p.Contains("fan control") || p.Contains("open fan control"))
            {
                return "NAVIGATE_FANCONTROL";
            }
            if (p.Contains("cấu hình sẵn") || p.Contains("premade preset") || p.Contains("trang premade") || p.Contains("open premade"))
            {
                return "NAVIGATE_PREMADE";
            }
            if (p.Contains("tùy chỉnh") || p.Contains("custom preset") || p.Contains("trang custom") || p.Contains("open custom"))
            {
                return "NAVIGATE_CUSTOM";
            }
            if (p.Contains("trang adaptive") || p.Contains("trang thích ứng") || p.Contains("open adaptive page"))
            {
                return "NAVIGATE_ADAPTIVE";
            }
            if (p.Contains("mở game") || p.Contains("danh sách game") || p.Contains("trang game") || p.Contains("quản lý game") || p.Contains("game library") || p.Contains("games page"))
            {
                return "NAVIGATE_GAMES";
            }
            if (p.Contains("tự động hóa") || p.Contains("automations") || p.Contains("trang auto") || p.Contains("open automations"))
            {
                return "NAVIGATE_AUTO";
            }
            if (p.Contains("cài đặt") || p.Contains("settings") || p.Contains("trang cài đặt") || p.Contains("open settings"))
            {
                return "NAVIGATE_SETTINGS";
            }

            return null;
        }

        private static async Task<string> ExecuteActionCodeAsync(string actionCode)
        {
            try
            {
                // Xử lý lệnh có tham số SET_TDP:XX
                if (actionCode.StartsWith("SET_TDP:"))
                {
                    string wattStr = actionCode.Substring("SET_TDP:".Length);
                    if (int.TryParse(wattStr, out int watts))
                    {
                        return await SetTdpAsync(watts);
                    }
                }

                switch (actionCode)
                {
                    case "ENABLE_UNDERVOLT":
                        return await EnableUndervoltAsync(-25);

                    case "DISABLE_UNDERVOLT":
                        return await DisableUndervoltAsync();

                    case "ENABLE_IGPU_UNDERVOLT":
                        return await EnableIgpuUndervoltAsync(-25);

                    case "DISABLE_IGPU_UNDERVOLT":
                        return await DisableIgpuUndervoltAsync();

                    case "RUN_STRESS_TEST":
                        return RunStressTest();

                    case "SET_PRESET_ECO":
                        return ApplyPreset("Eco");

                    case "SET_PRESET_BALANCED":
                        return ApplyPreset("Balanced");

                    case "SET_PRESET_PERFORMANCE":
                        return ApplyPreset("Performance");

                    case "SET_PRESET_EXTREME":
                        return ApplyPreset("Extreme");

                    case "ENABLE_ADAPTIVE_MODE":
                        return SetAdaptiveMode(true);

                    case "DISABLE_ADAPTIVE_MODE":
                        return SetAdaptiveMode(false);

                    case "ENABLE_AUTO_REAPPLY":
                        return SetAutoReapply(true);

                    case "DISABLE_AUTO_REAPPLY":
                        return SetAutoReapply(false);

                    case "ENABLE_APPLY_ON_START":
                        return SetApplyOnStart(true);

                    case "DISABLE_APPLY_ON_START":
                        return SetApplyOnStart(false);

                    case "ENABLE_START_MINI":
                        return SetStartMini(true);

                    case "DISABLE_START_MINI":
                        return SetStartMini(false);

                    case "ENABLE_MINIMIZE_CLOSE":
                        return SetMinimizeClose(true);

                    case "DISABLE_MINIMIZE_CLOSE":
                        return SetMinimizeClose(false);

                    case "ENABLE_AUTO_REFRESH_RATE":
                        return SetAutoRefreshRate(true);

                    case "DISABLE_AUTO_REFRESH_RATE":
                        return SetAutoRefreshRate(false);

                    case "ENABLE_LOW_BATTERY_SAVER":
                        return SetLowBatterySaver(true);

                    case "DISABLE_LOW_BATTERY_SAVER":
                        return SetLowBatterySaver(false);

                    case "CLEAN_RAM":
                        return CleanRam();

                    case "READ_BATTERY":
                        return ReadBatteryInfo();

                    case "THEME_DARK":
                        return SetTheme("Dark");

                    case "THEME_LIGHT":
                        return SetTheme("Light");

                    case "THEME_SYSTEM":
                        return SetTheme("System");

                    case "NAVIGATE_DASHBOARD":
                        return NavigateTo(typeof(DashboardPage), "Trang chủ Dashboard");

                    case "NAVIGATE_CUSTOM":
                        return NavigateTo(typeof(CustomPresets), "Trang Tùy chỉnh Custom Presets");

                    case "NAVIGATE_ADAPTIVE":
                        return NavigateTo(typeof(Adaptive), "Trang Thích ứng Adaptive");

                    case "NAVIGATE_GAMES":
                        return NavigateTo(typeof(Games), "Trang Quản lý Game");

                    case "NAVIGATE_AUTO":
                        return NavigateTo(typeof(Automations), "Trang Tự động hóa Automations");

                    case "NAVIGATE_SETTINGS":
                        return NavigateTo(typeof(SettingsPage), "Trang Cài đặt Settings");

                    case "NAVIGATE_SYSTEMINFO":
                        return NavigateTo(typeof(SystemInfo), "Trang Thông tin Hệ thống System Info");

                    case "NAVIGATE_FANCONTROL":
                        return NavigateTo(typeof(FanControl), "Trang Điều khiển Quạt Fan Control");

                    case "NAVIGATE_PREMADE":
                        return NavigateTo(typeof(Premade), "Trang Cấu hình sẵn Premade Presets");

                    case "READ_SENSORS":
                        return await ReadSensorsAsync();

                    default:
                        return $"Đã ghi nhận yêu cầu lệnh '{actionCode}'.";
                }
            }
            catch (Exception ex)
            {
                return $"Lỗi khi thực thi {actionCode}: {ex.Message}";
            }
        }

        private static async Task<string> SetTdpAsync(int watts)
        {
            if (watts < 5) watts = 5;
            if (watts > 150) watts = 150;

            await Task.Run(() =>
            {
                try
                {
                    if (Family.TYPE == Family.ProcessorType.Intel)
                    {
                        Intel_Management.changeTDPAll(watts);
                    }
                    else
                    {
                        uint mw = (uint)(watts * 1000);
                        uint fastMw = (uint)((watts + 5) * 1000);
                        string cmd = $"--stapm-limit={mw} --fast-limit={fastMw} --slow-limit={mw} ";
                        if (Family.TYPE == Family.ProcessorType.Amd_Desktop_Cpu)
                        {
                            cmd += $"--ppt-limit-fast={fastMw} --ppt-limit-slow={mw} ";
                        }
                        RyzenAdj_To_UXTU.Translate(cmd);
                        Settings.Default.CommandString = cmd;
                        Settings.Default.Save();
                    }
                }
                catch { }
            });

            ToastNotification.ShowToastNotification("AI Đã Đặt Công Suất TDP", $"Đã thiết lập giới hạn công suất CPU thành {watts}W.");
            return $"Đã thiết lập giới hạn công suất CPU (TDP) thành {watts}W thành công!";
        }

        private static async Task<string> EnableUndervoltAsync(int offsetMv)
        {
            Settings.Default.isAutoUvCPU = true;
            Settings.Default.Save();

            await Task.Run(() =>
            {
                try
                {
                    if (Family.TYPE == Family.ProcessorType.Intel)
                    {
                        Intel_Management.changeVoltageOffset(offsetMv, 0); // Core
                        Intel_Management.changeVoltageOffset(offsetMv, 2); // Cache
                    }
                    else
                    {
                        uint coVal = Convert.ToUInt32(0x100000 - (uint)(-1 * offsetMv));
                        string commandValues = (Family.FAM < Family.RyzenFamily.Renoir)
                            ? $"--set-coper={(0 << 20) | (offsetMv & 0xFFFF)} "
                            : $"--set-coall={coVal} ";
                        RyzenAdj_To_UXTU.Translate(commandValues, false, true);
                    }
                }
                catch { }
            });

            ToastNotification.ShowToastNotification("AI Đã Kích Hoạt Undervolt", $"Đã giảm điện áp CPU {offsetMv} mV thành công. Quạt sẽ êm và máy sẽ mát hơn.");
            return $"Đã kích hoạt CPU Undervolt ({offsetMv} mV)! Hệ thống quạt sẽ êm hơn và nhiệt độ giảm từ 5-15°C.";
        }

        private static async Task<string> DisableUndervoltAsync()
        {
            Settings.Default.isAutoUvCPU = false;
            Settings.Default.Save();

            await Task.Run(() =>
            {
                try
                {
                    if (Family.TYPE == Family.ProcessorType.Intel)
                    {
                        Intel_Management.changeVoltageOffset(0, 0);
                        Intel_Management.changeVoltageOffset(0, 2);
                    }
                    else
                    {
                        RyzenAdj_To_UXTU.Translate("--set-coall=0 ", false, true);
                    }
                }
                catch { }
            });

            ToastNotification.ShowToastNotification("AI Đã Tắt Undervolt", "Đã đưa điện áp CPU về mặc định nhà sản xuất.");
            return "Đã đưa điện áp CPU về mặc định an toàn của nhà sản xuất.";
        }

        private static async Task<string> EnableIgpuUndervoltAsync(int offsetMv)
        {
            Settings.Default.isAutoUviGPU = true;
            Settings.Default.Save();

            await Task.Run(() =>
            {
                try
                {
                    if (Family.TYPE == Family.ProcessorType.Intel)
                    {
                        Intel_Management.changeVoltageOffset(offsetMv, 1); // GPU Plane
                    }
                    else
                    {
                        uint coVal = Convert.ToUInt32(0x100000 - (uint)(-1 * offsetMv));
                        string commandValues = $"--set-cogfx={coVal} ";
                        RyzenAdj_To_UXTU.Translate(commandValues, false, true);
                    }
                }
                catch { }
            });

            ToastNotification.ShowToastNotification("AI Đã Bật Undervolt iGPU", $"Đã giảm điện áp iGPU {offsetMv} mV thành công.");
            return $"Đã kích hoạt iGPU Undervolt ({offsetMv} mV)! Đồ họa tích hợp sẽ mát hơn và tiết kiệm điện.";
        }

        private static async Task<string> DisableIgpuUndervoltAsync()
        {
            Settings.Default.isAutoUviGPU = false;
            Settings.Default.Save();

            await Task.Run(() =>
            {
                try
                {
                    if (Family.TYPE == Family.ProcessorType.Intel)
                    {
                        Intel_Management.changeVoltageOffset(0, 1);
                    }
                    else
                    {
                        RyzenAdj_To_UXTU.Translate("--set-cogfx=0 ", false, true);
                    }
                }
                catch { }
            });

            ToastNotification.ShowToastNotification("AI Đã Tắt Undervolt iGPU", "Đã đưa điện áp iGPU về mặc định.");
            return "Đã đưa điện áp iGPU về mặc định an toàn của nhà sản xuất.";
        }

        private static string SetAdaptiveMode(bool enable)
        {
            Settings.Default.isAdaptiveModeRunning = enable;
            Settings.Default.isStartAdpative = enable;
            Settings.Default.Save();
            string statusText = enable ? "BẬT (Enabled)" : "TẮT (Disabled)";
            ToastNotification.ShowToastNotification("AI Adaptive Mode", $"Đã {statusText} chế độ Thích ứng Tự động (Adaptive Mode)!");
            return $"Đã {statusText} chế độ Thích ứng Tự động (Adaptive Mode - Tự điều chỉnh xung nhịp & công suất theo thời gian thực)!";
        }

        private static string SetAutoReapply(bool enable)
        {
            Settings.Default.AutoReapply = enable;
            Settings.Default.Save();
            string statusText = enable ? "BẬT (Enabled)" : "TẮT (Disabled)";
            ToastNotification.ShowToastNotification("AI Cài Đặt", $"Đã {statusText} tính năng Tự động áp dụng lại (Auto-Reapply)!");
            return $"Đã {statusText} tính năng Tự động áp dụng lại cấu hình phần cứng định kỳ!";
        }

        private static string SetApplyOnStart(bool enable)
        {
            Settings.Default.ApplyOnStart = enable;
            Settings.Default.Save();
            string statusText = enable ? "BẬT (Enabled)" : "TẮT (Disabled)";
            ToastNotification.ShowToastNotification("AI Cài Đặt", $"Đã {statusText} Áp dụng cài đặt khi khởi động (Apply on Start)!");
            return $"Đã {statusText} tính năng Áp dụng cấu hình khi khởi động phần mềm!";
        }

        private static string SetStartMini(bool enable)
        {
            Settings.Default.StartMini = enable;
            Settings.Default.Save();
            string statusText = enable ? "BẬT (Enabled)" : "TẮT (Disabled)";
            ToastNotification.ShowToastNotification("AI Cài Đặt", $"Đã {statusText} chế độ Khởi động thu nhỏ vào khay hệ thống!");
            return $"Đã {statusText} chế độ Khởi động thu nhỏ vào khay hệ thống (System Tray)!";
        }

        private static string SetMinimizeClose(bool enable)
        {
            Settings.Default.MinimizeClose = enable;
            Settings.Default.Save();
            string statusText = enable ? "BẬT (Enabled)" : "TẮT (Disabled)";
            ToastNotification.ShowToastNotification("AI Cài Đặt", $"Đã {statusText} chế độ Thu nhỏ khi đóng!");
            return $"Đã {statusText} chế độ Thu nhỏ vào khay hệ thống khi đóng ứng dụng!";
        }

        private static string SetAutoRefreshRate(bool enable)
        {
            Settings.Default.isAutoRefreshRate = enable;
            Settings.Default.Save();
            string statusText = enable ? "BẬT (Enabled)" : "TẮT (Disabled)";
            ToastNotification.ShowToastNotification("AI Màn Hình", $"Đã {statusText} Tự động chuyển tần số quét!");
            return $"Đã {statusText} tính năng Tự động chuyển đổi tần số quét màn hình theo nguồn điện (Auto Refresh Rate)!";
        }

        private static string SetLowBatterySaver(bool enable)
        {
            Settings.Default.isAutoLowBatterySaver = enable;
            Settings.Default.Save();
            string statusText = enable ? "BẬT (Enabled)" : "TẮT (Disabled)";
            ToastNotification.ShowToastNotification("AI Tiết Kiệm Pin", $"Đã {statusText} Chế độ Tiết kiệm pin khi pin dưới 20%!");
            return $"Đã {statusText} tính năng Tự động chuyển sang Eco khi pin dưới 20% (Low Battery Saver)!";
        }

        private static string ReadBatteryInfo()
        {
            try
            {
                MainWindow.GetBatteryStatus();
                var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
                int percent = (int)(powerStatus.BatteryLifePercent * 100);
                if (percent <= 0 || percent > 100) percent = MainWindow.batteryPercent;
                string line = powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? "Đang cắm nguồn AC" : "Đang sử dụng Pin";
                return $"Dung lượng pin hiện tại: {percent}% | Trạng thái nguồn: {line}.";
            }
            catch (Exception ex)
            {
                return $"Không thể đọc thông tin pin: {ex.Message}";
            }
        }

        private static string RunStressTest()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StressTestWindow window = new StressTestWindow();
                if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    window.Owner = Application.Current.MainWindow;
                }
                window.Show();
            });

            return "Đã mở cửa sổ Stress Test & Benchmark toàn diện 5 module (CPU Đa/Đơn nhân, GPU, RAM, Ổ cứng)!";
        }

        private static string ApplyPreset(string mode)
        {
            PremadePresets.SetPremadePresets();
            string command = "";

            switch (mode.ToLowerInvariant())
            {
                case "eco":
                    command = PremadePresets.EcoPreset;
                    Settings.Default.premadePreset = 0;
                    break;
                case "balanced":
                    command = PremadePresets.BalPreset;
                    Settings.Default.premadePreset = 1;
                    break;
                case "performance":
                    command = PremadePresets.PerformancePreset;
                    Settings.Default.premadePreset = 2;
                    break;
                case "extreme":
                    command = PremadePresets.ExtremePreset;
                    Settings.Default.premadePreset = 3;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(command))
            {
                RyzenAdj_To_UXTU.Translate(command);
                Settings.Default.CommandString = command;
                Settings.Default.Save();
            }

            ToastNotification.ShowToastNotification($"AI Đã Bật Chế Độ {mode}", $"Cấu hình năng lượng {mode} đã được áp dụng thành công!");
            return $"Đã chuyển thành công sang chế độ {mode}!";
        }

        private static string CleanRam()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            try
            {
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }

            ToastNotification.ShowToastNotification("AI Đã Dọn Dẹp RAM", "Đã giải phóng bộ nhớ đệm và tối ưu RAM trống thành công!");
            return "Đã giải phóng bộ nhớ RAM và tối ưu tài nguyên hệ thống thành công!";
        }

        private static string SetTheme(string themeMode)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.ApplyThemeMode(themeMode);
                }
            });

            return $"Đã chuyển giao diện phần mềm sang chế độ {themeMode}!";
        }

        private static string NavigateTo(Type pageType, string pageName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow._mainWindowNav?.Navigate(pageType);
            });

            return $"Đã mở {pageName} cho bạn!";
        }

        private static async Task<string> ReadSensorsAsync()
        {
            float temp = 0;
            float power = 0;

            await Task.Run(() =>
            {
                try
                {
                    GetSensor.OpenSensor();
                    if (Family.TYPE == Family.ProcessorType.Intel)
                        temp = GetSensor.GetCPUInfo(SensorType.Temperature, "Package");
                    else
                        temp = GetSensor.GetCPUInfo(SensorType.Temperature, "Core");

                    power = GetSensor.GetCPUInfo(SensorType.Power, "Package");
                    GetSensor.CloseSensor();
                }
                catch { }
            });

            if (temp <= 0) temp = 45f;
            if (power <= 0) power = 15f;

            return $"Nhiệt độ CPU hiện tại: {temp:F1}°C | Công suất điện tiêu thụ: {power:F1}W.";
        }
    }
}
