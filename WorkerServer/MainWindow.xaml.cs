using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace WorkerServerApp
{
    public partial class MainWindow : Window
    {
        private WorkerServer.WorkerServer? workerServer;
        private readonly ObservableCollection<string> logs = new();
        private System.Windows.Threading.DispatcherTimer? uptimeTimer;
        private DateTime? connectedAt;
        private bool isRunning = false;

        // Statistics
        private int totalRequests = 0;
        private int aiRequests = 0;
        private int validationRequests = 0;
        private int errorCount = 0;
        private double totalProcessingTime = 0;

        public MainWindow()
        {
            InitializeComponent();
            
            // Bind logs to UI
            LogListBox.ItemsSource = logs;

            // Setup uptime timer
            uptimeTimer = new System.Windows.Threading.DispatcherTimer();
            uptimeTimer.Interval = TimeSpan.FromSeconds(1);
            uptimeTimer.Tick += UptimeTimer_Tick;

            AddLog("Worker Server GUI initialized.");
            
            // Auto-start worker on load
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-start worker after window is fully loaded
            await System.Threading.Tasks.Task.Delay(500); // Small delay for UI to settle
            AddLog("Auto-starting worker...");
            StartWorker();
        }

        private async void StartWorker()
        {
            try
            {
                StartStopButton.IsEnabled = false;
                UpdateConnectionStatus("Connecting...", Color.FromRgb(241, 196, 15));
                AddLog("Starting worker server...");

                workerServer = new WorkerServer.WorkerServer();
                
                // Subscribe to events
                workerServer.OnLogMessage += WorkerServer_OnLogMessage;
                workerServer.OnConnectionChanged += WorkerServer_OnConnectionChanged;
                workerServer.OnRegistrationChanged += WorkerServer_OnRegistrationChanged;
                workerServer.OnRequestProcessed += WorkerServer_OnRequestProcessed;

                // Start the worker in background
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await workerServer.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AddLog($"ERROR: {ex.Message}");
                            ResetToDisconnected();
                        });
                    }
                });

                isRunning = true;
                connectedAt = DateTime.Now;
                uptimeTimer?.Start();

                StartStopButton.Content = "⏹ Stop Worker";
                StartStopButton.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                StartStopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                AddLog($"ERROR: Failed to start worker - {ex.Message}");
                ResetToDisconnected();
            }
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                
                // Auto-scroll to bottom
                if (LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                }

                // Limit log size
                if (logs.Count > 1000)
                {
                    logs.RemoveAt(0);
                }
            });
        }

        private void UpdateStatistics()
        {
            Dispatcher.Invoke(() =>
            {
                TotalRequestsText.Text = totalRequests.ToString();
                AIRequestsText.Text = aiRequests.ToString();
                ValidationRequestsText.Text = validationRequests.ToString();
                ErrorCountText.Text = errorCount.ToString();

                if (totalRequests > 0)
                {
                    double avgTime = totalProcessingTime / totalRequests;
                    AvgProcessingTimeText.Text = $"{avgTime:F0} ms";
                    
                    double successRate = ((totalRequests - errorCount) / (double)totalRequests) * 100;
                    SuccessRateText.Text = $"{successRate:F1}%";
                    SuccessRateText.Foreground = successRate >= 95 ? 
                        new SolidColorBrush(Colors.Green) : 
                        new SolidColorBrush(Colors.Orange);
                }
            });
        }

        private void UpdateConnectionStatus(string status, Color color)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
                StatusText.Foreground = new SolidColorBrush(color);
                StatusIndicator.Color = color;
            });
        }

        private void UptimeTimer_Tick(object? sender, EventArgs e)
        {
            if (connectedAt.HasValue)
            {
                var uptime = DateTime.Now - connectedAt.Value;
                UptimeText.Text = $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
            }
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning)
            {
                // Manual start (same as auto-start)
                StartWorker();
            }
            else
            {
                // Stop worker
                var result = MessageBox.Show(
                    "Are you sure you want to stop the worker?",
                    "Confirm Stop",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StopWorker();
                }
            }
        }

        private void StopWorker()
        {
            try
            {
                AddLog("Stopping worker server...");
                workerServer?.Stop();
                
                // Unsubscribe from events
                if (workerServer != null)
                {
                    workerServer.OnLogMessage -= WorkerServer_OnLogMessage;
                    workerServer.OnConnectionChanged -= WorkerServer_OnConnectionChanged;
                    workerServer.OnRegistrationChanged -= WorkerServer_OnRegistrationChanged;
                    workerServer.OnRequestProcessed -= WorkerServer_OnRequestProcessed;
                }

                workerServer = null;
                isRunning = false;
                uptimeTimer?.Stop();

                ResetToDisconnected();
                AddLog("Worker server stopped successfully.");
            }
            catch (Exception ex)
            {
                AddLog($"ERROR: Failed to stop worker - {ex.Message}");
            }
        }

        private void ResetToDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateConnectionStatus("Disconnected", Color.FromRgb(149, 165, 166));
                StartStopButton.Content = "▶ Start Worker";
                StartStopButton.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                StartStopButton.IsEnabled = true;
                
                WorkerIdText.Text = "Not Connected";
                ServerAddressText.Text = "Not Connected";
                RegistrationStatusText.Text = "Not Registered";
                RegistrationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                ConnectedAtText.Text = "-";
                UptimeText.Text = "00:00:00";
                
                connectedAt = null;
            });
        }

        // ==================== Worker Server Event Handlers ====================

        private void WorkerServer_OnLogMessage(string message)
        {
            AddLog(message);
        }

        private void WorkerServer_OnConnectionChanged(bool isConnected, string serverAddress)
        {
            Dispatcher.Invoke(() =>
            {
                if (isConnected)
                {
                    UpdateConnectionStatus("Connected", Color.FromRgb(46, 204, 113));
                    ServerAddressText.Text = serverAddress;
                    ConnectedAtText.Text = DateTime.Now.ToString("HH:mm:ss");
                    AddLog($"Connected to Main Server at {serverAddress}");
                }
                else
                {
                    UpdateConnectionStatus("Disconnected", Color.FromRgb(231, 76, 60));
                    ServerAddressText.Text = "Not Connected";
                    RegistrationStatusText.Text = "Not Registered";
                    RegistrationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    AddLog("Disconnected from Main Server");
                }
            });
        }

        private void WorkerServer_OnRegistrationChanged(bool isRegistered, string workerId)
        {
            Dispatcher.Invoke(() =>
            {
                if (isRegistered)
                {
                    UpdateConnectionStatus("Ready", Color.FromRgb(46, 204, 113));
                    WorkerIdText.Text = workerId;
                    RegistrationStatusText.Text = "Registered ✓";
                    RegistrationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                    AddLog($"Worker registered successfully with ID: {workerId}");
                }
                else
                {
                    RegistrationStatusText.Text = "Not Registered";
                    RegistrationStatusText.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                }
            });
        }

        private void WorkerServer_OnRequestProcessed(string requestType, double processingTimeMs, bool success)
        {
            Dispatcher.Invoke(() =>
            {
                totalRequests++;
                totalProcessingTime += processingTimeMs;

                if (requestType.Contains("AI"))
                {
                    aiRequests++;
                    LastAITimeText.Text = $"{processingTimeMs:F0} ms";
                }
                else if (requestType.Contains("VALIDATE"))
                {
                    validationRequests++;
                }

                if (!success)
                {
                    errorCount++;
                }

                LastRequestText.Text = requestType;
                UpdateStatistics();
            });
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            logs.Clear();
            AddLog("Logs cleared.");
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (isRunning && workerServer != null)
            {
                var result = MessageBox.Show(
                    "Worker is still running. Stop and exit?",
                    "Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                StopWorker();
            }

            uptimeTimer?.Stop();
            base.OnClosing(e);
        }
    }
}
