using Accord.Math.Distances;
using AutoOC.Controllers;
using AutoOC.Monitors;
using DuoVia.FuzzyStrings;
using GameLib.Plugin.RiotGames.Model;
using Gma.System.MouseKeyHook;
using HidSharp.Utility;
using Microsoft.Win32;
using RTSSSharedMemoryNET;
using RyzenSmu;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using Universal_x86_Tuning_Utility.Properties;
using Universal_x86_Tuning_Utility.Scripts;
using Universal_x86_Tuning_Utility.Scripts.Intel_Backend;
using Universal_x86_Tuning_Utility.Scripts.Misc;
using Universal_x86_Tuning_Utility.Scripts.UXTU_Super_Resolution;
using Universal_x86_Tuning_Utility.Services;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using static Universal_x86_Tuning_Utility.Scripts.Game_Manager;
using Application = System.Windows.Application;
using MessageBox = System.Windows.Forms.MessageBox;
using Settings = Universal_x86_Tuning_Utility.Properties.Settings;

namespace Universal_x86_Tuning_Utility.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public ViewModels.MainWindowViewModel ViewModel { get; set; }

        DispatcherTimer GC = new DispatcherTimer();
        DispatcherTimer Misc = new DispatcherTimer();
        public DispatcherTimer autoReapply = new DispatcherTimer();
        public DispatcherTimer autoRestore = new DispatcherTimer();

        public static MainWindow? Instance { get; private set; }
        public static bool isMini { get; private set; }
        public static NavigationStore _mainWindowNav;
        private static INavigationService _navigationService;
        private static bool _firstRun = true;
        private static List<GameLauncherItem> _gamesList;
        private int _garbageCounter;

        public MainWindow(ViewModels.MainWindowViewModel viewModel, IPageService pageService, INavigationService navigationService)
        {
            Instance = this;
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();

            _navigationService = navigationService;
            _mainWindowNav = RootNavigation;

            GC.Interval = TimeSpan.FromSeconds(2);
            GC.Tick += GC_Tick;
            GC.Start();

            Misc.Interval = TimeSpan.FromSeconds(1);
            Misc.Tick += Misc_Tick;
            Misc.Start();

            autoReapply.Interval = TimeSpan.FromSeconds((int)Settings.Default.AutoReapplyTime);
            autoReapply.Tick += AutoReapply_Tick;
            autoReapply.Start();

            autoRestore.Interval = TimeSpan.FromSeconds(1);
            autoRestore.Tick += Controller.AutoRestore_Tick;
            autoRestore.Start();

            SetupNavigationService(pageService);

            SetupUI();
            ApplyOnStart();

            SystemEvents.PowerModeChanged += HandlePowerModeChange;
        }

        private void SetupNavigationService(IPageService pageService)
        {
            _navigationService.SetNavigationControl(RootNavigation);
            RootNavigation.PageService = pageService;
        }

        private void SetupUI()
        {
            tbMain.Title = $"VeloX Utility - {Family.CPUName}";
            Controller.SetUpMagWindow(this);
            Wpf.Ui.Appearance.Watcher.Watch(this, Wpf.Ui.Appearance.BackgroundType.Mica, true);
            ApplyThemeFromSettings();
            ConfigureAiChatInput();
        }

        private void ConfigureAiChatInput()
        {
            if (txtChatMessage == null)
                return;

            txtChatMessage.AcceptsReturn = false;
            txtChatMessage.Focusable = true;
            txtChatMessage.IsTabStop = true;

            // Kích hoạt bộ gõ tiếng Việt Telex thông minh tích hợp
            VietnameseInputHelper.Attach(txtChatMessage);
        }

        private void BtnToggleVietnamese_Click(object sender, RoutedEventArgs e)
        {
            VietnameseInputHelper.IsEnabled = !VietnameseInputHelper.IsEnabled;
            if (btnToggleVietnamese != null)
            {
                btnToggleVietnamese.Content = VietnameseInputHelper.IsEnabled ? "🇻🇳 VN" : "🔤 EN";
                btnToggleVietnamese.ToolTip = VietnameseInputHelper.IsEnabled
                    ? "Bộ gõ tiếng Việt Telex: ĐANG BẬT"
                    : "Bộ gõ tiếng Việt Telex: ĐANG TẮT (Chế độ tiếng Anh)";
            }
        }

        #region Theme Switching Logic

        private void ApplyThemeFromSettings()
        {
            string themeMode = Settings.Default.ThemeMode ?? "Dark";
            ApplyThemeMode(themeMode);
        }

        public void ApplyThemeMode(string mode)
        {
            switch (mode)
            {
                case "Light":
                    Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Light);
                    Settings.Default.ThemeMode = "Light";
                    break;

                case "System":
                    bool isSystemLight = IsSystemInLightTheme();
                    Wpf.Ui.Appearance.Theme.Apply(isSystemLight ? Wpf.Ui.Appearance.ThemeType.Light : Wpf.Ui.Appearance.ThemeType.Dark);
                    Settings.Default.ThemeMode = "System";
                    break;

                case "Dark":
                default:
                    Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Dark);
                    Settings.Default.ThemeMode = "Dark";
                    break;
            }
            Settings.Default.Save();
        }

        private bool IsSystemInLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int i)
                {
                    return i == 1;
                }
            }
            catch { }
            return false;
        }

        private void BtnThemeLight_Click(object sender, RoutedEventArgs e) => ApplyThemeMode("Light");
        private void BtnThemeDark_Click(object sender, RoutedEventArgs e) => ApplyThemeMode("Dark");
        private void BtnThemeSystem_Click(object sender, RoutedEventArgs e) => ApplyThemeMode("System");

        #endregion

        #region AI Chat Panel Logic

        private bool _isChatOpen = false;

        private void BtnToggleChat_Click(object sender, RoutedEventArgs e)
        {
            if (_isChatOpen)
            {
                CloseChatPanel();
            }
            else
            {
                OpenChatPanel();
            }
        }

        private void BtnCloseChat_Click(object sender, RoutedEventArgs e)
        {
            CloseChatPanel();
        }

        private void OpenChatPanel()
        {
            chatAiPanel.Visibility = Visibility.Visible;
            var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["OpenChatAnimation"];
            sb.Begin(this);
            _isChatOpen = true;
            txtChatMessage.Focus();
        }

        private void CloseChatPanel()
        {
            var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["CloseChatAnimation"];
            sb.Completed += (s, e) =>
            {
                if (!_isChatOpen) chatAiPanel.Visibility = Visibility.Collapsed;
            };
            _isChatOpen = false;
            sb.Begin(this);
        }

        private void TxtChatMessage_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter &&
                !System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) &&
                !System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
            {
                e.Handled = true;
                SendMessageToAi();
            }
        }

        private void BtnQuickPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn)
            {
                string prompt = btn.Content?.ToString() ?? "";
                if (prompt.Contains("Giảm ồn quạt"))
                    txtChatMessage.Text = "Làm sao để giảm tiếng ồn quạt máy tính khi đang chạy?";
                else if (prompt.Contains("Hạ nhiệt CPU"))
                    txtChatMessage.Text = "Cách hạ nhiệt độ CPU hiệu quả nhất để máy mát và quạt không kêu?";
                else if (prompt.Contains("Cách Undervolt"))
                    txtChatMessage.Text = "Hướng dẫn tôi cách Undervolt CPU để giảm tiếng ồn quạt và hạ nhiệt độ?";
                else if (prompt.Contains("Tăng FPS"))
                    txtChatMessage.Text = "Làm sao để tối ưu hệ thống tăng FPS khi chơi game?";
                else
                    txtChatMessage.Text = prompt;

                SendMessageToAi();
            }
        }

        private void BtnClearChat_Click(object sender, RoutedEventArgs e)
        {
            panelChatMessages.Children.Clear();
            GeminiService.ClearHistory();

            Border welcomeBubble = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 24, 10),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                MaxWidth = 280
            };
            welcomeBubble.SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");

            TextBlock tb = new TextBlock
            {
                Text = "Xin chào! Tôi là Trợ Lý AI VeloX. Tôi có thể hỗ trợ bạn tối ưu CPU/GPU, tư vấn Undervolt để giảm tiếng ồn quạt, hạ nhiệt độ hoặc tăng tốc máy tính. Hãy hỏi tôi bất cứ điều gì!",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

            welcomeBubble.Child = tb;
            panelChatMessages.Children.Add(welcomeBubble);
        }

        private void BtnSendChat_Click(object sender, RoutedEventArgs e)
        {
            SendMessageToAi();
        }

        private async void SendMessageToAi()
        {
            string userText = txtChatMessage.Text?.Trim();
            if (string.IsNullOrEmpty(userText)) return;

            txtChatMessage.Text = "";
            btnSendChat.IsEnabled = false;

            // Add user message bubble
            AddChatMessageBubble(userText, isUser: true);

            // Add loading indicator
            Border loadingBubble = AddChatMessageBubble("⏳ AI đang suy nghĩ và phân tích dữ liệu...", isUser: false);

            try
            {
                string aiResponse = await GeminiService.ChatAsync(userText);
                panelChatMessages.Children.Remove(loadingBubble);

                // Thực thi lệnh điều khiển chức năng phần mềm thông qua AiCommandExecutor
                var execResult = await AiCommandExecutor.ProcessAndExecuteCommandsAsync(userText, aiResponse);

                string finalBubbleText = execResult.CleanResponse;
                if (execResult.HasActionExecuted)
                {
                    finalBubbleText = $"⚡ {execResult.ExecutedActionText}\n\n{finalBubbleText}";
                }

                AddChatMessageBubble(finalBubbleText, isUser: false);
            }
            catch (Exception ex)
            {
                panelChatMessages.Children.Remove(loadingBubble);
                AddChatMessageBubble("❌ Lỗi kết nối AI: " + ex.Message, isUser: false);
            }
            finally
            {
                btnSendChat.IsEnabled = true;
            }
        }

        private Border AddChatMessageBubble(string text, bool isUser)
        {
            Border bubble = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = isUser ? new Thickness(24, 0, 0, 10) : new Thickness(0, 0, 24, 10),
                HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left,
                MaxWidth = 300
            };

            if (isUser)
            {
                bubble.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
            }
            else
            {
                bubble.SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");
            }

            TextBlock tb = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, isUser ? "TextFillColorLightPrimaryBrush" : "TextFillColorPrimaryBrush");

            bubble.Child = tb;
            panelChatMessages.Children.Add(bubble);
            scrollChatMessages.ScrollToEnd();

            return bubble;
        }

        #endregion

        private async void ApplyOnStart()
        {
            if (Settings.Default.ApplyOnStart)
                if (Settings.Default.CommandString != null && Settings.Default.CommandString != "")
                {

                    await Task.Run(() => GetBatteryStatus());
                    await Task.Run(() => GetBatteryStatus());

                    if (statuscode == 2 || statuscode == 6 || statuscode == 7 || statuscode == 8)
                    {
                        if (Settings.Default.acCommandString != null && Settings.Default.acCommandString != "")
                        {
                            Settings.Default.CommandString = Settings.Default.acCommandString;
                            Settings.Default.Save();
                            await Task.Run(() => RyzenAdj_To_UXTU.Translate(Settings.Default.acCommandString));
                            ToastNotification.ShowToastNotification("Charge Preset Applied!", $"Your charge preset settings have been applied!");
                        }
                        else
                        {
                            await Task.Run(() => RyzenAdj_To_UXTU.Translate(Settings.Default.CommandString));
                            ToastNotification.ShowToastNotification("Settings Reapplied!", $"Your last applied settings have been reapplied!");
                        }
                    }
                    else
                    {
                        if (Settings.Default.dcCommandString != null && Settings.Default.dcCommandString != "")
                        {
                            Settings.Default.CommandString = Settings.Default.dcCommandString;
                            Settings.Default.Save();
                            await Task.Run(() => RyzenAdj_To_UXTU.Translate(Settings.Default.dcCommandString));
                            ToastNotification.ShowToastNotification("Discharge Preset Applied!", $"Your discharge preset settings have been applied!");
                        }
                        else
                        {
                            await Task.Run(() => RyzenAdj_To_UXTU.Translate(Settings.Default.CommandString));
                            ToastNotification.ShowToastNotification("Settings Reapplied!", $"Your last applied settings have been reapplied!");
                        }
                    }
                }
        }


        static bool firstTime = true;
        private static List<GameLauncherItem> listOfGames = null;
        int garbage = -1;

        InstabilityMonitor monitor = null;

        AdaptiveUndervoltController cpuController = null;
        AdaptiveUndervoltController iGpuController = null;

        int lastCPUUVOffset = 0;
        int lastiGPUUVOffset = 0;
        private async void Misc_Tick(object sender, EventArgs e)
        {
            try
            {
                if (File.Exists(Settings.Default.Path + "\\gameData.json") && Settings.Default.isTrack == true)
                {
                    try
                    {
                        await ProcessRTSS();
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLogger.LogError(ex, "Failed to process RTSS stats");
                    }
                }

                if(Settings.Default.isAutoUvCPU == true)
                {

                    if(monitor == null) monitor = new InstabilityMonitor();

                    if (cpuController == null) cpuController = new AdaptiveUndervoltController(
                        monitor,
                        minOffset: -50,
                        stepSize: 1,
                        stableThreshold: 8,
                        cooldownThreshold: 4,
                        isIgpu: false
                    );

                    var newOffset = cpuController.UpdateOffset();
                    var commandValues = "";

                    if (Family.FAM < Family.RyzenFamily.Renoir) commandValues = $"--set-coper={(0 << 20) | (newOffset & 0xFFFF)} ";
                    else
                    {
                        if (newOffset >= 0) commandValues = commandValues + $"--set-coall={newOffset} ";
                        if (newOffset < 0) commandValues = commandValues + $"--set-coall={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)newOffset))} ";
                    }

                    if (lastCPUUVOffset != newOffset)
                    {
                        await Task.Run(() => RyzenAdj_To_UXTU.Translate(commandValues, false, true));
                        lastCPUUVOffset = newOffset;
                    }

                    cpuController.RecordAppliedOffset(newOffset);
                }
                else if (Settings.Default.isAutoUvCPU == false && cpuController != null)
                {
                    cpuController.Dispose();
                    cpuController = null;
                }


                if (Settings.Default.isAutoUviGPU == true)
                {
                    if (monitor == null) monitor = new InstabilityMonitor();

                    if(iGpuController == null) iGpuController = new AdaptiveUndervoltController(
                        monitor,
                        minOffset: -50,
                        stepSize: 1,
                        stableThreshold: 8,
                        cooldownThreshold: 4,
                        isIgpu : true
                    );

                    var newOffset = iGpuController.UpdateOffset();
                    var commandValues = "";

                    if (newOffset >= 0) commandValues = commandValues + $"--set-cogfx={newOffset} ";
                    if (newOffset < 0) commandValues = commandValues + $"--set-cogfx={Convert.ToUInt32(0x100000 - (uint)(-1 * (int)newOffset))} ";

                    if (lastiGPUUVOffset != newOffset)
                    {
                        await Task.Run(() => RyzenAdj_To_UXTU.Translate(commandValues, false, true));
                        lastiGPUUVOffset = newOffset;
                    }

                    iGpuController.RecordAppliedOffset(newOffset);
                }
                else if (Settings.Default.isAutoUviGPU == false && iGpuController != null)
                {
                    iGpuController.Dispose();
                    iGpuController = null;
                }

                if(Settings.Default.isAutoUvCPU == false && Settings.Default.isAutoUviGPU == false && monitor != null)
                {
                    monitor.Stop();
                    monitor = null;
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, "Failed during RTSS performance tracking tick");
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private async Task ProcessRTSS()
        {
            if (RTSS.RTSSRunning())
            {
                var osd = new OSD("RTSSDemo");
                var appEntries = OSD.GetAppEntries().Where(x => (x.Flags & AppFlags.MASK) != AppFlags.None).ToArray();
                if (!firstTime)
                {
                    foreach (Game_Manager.GameLauncherItem item in listOfGames)
                    {
                        foreach (var app in appEntries)
                        {
                            if (item.path != null && app.Name.ToLower().Contains(item.path.ToLower()) || app.Name.ToLower().Contains(GetImages.CleanFileName(item.gameName.ToLower())) || item.exe != null && app.Name.ToLower().Contains(item.exe))
                            {
                                GameDataManager gameDataManager = new GameDataManager(Settings.Default.Path + "gameData.json");

                                GameData gameData = gameDataManager.GetPreset(item.gameName);
                                uint fps = app.InstantaneousFrames;

                                string fpsString = gameData.fpsAvData;

                                uint[] fpsArray = fpsString.Split(',').Select(uint.Parse).ToArray();

                                Array.Resize(ref fpsArray, fpsArray.Length + 1);
                                fpsArray[fpsArray.Length - 1] = fps;

                                for (int i = 0; i < fpsArray.Length - 1; i++)
                                {
                                    fpsArray[i] = fpsArray[i + 1];
                                }

                                uint totalFps = fpsArray.Aggregate((uint)0, (sum, fps) => sum + fps);
                                uint averageFps = (uint)(totalFps / (uint)fpsArray.Length);

                                fpsString = string.Join(",", fpsArray);

                                string timeSpanString = gameData.msAvData;

                                string[] timeSpanArray = timeSpanString.Split(',');

                                TimeSpan[] timeSpans = timeSpanArray.Select(TimeSpan.Parse).ToArray();
                                TimeSpan newTimeSpan = app.InstantaneousFrameTime;

                                Array.Resize(ref timeSpans, timeSpans.Length + 1);
                                timeSpans[timeSpans.Length - 1] = newTimeSpan;

                                for (int i = 0; i < timeSpans.Length - 1; i++)
                                {
                                    timeSpans[i] = timeSpans[i + 1];
                                }

                                TimeSpan totalTimeSpan = new TimeSpan(timeSpans.Select(ts => ts.Ticks).Sum());
                                TimeSpan averageTimeSpan = TimeSpan.FromTicks(totalTimeSpan.Ticks / timeSpans.Length);

                                timeSpanString = string.Join(",", timeSpans);

                                GameData preset = new GameData
                                {
                                    fpsData = averageFps.ToString(),
                                    fpsAvData = fpsString,
                                    msData = string.Format("{0:0.##}", averageTimeSpan.TotalMilliseconds),
                                    msAvData = timeSpanString,
                                };
                                gameDataManager.SavePreset(item.gameName, preset);

                                garbage++;

                                if (garbage >= 20)
                                {
                                    await Task.Run(() => {
                                        Garbage.Garbage_Collect();
                                    });

                                    garbage = -1;
                                }
                            }
                        }
                    }
                }
                else
                {
                    Misc.Stop();
                    await Task.Run(() => {
                        listOfGames = Game_Manager.syncGame_Library();
                        Garbage.Garbage_Collect();
                    });
                    firstTime = false;
                    Misc.Start();
                }
            }
            else RTSS.startRTSS();
        }

        private async Task ProcessGamePerformanceData(GameLauncherItem game)
        {
            var appEntries = RTSSSharedMemoryNET.OSD.GetAppEntries().Where(app => (app.Flags & AppFlags.MASK) != AppFlags.None).ToArray();

            foreach (var app in appEntries)
            {
                if (!IsGameMatched(game, app.Name)) continue;

                var gameDataManager = new GameDataManager(Settings.Default.Path + "gameData.json");
                var gameData = gameDataManager.GetPreset(game.gameName);

                UpdateGamePerformanceData(app, ref gameData);
                gameDataManager.SavePreset(game.gameName, gameData);
            }
        }

        private static bool IsGameMatched(GameLauncherItem game, string appName)
        {
            return !string.IsNullOrWhiteSpace(game.path) && appName.Contains(game.path, StringComparison.OrdinalIgnoreCase)
                   || appName.Contains(GetImages.CleanFileName(game.gameName), StringComparison.OrdinalIgnoreCase)
                   || !string.IsNullOrWhiteSpace(game.exe) && appName.Contains(game.exe, StringComparison.OrdinalIgnoreCase);
        }

        private static void UpdateGamePerformanceData(AppEntry app, ref GameData gameData)
        {
            var fpsArray = ParseAndUpdateData(app.InstantaneousFrames, gameData.fpsAvData, out var averageFps);
            var timeSpans = ParseAndUpdateData(app.InstantaneousFrameTime, gameData.msAvData, out var averageTimeSpan);

            gameData.fpsData = averageFps.ToString();
            gameData.fpsAvData = fpsArray;
            gameData.msData = averageTimeSpan.TotalMilliseconds.ToString("0.##");
            gameData.msAvData = timeSpans;
        }

        private static string ParseAndUpdateData<T>(T newData, string existingData, out T average)
        {
            var dataList = existingData.Split(',').Select(s => (T)Convert.ChangeType(s, typeof(T))).ToList();
            dataList.Add(newData);

            if (dataList.Count > 100) dataList.RemoveAt(0);

            average = (T)Convert.ChangeType(dataList.Average(x => Convert.ToDouble(x)), typeof(T));
            return string.Join(",", dataList);
        }

        private static ushort statuscode;
        public static ushort batteryPercent = 100;

        public static void GetBatteryStatus()
        {
            try
            {
                var batteryClass = new ManagementClass("Win32_Battery");
                var batteries = batteryClass.GetInstances();

                foreach (var battery in batteries)
                {
                    statuscode = (ushort)battery["BatteryStatus"];
                    if (battery["EstimatedChargeRemaining"] != null)
                    {
                        batteryPercent = Convert.ToUInt16(battery["EstimatedChargeRemaining"]);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions if necessary
                DiagnosticLogger.LogError(ex, "Failed to get battery status");
            }
        }

        private void PerformGarbageCollection(object sender, EventArgs e)
        {
            if (!isMini) Task.Run(Garbage.Garbage_Collect);
        }

        private async void AutoReapply_Tick(object sender, EventArgs e)
        {
            try
            {
                if (niTray.Visibility == Visibility.Hidden && this.Visibility == Visibility.Hidden) niTray.Visibility = Visibility.Visible;

                if ((bool)Settings.Default.AutoReapply == true && (bool)Settings.Default.isAdaptiveModeRunning == false)
                {
                    string commands = (string)Settings.Default.CommandString;
                    //Check if RyzenAdjArguments is populated
                    if (commands != null && commands != "")
                    {
                        await Task.Run(() => RyzenAdj_To_UXTU.Translate(commands));
                    }

                    if (autoReapply.Interval != TimeSpan.FromSeconds((int)Settings.Default.AutoReapplyTime))
                    {
                        autoReapply.Stop();
                        autoReapply.Interval = TimeSpan.FromSeconds((int)Settings.Default.AutoReapplyTime);
                        autoReapply.Start();
                    }
                }

                // Check Low Battery Saver
                if (Settings.Default.isAutoLowBatterySaver)
                {
                    GetBatteryStatus();
                    if (statuscode != 2 && statuscode != 6 && statuscode != 7 && statuscode != 8 && batteryPercent <= 20 && lastAppliedState != "low_battery")
                    {
                        lastAppliedState = "low_battery";
                        PremadePresets.SetPremadePresets();
                        string ecoCmd = PremadePresets.EcoPreset;
                        if (!string.IsNullOrEmpty(ecoCmd))
                        {
                            Settings.Default.CommandString = ecoCmd;
                            try { Settings.Default.Save(); } catch { }
                            await Task.Run(() => RyzenAdj_To_UXTU.Translate(ecoCmd));
                            ToastNotification.ShowToastNotification("Low Battery Saver", "Battery is under 20%! Eco preset activated to maximize battery life.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, "Failed during auto-reapply tick");
            }
        }
        int i = 0;
        async void GC_Tick(object sender, EventArgs e)
        {
            await Task.Run(() => Garbage.Garbage_Collect());
            if (Settings.Default.StartMini == true && this.WindowState == WindowState.Minimized && i < 2) this.ShowInTaskbar = false;
            if (i > 8) GC.Stop();
            i++;
        }

        private static void UpdateTimerInterval(DispatcherTimer timer, int newInterval)
        {
            if (timer.Interval == TimeSpan.FromSeconds(newInterval)) return;

            timer.Stop();
            timer.Interval = TimeSpan.FromSeconds(newInterval);
            timer.Start();
        }

        static string lastAppliedState = "";
        private async void HandlePowerModeChange(object sender, PowerModeChangedEventArgs e)
        {
            try
            {
                if ((bool)Settings.Default.isAdaptiveModeRunning == false)
                {
                    if (e.Mode == PowerModes.StatusChange)
                    {
                        await Task.Run(() => GetBatteryStatus());
                        await Task.Run(() => PremadePresets.SetPremadePresets());

                        if (statuscode == 2 || statuscode == 6 || statuscode == 7 || statuscode == 8)
                        {
                            if (Settings.Default.isAutoRefreshRate)
                            {
                                try
                                {
                                    Display.setUpLists();
                                    if (Display.uniqueRefreshRates != null && Display.uniqueRefreshRates.Count > 0)
                                    {
                                        int maxHz = Display.uniqueRefreshRates.Max();
                                        Display.ApplySettings(maxHz);
                                    }
                                }
                                catch { }
                            }

                            if (Settings.Default.acCommandString != null && Settings.Default.acCommandString != "" && Settings.Default.acPreset != "None")
                            {
                                if (Settings.Default.acPreset.Contains("PM - Eco"))
                                {
                                    Settings.Default.premadePreset = 0;
                                    Settings.Default.acCommandString = PremadePresets.EcoPreset;
                                }
                                else if (Settings.Default.acPreset.Contains("PM - Bal"))
                                {
                                    Settings.Default.premadePreset = 1;
                                    Settings.Default.acCommandString = PremadePresets.BalPreset;
                                }
                                else if (Settings.Default.acPreset.Contains("PM - Perf"))
                                {
                                    Settings.Default.premadePreset = 2;
                                    Settings.Default.acCommandString = PremadePresets.PerformancePreset;
                                }
                                else if (Settings.Default.acPreset.Contains("PM - Ext"))
                                {
                                    Settings.Default.premadePreset = 3;
                                    Settings.Default.acCommandString = PremadePresets.ExtremePreset;
                                }

                                Settings.Default.CommandString = Settings.Default.acCommandString;
                                Settings.Default.Save();
                                await Task.Run(() => RyzenAdj_To_UXTU.Translate(Settings.Default.acCommandString));

                                if (lastAppliedState != "ac") ToastNotification.ShowToastNotification("Charge Preset Applied!", $"Your charge preset settings have been applied!");
                                lastAppliedState = "ac";
                            }
                        }
                        else
                        {
                            if (Settings.Default.isAutoRefreshRate)
                            {
                                try
                                {
                                    Display.ApplySettings(60);
                                }
                                catch { }
                            }

                            if (Settings.Default.dcCommandString != null && Settings.Default.dcCommandString != "" && Settings.Default.dcPreset != "None")
                            {
                                if (Settings.Default.dcPreset.Contains("PM - Eco"))
                                {
                                    Settings.Default.premadePreset = 0;
                                    Settings.Default.dcCommandString = PremadePresets.EcoPreset;
                                }
                                else if (Settings.Default.dcPreset.Contains("PM - Bal"))
                                {
                                    Settings.Default.premadePreset = 1;
                                    Settings.Default.dcCommandString = PremadePresets.BalPreset;
                                }
                                else if (Settings.Default.dcPreset.Contains("PM - Perf"))
                                {
                                    Settings.Default.premadePreset = 2;
                                    Settings.Default.dcCommandString = PremadePresets.PerformancePreset;
                                }
                                else if (Settings.Default.dcPreset.Contains("PM - Ext"))
                                {
                                    Settings.Default.premadePreset = 3;
                                    Settings.Default.dcCommandString = PremadePresets.ExtremePreset;
                                }
                                Settings.Default.CommandString = Settings.Default.dcCommandString;
                                Settings.Default.Save();
                                await Task.Run(() => RyzenAdj_To_UXTU.Translate(Settings.Default.dcCommandString));

                                if (lastAppliedState != "dc") ToastNotification.ShowToastNotification("Discharge Preset Applied!", $"Your discharge preset settings have been applied!");
                                lastAppliedState = "dc";
                            }
                        }
                    }

                    if (e.Mode == PowerModes.Resume)
                    {
                        if (Settings.Default.resumeCommandString != null && Settings.Default.resumeCommandString != "" && Settings.Default.resumePreset != "None")
                        {
                            if (Settings.Default.resumePreset.Contains("PM - Eco"))
                            {
                                Settings.Default.premadePreset = 0;
                                Settings.Default.resumeCommandString = PremadePresets.EcoPreset;
                            }
                            else if (Settings.Default.resumePreset.Contains("PM - Bal"))
                            {
                                Settings.Default.premadePreset = 1;
                                Settings.Default.resumeCommandString = PremadePresets.BalPreset;
                            }
                            else if (Settings.Default.resumePreset.Contains("PM - Perf"))
                            {
                                Settings.Default.premadePreset = 2;
                                Settings.Default.resumeCommandString = PremadePresets.PerformancePreset;
                            }
                            else if (Settings.Default.resumePreset.Contains("PM - Ext"))
                            {
                                Settings.Default.premadePreset = 3;
                                Settings.Default.resumeCommandString = PremadePresets.ExtremePreset;
                            }
                            Settings.Default.CommandString = Settings.Default.resumeCommandString;
                            Settings.Default.Save();
                            Task.Run(() => RyzenAdj_To_UXTU.Translate(Settings.Default.resumeCommandString));

                            if (lastAppliedState != "resume") ToastNotification.ShowToastNotification("Resume Preset Applied!", $"Your resume preset settings have been applied!");
                            lastAppliedState = "resume";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLogger.LogError(ex, "Failed during power mode change handling");
            }
        }

        #region INavigationWindow Methods

        public Frame GetFrame() => RootFrame;

        public INavigation GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(IPageService pageService) => RootNavigation.PageService = pageService;

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void UiWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                isMini = true;
                this.WindowStyle = WindowStyle.ToolWindow;
                this.ShowInTaskbar = false;
            }
            else
            {
                isMini = false;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.ShowInTaskbar = true;
            }

            Task.Run(Garbage.Garbage_Collect);
        }

        private void NotifyIcon_LeftClick(Wpf.Ui.Controls.NotifyIcon sender, RoutedEventArgs e)
        {
            if (this.WindowState != WindowState.Minimized)
            {
                this.WindowState = WindowState.Minimized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
                this.Activate();
            }

            Task.Run(Garbage.Garbage_Collect);
        }

        private void miClose_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.isAdaptiveModeRunning = false;
            Settings.Default.Save();
            Controller.magWindow?.Dispose();
            Application.Current.Shutdown();
        }

        private void UiWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Settings.Default.MinimizeClose)
            {
                WindowState = WindowState.Minimized;
                e.Cancel = true;
                return;
            }
            
            Settings.Default.isAdaptiveModeRunning = false;
            Settings.Default.Save();
            Controller.magWindow?.Dispose();
            //Fan_Control.disableFanControl();
        }

        private void mainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Settings.Default.StartMini == true)
            {
                this.WindowState = WindowState.Minimized;
            }
            else
            {
                if (GetSystemInfo.Manufacturer.ToUpper().Contains("AYANEO") || GetSystemInfo.Manufacturer.ToUpper().Contains("GPD") || GetSystemInfo.Product.ToUpper().Contains("ONEXPLAYER"))
                {
                    int displayCount = Screen.AllScreens.Length;
                    if (displayCount < 2)
                    {
                        this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
                        this.WindowState = WindowState.Maximized;
                    }
                }
            }

            PremadePresets.SetPremadePresets();

            Task.Run(Garbage.Garbage_Collect);
        }
    }
}