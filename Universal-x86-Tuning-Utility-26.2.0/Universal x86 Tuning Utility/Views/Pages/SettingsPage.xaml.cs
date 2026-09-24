using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using Universal_x86_Tuning_Utility.Properties;
using Universal_x86_Tuning_Utility.Scripts.Misc;
using Universal_x86_Tuning_Utility.Services;
using Wpf.Ui.Common.Interfaces;
using System.Diagnostics.Eventing.Reader;

namespace Universal_x86_Tuning_Utility.Views.Pages
{
    /// <summary>
    /// Interaction logic for SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : INavigableView<ViewModels.SettingsViewModel>
    {
        private readonly ILogger<SettingsPage> _logger;

        public ViewModels.SettingsViewModel ViewModel
        {
            get;
        }

        public SettingsPage(ViewModels.SettingsViewModel viewModel, ILogger<SettingsPage> logger)
        {
            ViewModel = viewModel;
            _logger = logger;

            InitializeComponent();

            cbStartBoot.IsChecked = Settings.Default.StartOnBoot;
            cbStartMini.IsChecked = Settings.Default.StartMini;
            cbMinimizeClose.IsChecked = Settings.Default.MinimizeClose;
            cbApplyStart.IsChecked = Settings.Default.ApplyOnStart;
            cbAutoReapply.IsChecked = Settings.Default.AutoReapply;
            nudAutoReapply.Value = Settings.Default.AutoReapplyTime;
            nudAutoReapply.Text = Convert.ToString(Settings.Default.AutoReapplyTime);

            cbAdaptive.IsChecked = Settings.Default.isStartAdpative;
            cbTrack.IsChecked = Settings.Default.isTrack;

            cbxLogLevel.SelectedIndex = Settings.Default.DiagnosticLogLevel;
        }

        private void cbStartBoot_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            using (TaskService ts = new TaskService())
            {
                if (ts.RootFolder.AllTasks.Any(t => t.Name == "UXTU"))
                {
                    // Remove the task we just created
                    ts.RootFolder.DeleteTask("UXTU");
                }
            }

            if (cbStartBoot.IsChecked == true)
            {
                // Get the service on the local machine
                using (TaskService ts = new TaskService())
                {
                    if (!ts.RootFolder.AllTasks.Any(t => t.Name == "UXTU"))
                    {
                        // Create a new task definition and assign properties
                        TaskDefinition td = ts.NewTask();
                        td.Principal.RunLevel = TaskRunLevel.Highest;
                        td.RegistrationInfo.Description = "Start UXTU";
                        td.Settings.DisallowStartIfOnBatteries = false;
                        td.Settings.StopIfGoingOnBatteries = false;
                        td.Settings.DisallowStartOnRemoteAppSession = false;

                        // Create a trigger that will fire the task at this time every other day
                        td.Triggers.Add(new LogonTrigger());

                        string path = Environment.ProcessPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Universal x86 Tuning Utility.exe");

                        // Create an action that will launch Notepad whenever the trigger fires
                        td.Actions.Add(path);

                        // Register the task in the root folder
                        ts.RootFolder.RegisterTaskDefinition(@"UXTU", td);
                    }
                }
            }

            Settings.Default.StartOnBoot = (bool)cbStartBoot.IsChecked;
            Settings.Default.Save();
        }

        private void cbStartMini_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Settings.Default.StartMini = (bool)cbStartMini.IsChecked;
            Settings.Default.Save();
        }

        private void cbMinimizeClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Settings.Default.MinimizeClose = (bool)cbMinimizeClose.IsChecked;
            Settings.Default.Save();
        }

        private void cbAutoReapply_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Settings.Default.AutoReapply = (bool)cbAutoReapply.IsChecked;
            Settings.Default.AutoReapplyTime = (int)nudAutoReapply.Value;
            Settings.Default.Save();
        }

        private void nudAutoReapply_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Settings.Default.AutoReapplyTime = (int)nudAutoReapply.Value;
            Settings.Default.Save();
        }

        private void cbApplyStart_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Settings.Default.ApplyOnStart = (bool)cbApplyStart.IsChecked;
            Settings.Default.Save();
        }




        private void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void UiPage_Loaded(object sender, RoutedEventArgs e)
        {
            Garbage.Garbage_Collect();
        }

        private void btnStressTest_Click(object sender, RoutedEventArgs e)
        {
            StressTestRunner.StartOrToggleStressTest();
        }

        private void cbAdaptive_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.isStartAdpative = (bool)cbAdaptive.IsChecked;
            Settings.Default.Save();
        }

        private void cbTrack_Click(object sender, RoutedEventArgs e)
        {
            Settings.Default.isTrack = (bool)cbTrack.IsChecked;
            Settings.Default.Save();
        }

        private void cbxLogLevel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cbxLogLevel == null)
            {
                return;
            }

            Settings.Default.DiagnosticLogLevel = cbxLogLevel.SelectedIndex;
            Settings.Default.Save();
            DiagnosticLogger.ApplySettingsLevel();
        }

        private void nudAutoReapply_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (nudAutoReapply != null && nudAutoReapply.Value != null)
            {
                Settings.Default.AutoReapplyTime = (int)nudAutoReapply.Value;
                Settings.Default.Save();
            }
        }
    }
}