using System;
using System.Windows;
using Universal_x86_Tuning_Utility.Scripts.Misc;
using Universal_x86_Tuning_Utility.Views.Windows;

namespace Universal_x86_Tuning_Utility.Services
{
    public static class StressTestRunner
    {
        public static void StartOrToggleStressTest()
        {
            try
            {
                // Confirmation dialog
                MessageBoxResult result = MessageBox.Show(
                    "Bạn có chắc chắn muốn chạy bài kiểm tra quá tải (Stress Test) và chấm điểm hiệu năng toàn diện không?\n\n" +
                    "Bài kiểm tra chuyên sâu bao gồm:\n" +
                    "• CPU Đa nhân (100% công suất tải AVX2 / FP64 đa luồng)\n" +
                    "• CPU Đơn nhân (Đo xung nhịp đỉnh IPC Single-Core)\n" +
                    "• GPU Đồ họa nặng (Direct3D 3D Graphics & Heavy Compute Pipeline)\n" +
                    "• Bộ nhớ RAM (Đo tốc độ băng thông GB/s & Kiểm tra sức khỏe bit)\n" +
                    "• Ổ cứng SSD/NVMe (Đo tốc độ Đọc/Ghi thực tế MB/s & Kiểm tra SMART)\n\n" +
                    "Nhấn 'Yes' để bắt đầu hoặc 'No' để hủy.",
                    "Xác nhận Stress Test & Benchmark Toàn Diện",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    // User clicked No or Cancel -> cancel and do not run
                    return;
                }

                // User clicked Yes -> launch StressTestWindow
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StressTestWindow window = new StressTestWindow();
                    if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
                    {
                        window.Owner = Application.Current.MainWindow;
                    }
                    window.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, "Failed in StartOrToggleStressTest");
            }
        }
    }
}
