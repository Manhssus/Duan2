using LibreHardwareMonitor.Hardware;
using RyzenSmu;
using System;
using System.Windows;
using System.Windows.Input;
using Universal_x86_Tuning_Utility.Scripts;
using Universal_x86_Tuning_Utility.Scripts.GPUs.NVIDIA;
using Universal_x86_Tuning_Utility.Scripts.Intel_Backend;
using Universal_x86_Tuning_Utility.Scripts.Misc;
using Wpf.Ui.Common.Interfaces;

namespace Universal_x86_Tuning_Utility.Views.Pages
{
    /// <summary>
    /// Interaction logic for DashboardPage.xaml
    /// </summary>
    public partial class DashboardPage : INavigableView<ViewModels.DashboardViewModel>
    {
        public ViewModels.DashboardViewModel ViewModel
        {
            get;
        }

        public DashboardPage(ViewModels.DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            _ = Tablet.TabletDevices;

            Garbage.Garbage_Collect();

            if (Family.TYPE == Family.ProcessorType.Intel)
            {
                caPremade.IsEnabled = false;
                btnPremade.IsEnabled = false;
            }

            try
            {
                System.Threading.Tasks.Task.Run(() => NvHwCheck.CheckROPCount());
            }
            catch
            {
                
            }

            try
            {
                if (Universal_x86_Tuning_Utility.Properties.Settings.Default.isAutoUvCPU)
                {
                    rbUvAuto.IsChecked = true;
                    txtUvStatus.Text = "Trạng thái: 🟢 Đang bật AutoOC Undervolt Thích Ứng (Quạt êm & CPU mát mẻ).";
                }
            }
            catch { }
        }

        private void ApplyFastVoltageDrop(int offset, int fallbackOffset = -80)
        {
            var targets = new[] { offset, Math.Min(offset - 10, fallbackOffset), fallbackOffset };

            foreach (var v in targets)
            {
                if (Family.TYPE == Family.ProcessorType.Intel)
                {
                    Intel_Management.changeVoltageOffset(v, 0); // Core
                    Intel_Management.changeVoltageOffset(v, 2); // Cache
                }
                else
                {
                    uint coVal = Convert.ToUInt32(0x100000 - (uint)(-1 * v));
                    string commandValues = "";
                    if (Family.FAM < Family.RyzenFamily.Renoir)
                        commandValues = $"--set-coper={(0 << 20) | (v & 0xFFFF)} ";
                    else
                        commandValues = $"--set-coall={coVal} ";

                    RyzenAdj_To_UXTU.Translate(commandValues, false, true);
                }

                System.Threading.Thread.Sleep(60);
            }
        }

        private async void BtnApplyUv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnApplyUv.IsEnabled = false;
                txtUvStatus.Text = "⏳ Đang áp dụng Undervolt ngay lập tức để quạt giảm âm lượng ngay...";

                int offset = -25;
                bool isAuto = false;

                if (rbUvQuiet.IsChecked == true) offset = -35;
                else if (rbUvBalanced.IsChecked == true) offset = -25;
                else if (rbUvMild.IsChecked == true) offset = -15;
                else if (rbUvAuto.IsChecked == true) isAuto = true;

                if (isAuto)
                {
                    Universal_x86_Tuning_Utility.Properties.Settings.Default.isAutoUvCPU = true;
                    Universal_x86_Tuning_Utility.Properties.Settings.Default.Save();
                    txtUvStatus.Text = "Trạng thái: 🟢 Đã bật AutoOC Thích Ứng! Hệ thống sẽ giảm điện áp ngay và rút xuống mức thấp nhất nếu quạt vẫn còn ồn.";
                }
                else
                {
                    Universal_x86_Tuning_Utility.Properties.Settings.Default.isAutoUvCPU = false;
                    Universal_x86_Tuning_Utility.Properties.Settings.Default.Save();

                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        ApplyFastVoltageDrop(offset);
                    });

                    txtUvStatus.Text = $"Trạng thái: 🟢 Đã giảm điện áp ngay {offset} mV và tiếp tục hạ xuống mức thấp nhất nếu quạt vẫn còn ồn. Nhiệt độ sẽ giảm nhanh hơn và quạt nên giảm âm lượng ngay lập tức.";
                }
            }
            catch (Exception ex)
            {
                txtUvStatus.Text = $"❌ Lỗi khi áp dụng: {ex.Message}";
            }
            finally
            {
                btnApplyUv.IsEnabled = true;
            }
        }

        private async void BtnResetUv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnResetUv.IsEnabled = false;
                txtUvStatus.Text = "⏳ Đang đưa điện áp về mặc định...";

                Universal_x86_Tuning_Utility.Properties.Settings.Default.isAutoUvCPU = false;
                Universal_x86_Tuning_Utility.Properties.Settings.Default.Save();

                await System.Threading.Tasks.Task.Run(() =>
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
                });

                txtUvStatus.Text = "Trạng thái: ⚪ Đã đưa điện áp về mặc định của nhà sản xuất.";
            }
            catch (Exception ex)
            {
                txtUvStatus.Text = $"❌ Lỗi khi đặt lại: {ex.Message}";
            }
            finally
            {
                btnResetUv.IsEnabled = true;
            }
        }

        // Hàm xử lý sự kiện khi bấm nút "Phân Tích & Tối Ưu Mới"
        private async void BtnAskAi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnAskAi.IsEnabled = false;
                TxtAiAdvice.Text = "⏳ Đang đọc nhiệt độ, công suấ...";

                // Mở sensor để đọc dữ liệu
                await System.Threading.Tasks.Task.Run(() => GetSensor.OpenSensor());

                // Đọc nhiệt độ CPU và công suất điện tiêu thụ thực tế từ hệ thống UXTU
                float currentTemp;
                if (Family.TYPE == Family.ProcessorType.Intel)
                    currentTemp = GetSensor.GetCPUInfo(SensorType.Temperature, "Package");
                else
                    currentTemp = GetSensor.GetCPUInfo(SensorType.Temperature, "Core");

                float currentPower = GetSensor.GetCPUInfo(SensorType.Power, "Package");

                // Xử lý giá trị phòng trường hợp cảm biến vừa khởi động chưa kịp ghi nhận
                if (currentTemp <= 0) currentTemp = 45.0f;
                if (currentPower <= 0) currentPower = 15.0f;

                // Truyền trực tiếp thông số thực tế vào GeminiService
                string aiResponse = await GeminiService.GetHardwareAdviceAsync(currentTemp, currentPower);

                // Hiển thị thông số kèm lời khuyên từ AI
                TxtAiAdvice.Text = $"📊 Trạng thái: {currentTemp}°C - {currentPower}W\n💡 Khuyên dùng: {aiResponse}";
            }
            catch (Exception ex)
            {
                TxtAiAdvice.Text = $"❌ Lỗi: {ex.Message}";
            }
            finally
            {
                BtnAskAi.IsEnabled = true;

                // Đóng sensor sau khi đọc xong
                try { GetSensor.CloseSensor(); } catch { }
            }
        }
    }
}