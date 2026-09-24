using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class GeminiService
{
    private static readonly string ApiKey = "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";
    private static readonly string Endpoint = "https://api.groq.com/openai/v1/chat/completions";

    // Primary highest & smartest model on Groq
    private static readonly string PrimaryModel = "openai/gpt-oss-120b";
    // Fallback models if primary model is rate limited or unavailable
    private static readonly string[] FallbackModels = new[] { "openai/gpt-oss-20b", "qwen/qwen3.6-27b" };

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static readonly string FullControlSystemPrompt =
        "Bạn là Trợ Lý AI VeloX tích hợp trong Universal x86 Tuning Utility (VeloX Utility). " +
        "You are VeloX AI Assistant integrated into Universal x86 Tuning Utility. " +
        "Bạn có TOÀN QUYỀN ĐIỀU KHIỂN trực tiếp mọi ngóc ngách, từng tính năng và thông số chi tiết của phần mềm.\n\n" +
        "QUY TẮC BẮT BUỘC: Khi người dùng yêu cầu bật, tắt, hoặc điều chỉnh bất kỳ tính năng hoặc thông số nào (bằng tiếng Việt hoặc tiếng Anh), bạn PHẢI đính kèm mã lệnh [ACTION:...] tương ứng ở đầu câu trả lời. Dưới đây là danh mục toàn bộ các lệnh điều khiển chi tiết của phần mềm:\n\n" +
        "=== 1. ADAPTIVE MODE (CHẾ ĐỘ THÍCH ỨNG) ===\n" +
        "- [ACTION:ENABLE_AUTO_SWITCH] : Bật tính năng 'Auto Switch' (tự động chuyển đổi profile theo game/ứng dụng). Chú ý: Đây là tùy chọn 'Auto switch app profile', KHÔNG PHẢI là bật toàn bộ Adaptive Mode!\n" +
        "- [ACTION:DISABLE_AUTO_SWITCH] : Tắt tính năng 'Auto Switch' (tắt tự động chuyển profile).\n" +
        "- [ACTION:ENABLE_ADAPTIVE_MODE] : Bắt đầu chạy tiến trình Adaptive Mode (Start Adaptive Mode).\n" +
        "- [ACTION:DISABLE_ADAPTIVE_MODE] : Dừng tiến trình Adaptive Mode (Stop Adaptive Mode).\n" +
        "- [ACTION:SET_POLLING_RATE:X] : Đặt tần số lấy mẫu Polling Rate (từ 0.5 đến 8.0 giây, ví dụ: [ACTION:SET_POLLING_RATE:1.5]).\n" +
        "- [ACTION:SET_TARGET_TEMP:XX] : Đặt giới hạn nhiệt độ mục tiêu tối đa (°C, ví dụ: [ACTION:SET_TARGET_TEMP:85]).\n" +
        "- [ACTION:SET_MIN_CPU_CLOCK:XXXX] : Đặt xung nhịp tối thiểu CPU (MHz, ví dụ: [ACTION:SET_MIN_CPU_CLOCK:1500]).\n" +
        "- [ACTION:SET_MAX_GFX_CLOCK:XXXX] : Đặt xung nhịp tối đa đồ họa tích hợp iGPU (MHz, ví dụ: [ACTION:SET_MAX_GFX_CLOCK:1900]).\n" +
        "- [ACTION:SET_MIN_GFX_CLOCK:XXXX] : Đặt xung nhịp tối thiểu đồ họa tích hợp iGPU (MHz, ví dụ: [ACTION:SET_MIN_GFX_CLOCK:400]).\n" +
        "- [ACTION:ENABLE_AUTO_RESTORE] / [ACTION:DISABLE_AUTO_RESTORE] : Bật/tắt tự động khôi phục cài đặt khi thoát game.\n\n" +
        "=== 2. UNDERVOLT & CÔNG SUẤT (TDP / VOLTAGE) ===\n" +
        "- [ACTION:ENABLE_UNDERVOLT] : Bật CPU Undervolt giảm điện áp và nhiệt độ máy (-25 mV mặc định).\n" +
        "- [ACTION:DISABLE_UNDERVOLT] : Đưa điện áp CPU về mặc định nhà sản xuất.\n" +
        "- [ACTION:SET_UNDERVOLT:XX] : Đặt mức giảm điện áp CPU chính xác (mV, ví dụ: [ACTION:SET_UNDERVOLT:-35]).\n" +
        "- [ACTION:ENABLE_IGPU_UNDERVOLT] : Bật Undervolt cho đồ họa tích hợp iGPU (-25 mV).\n" +
        "- [ACTION:DISABLE_IGPU_UNDERVOLT] : Đưa điện áp iGPU về mặc định.\n" +
        "- [ACTION:SET_IGPU_UNDERVOLT:XX] : Đặt mức giảm điện áp iGPU chính xác (mV, ví dụ: [ACTION:SET_IGPU_UNDERVOLT:-30]).\n" +
        "- [ACTION:SET_TDP:XX] : Đặt giới hạn công suất TDP thành XX Watt (ví dụ: [ACTION:SET_TDP:35]).\n" +
        "- [ACTION:SET_INTEL_PL1:XX] : Đặt công suất duy trì dài hạn PL1 cho CPU Intel (Watt, ví dụ: [ACTION:SET_INTEL_PL1:35]).\n" +
        "- [ACTION:SET_INTEL_PL2:XX] : Đặt công suất tối đa ngắn hạn PL2 cho CPU Intel (Watt, ví dụ: [ACTION:SET_INTEL_PL2:55]).\n\n" +
        "=== 3. PRESETS NĂNG LƯỢNG (CHẾ ĐỘ CÔNG SUẤT) ===\n" +
        "- [ACTION:SET_PRESET_ECO] : Bật chế độ Tiết Kiệm Pin (Eco Mode) - êm ái, quạt mát.\n" +
        "- [ACTION:SET_PRESET_BALANCED] : Bật chế độ Cân Bằng (Balanced Mode).\n" +
        "- [ACTION:SET_PRESET_PERFORMANCE] : Bật chế độ Hiệu Năng Cao (Performance Mode) - tăng xung nhịp chơi game.\n" +
        "- [ACTION:SET_PRESET_EXTREME] : Bật chế độ Cực Hạn (Extreme Mode) - mở khóa toàn bộ công suất.\n\n" +
        "=== 4. TỰ ĐỘNG HÓA (AUTOMATIONS) ===\n" +
        "- [ACTION:ENABLE_AUTO_REFRESH_RATE] / [ACTION:DISABLE_AUTO_REFRESH_RATE] : Bật/tắt tự chuyển tần số quét (Hz) màn hình khi cắm/rút sạc.\n" +
        "- [ACTION:ENABLE_LOW_BATTERY_SAVER] / [ACTION:DISABLE_LOW_BATTERY_SAVER] : Bật/tắt tự động chuyển chế độ Eco khi pin dưới 20%.\n" +
        "- [ACTION:ENABLE_CPU_AUTOUV] / [ACTION:DISABLE_CPU_AUTOUV] : Bật/tắt AutoOC thích ứng điện áp CPU.\n" +
        "- [ACTION:ENABLE_IGPU_AUTOUV] / [ACTION:DISABLE_IGPU_AUTOUV] : Bật/tắt AutoOC thích ứng điện áp iGPU.\n" +
        "- [ACTION:SET_CHARGE_PRESET:PRESET] : Đặt preset khi cắm sạc (Eco, Balanced, Performance, Extreme).\n" +
        "- [ACTION:SET_DISCHARGE_PRESET:PRESET] : Đặt preset khi dùng pin (Eco, Balanced, Performance, Extreme).\n" +
        "- [ACTION:SET_RESUME_PRESET:PRESET] : Đặt preset khi thức dậy từ sleep (Eco, Balanced, Performance, Extreme).\n\n" +
        "=== 5. CÀI ĐẶT HỆ THỐNG (SETTINGS) ===\n" +
        "- [ACTION:ENABLE_START_ON_BOOT] / [ACTION:DISABLE_START_ON_BOOT] : Bật/tắt khởi động cùng Windows (Logon Task).\n" +
        "- [ACTION:ENABLE_START_MINI] / [ACTION:DISABLE_START_MINI] : Bật/tắt khởi động thu nhỏ vào khay hệ thống (Start Minimized).\n" +
        "- [ACTION:ENABLE_MINIMIZE_CLOSE] / [ACTION:DISABLE_MINIMIZE_CLOSE] : Bật/tắt thu nhỏ vào khay hệ thống khi đóng (Minimize on Close).\n" +
        "- [ACTION:ENABLE_APPLY_ON_START] / [ACTION:DISABLE_APPLY_ON_START] : Bật/tắt tự áp dụng cấu hình khi khởi động (Apply on Start).\n" +
        "- [ACTION:ENABLE_AUTO_REAPPLY] / [ACTION:DISABLE_AUTO_REAPPLY] : Bật/tắt tự động nạp lại cấu hình phần cứng định kỳ.\n" +
        "- [ACTION:SET_AUTO_REAPPLY_TIME:X] : Đặt chu kỳ nạp lại cấu hình (giây, ví dụ: [ACTION:SET_AUTO_REAPPLY_TIME:3]).\n" +
        "- [ACTION:ENABLE_PERF_TRACKING] / [ACTION:DISABLE_PERF_TRACKING] : Bật/tắt theo dõi thông số qua RTSS.\n\n" +
        "=== 6. SUPER RESOLUTION (MAGPIE UPSCALING) ===\n" +
        "- [ACTION:ENABLE_SUPER_RESOLUTION] / [ACTION:DISABLE_SUPER_RESOLUTION] : Bật/tắt tính năng Super Resolution (Magpie).\n" +
        "- [ACTION:SET_SHARPNESS:X.X] : Đặt độ sắc nét (0.1 đến 1.0, ví dụ: [ACTION:SET_SHARPNESS:0.8]).\n" +
        "- [ACTION:ENABLE_VSYNC] / [ACTION:DISABLE_VSYNC] : Bật/tắt VSync trong Super Resolution.\n" +
        "- [ACTION:ENABLE_SHOW_FPS] / [ACTION:DISABLE_SHOW_FPS] : Bật/tắt hiển thị FPS overlay.\n" +
        "- [ACTION:SET_SCALE_MODE:MODE] : Đặt thuật toán upscale (FSR, NIS, Anime4K, Bicubic, Bilinear, Lancaster).\n\n" +
        "=== 7. MÀN HÌNH & ASUS LAPTOP ===\n" +
        "- [ACTION:SET_REFRESH_RATE:HZ] : Đặt tần số quét màn hình thành HZ (ví dụ: [ACTION:SET_REFRESH_RATE:60], [ACTION:SET_REFRESH_RATE:120], [ACTION:SET_REFRESH_RATE:144]).\n" +
        "- [ACTION:SET_ASUS_MODE:MODE] : Chỉnh chế độ quạt ASUS (Silent, Balanced, Turbo).\n" +
        "- [ACTION:SET_ASUS_GPU_ECO:BOOL] : Bật/tắt chế độ ngắt dGPU tiết kiệm pin ASUS.\n\n" +
        "=== 8. CÔNG CỤ & ĐIỀU HƯỚNG (TOOLS & NAVIGATION) ===\n" +
        "- [ACTION:CLEAN_RAM] : Dọn dẹp và giải phóng bộ nhớ RAM ngay lập tức.\n" +
        "- [ACTION:RUN_STRESS_TEST] : Mở bài kiểm tra quá tải Stress Test & Benchmark 5 module.\n" +
        "- [ACTION:READ_SENSORS] : Đọc nhiệt độ và công suất phần cứng CPU hiện tại.\n" +
        "- [ACTION:READ_BATTERY] : Đọc trạng thái sạc và dung lượng pin hiện tại.\n" +
        "- [ACTION:THEME_DARK] / [ACTION:THEME_LIGHT] / [ACTION:THEME_SYSTEM] : Chuyển giao diện.\n" +
        "- [ACTION:NAVIGATE_DASHBOARD] : Mở trang chủ Dashboard.\n" +
        "- [ACTION:NAVIGATE_CUSTOM] : Mở trang Tùy chỉnh Custom Presets.\n" +
        "- [ACTION:NAVIGATE_ADAPTIVE] : Mở trang Thích ứng Adaptive Presets.\n" +
        "- [ACTION:NAVIGATE_GAMES] : Mở trang Quản lý Game (Games Library).\n" +
        "- [ACTION:NAVIGATE_AUTO] : Mở trang Tự động hóa Automations.\n" +
        "- [ACTION:NAVIGATE_SETTINGS] : Mở trang Cài đặt Settings.\n" +
        "- [ACTION:NAVIGATE_SYSTEMINFO] : Mở trang Thông tin phần cứng System Info.\n" +
        "- [ACTION:NAVIGATE_FANCONTROL] : Mở trang Điều khiển Quạt Fan Control.\n" +
        "- [ACTION:NAVIGATE_PREMADE] : Mở trang Cấu hình sẵn Premade Presets.\n\n" +
        "LƯU Ý QUAN TRỌNG: Hãy phân biệt thật chính xác giữa các tính năng tương tự nhau. Ví dụ: 'Auto switch' là [ACTION:ENABLE_AUTO_SWITCH], không phải Adaptive Mode! Phản hồi thân thiện, súc tích bằng ngôn ngữ của người dùng và xác nhận rõ ràng cài đặt đã điều chỉnh.";

    private static readonly List<object> ChatHistory = new List<object>
    {
        new { role = "system", content = FullControlSystemPrompt }
    };

    private static readonly object HistoryLock = new object();

    public static void ClearHistory()
    {
        lock (HistoryLock)
        {
            ChatHistory.Clear();
            ChatHistory.Add(new { role = "system", content = FullControlSystemPrompt });
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        client.DefaultRequestHeaders.Add("User-Agent", "VeloX-Utility/26.2.0");
        return client;
    }

    public static async Task<string> GetHardwareAdviceAsync(float cpuTemp, float powerLimitWatt)
    {
        string prompt = $"Máy tính hiện tại có CPU nhiệt độ {cpuTemp:F1}°C và giới hạn điện {powerLimitWatt:F1}W. Hãy đưa ra 1 lời khuyên ngắn gọn (dưới 30 từ) bằng tiếng Việt để tối ưu pin hoặc tản nhiệt.";

        var messages = new List<object>
        {
            new { role = "system", content = "Bạn là Trợ Lý AI VeloX phần cứng máy tính. Hãy đưa ra lời khuyên ngắn gọn, đúng trọng tâm (dưới 30 từ) bằng tiếng Việt." },
            new { role = "user", content = prompt }
        };

        string response = await SendRequestAsync(messages, maxTokens: 150, temperature: 0.5f);
        return string.IsNullOrWhiteSpace(response) ? "Nhiệt độ và điện áp hiện tại ở mức bình thường. Hãy duy trì tản nhiệt tốt!" : response.Trim();
    }

    public static async Task<string> ChatAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return "Vui lòng nhập tin nhắn.";

        List<object> messagesToSend;
        lock (HistoryLock)
        {
            ChatHistory.Add(new { role = "user", content = userMessage });
            // Keep recent messages to prevent overly large payload while maintaining multi-turn context
            if (ChatHistory.Count > 25)
            {
                // Preserve system prompt at index 0, remove older turns
                ChatHistory.RemoveRange(1, ChatHistory.Count - 25);
            }
            messagesToSend = new List<object>(ChatHistory);
        }

        string response = await SendRequestAsync(messagesToSend, maxTokens: 1500, temperature: 0.7f);

        if (!string.IsNullOrWhiteSpace(response))
        {
            lock (HistoryLock)
            {
                ChatHistory.Add(new { role = "assistant", content = response.Trim() });
            }
            return response.Trim();
        }

        return "Không thể nhận phản hồi từ AI. Vui lòng thử lại sau.";
    }

    private static async Task<string> SendRequestAsync(List<object> messages, int maxTokens, float temperature)
    {
        var modelsToTry = new List<string> { PrimaryModel };
        modelsToTry.AddRange(FallbackModels);

        string lastError = string.Empty;

        foreach (var model in modelsToTry)
        {
            try
            {
                var payload = new
                {
                    model = model,
                    messages = messages,
                    max_tokens = maxTokens,
                    temperature = temperature
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await HttpClient.PostAsync(Endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(responseJson);

                    if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out JsonElement messageElement) &&
                            messageElement.TryGetProperty("content", out JsonElement contentElement))
                        {
                            string aiText = contentElement.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(aiText))
                            {
                                return aiText;
                            }
                        }
                    }
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    lastError = $"HTTP {(int)response.StatusCode}: {errorBody}";
                    // If rate limited or server error, continue to fallback model
                    continue;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                // Try next fallback model
                continue;
            }
        }

        return $"Lỗi kết nối: {lastError}";
    }
}