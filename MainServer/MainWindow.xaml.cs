using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace MainServerApp
{
    public partial class MainWindow : Window
    {
        private readonly MainServer.MainServer server;
        private readonly ObservableCollection<string> logs = new();
        private readonly ObservableCollection<WorkerViewModel> workers = new();
        private readonly ObservableCollection<ClientViewModel> clients = new();
        private readonly ObservableCollection<RoomViewModel> rooms = new();
        private System.Windows.Threading.DispatcherTimer? roomUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            // Bind collections to UI
            LogListBox.ItemsSource = logs;
            WorkersDataGrid.ItemsSource = workers;
            ClientsDataGrid.ItemsSource = clients;
            RoomsDataGrid.ItemsSource = rooms;

            // Create and configure server
            server = new MainServer.MainServer(5000, 5001);
            
            // Subscribe to server events
            server.OnLogMessage += Server_OnLogMessage;
            server.OnWorkerConnected += Server_OnWorkerConnected;
            server.OnWorkerDisconnected += Server_OnWorkerDisconnected;
            server.OnWorkerStatusChanged += Server_OnWorkerStatusChanged;
            server.OnClientConnected += Server_OnClientConnected;
            server.OnClientDisconnected += Server_OnClientDisconnected;
            server.OnClientAuthenticated += Server_OnClientAuthenticated;
            server.OnServerStats += Server_OnServerStats;
            server.OnRoomUpdated += Server_OnRoomUpdated;

            // Timer to update room durations
            roomUpdateTimer = new System.Windows.Threading.DispatcherTimer();
            roomUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            roomUpdateTimer.Tick += RoomUpdateTimer_Tick;
            roomUpdateTimer.Start();

            // Start server asynchronously
            _ = StartServerAsync();
        }

        private async System.Threading.Tasks.Task StartServerAsync()
        {
            try
            {
                await server.StartAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // ==================== Event Handlers for Server Events ====================

        private void Server_OnLogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                
                // Auto-scroll to bottom
                if (LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                }

                // Limit log size to prevent memory issues
                if (logs.Count > 1000)
                {
                    logs.RemoveAt(0);
                }
            });
        }

        private void Server_OnWorkerConnected(string workerId, string endpoint)
        {
            Dispatcher.Invoke(() =>
            {
                workers.Add(new WorkerViewModel
                {
                    WorkerId = workerId,
                    Endpoint = endpoint,
                    Status = "Idle",
                    CurrentTask = "Idle",
                    TasksCompleted = 0
                });
            });
        }

        private void Server_OnWorkerDisconnected(string workerId)
        {
            Dispatcher.Invoke(() =>
            {
                var worker = workers.FirstOrDefault(w => w.WorkerId == workerId);
                if (worker != null)
                {
                    workers.Remove(worker);
                }
            });
        }

        private void Server_OnWorkerStatusChanged(string workerId, MainServer.MainServer.WorkerStatus status, string currentTask)
        {
            Dispatcher.Invoke(() =>
            {
                var worker = workers.FirstOrDefault(w => w.WorkerId == workerId);
                if (worker != null)
                {
                    worker.Status = status.ToString();
                    worker.CurrentTask = currentTask;
                    
                    // Update tasks completed from server
                    var serverWorker = server.Workers.FirstOrDefault(w => w.WorkerId == workerId);
                    if (serverWorker != null)
                    {
                        worker.TasksCompleted = serverWorker.TasksCompleted;
                    }
                    
                    // Refresh the DataGrid to show updated values
                    WorkersDataGrid.Items.Refresh();
                }
            });
        }

        private void Server_OnClientConnected(string clientId, int totalClients)
        {
            Dispatcher.Invoke(() =>
            {
                var client = server.Clients.FirstOrDefault(c => c.ClientId == clientId);
                clients.Add(new ClientViewModel
                {
                    ClientId = clientId,
                    PlayerName = client?.AuthenticatedProfile?.PlayerName ?? "Unknown"
                });
            });
        }

        private void Server_OnClientDisconnected(string clientId, int totalClients)
        {
            Dispatcher.Invoke(() =>
            {
                var client = clients.FirstOrDefault(c => c.ClientId == clientId);
                if (client != null)
                {
                    clients.Remove(client);
                }
            });
        }

        private void Server_OnClientAuthenticated(string clientId, string playerName)
        {
            Dispatcher.Invoke(() =>
            {
                var client = clients.FirstOrDefault(c => c.ClientId == clientId);
                if (client != null)
                {
                    client.PlayerName = playerName;
                    ClientsDataGrid.Items.Refresh();
                }
            });
        }

        private void Server_OnServerStats(int totalClients, int totalWorkers, int activeGames)
        {
            Dispatcher.Invoke(() =>
            {
                ClientsCountText.Text = totalClients.ToString();
                WorkersCountText.Text = totalWorkers.ToString();
                ActiveGamesText.Text = activeGames.ToString();
            });
        }

        private void Server_OnRoomUpdated()
        {
            UpdateRoomsList();
        }

        private void RoomUpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Update durations of active rooms
            Dispatcher.Invoke(() =>
            {
                foreach (var room in rooms)
                {
                    var duration = DateTime.Now - room.StartTime;
                    room.Duration = $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
                }
                RoomsDataGrid.Items.Refresh();
            });
        }

        private void UpdateRoomsList()
        {
            Dispatcher.Invoke(() =>
            {
                rooms.Clear();
                foreach (var room in server.ActiveRooms)
                {
                    rooms.Add(new RoomViewModel
                    {
                        RoomId = room.RoomId,
                        Player1Name = room.GetPlayer1Name(),
                        Player2Name = room.GetPlayer2Name(),
                        StartTime = room.StartTime,
                        Duration = "00:00"
                    });
                }
            });
        }

        // ==================== Button Click Handlers ====================

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            logs.Clear();
        }

        private void DisconnectWorkerButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkersDataGrid.SelectedItem is WorkerViewModel worker)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to disconnect worker '{worker.WorkerId}'?",
                    "Confirm Disconnect",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    server.DisconnectWorker(worker.WorkerId);
                }
            }
        }

        private void DisconnectClientButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsDataGrid.SelectedItem is ClientViewModel client)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to disconnect client '{client.ClientId}'?",
                    "Confirm Disconnect",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    server.DisconnectClient(client.ClientId);
                }
            }
        }

        private async void StartStopServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (server.IsRunning)
            {
                // Stop server
                var result = MessageBox.Show(
                    "Are you sure you want to stop the server?",
                    "Confirm Stop",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    server.Stop();
                    
                    // Wait a bit for server to fully stop
                    await Task.Delay(100);
                    
                    StatusText.Text = "Stopped";
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                    StatusIndicator.Color = System.Windows.Media.Colors.Red;
                    StartStopServerButton.Content = "▶ Start Server";
                    StartStopServerButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2ECC71"));
                }
            }
            else
            {
                // Start server
                ClearAllData();
                
                // Update UI immediately to Start state
                StatusText.Text = "Running";
                StatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                StatusIndicator.Color = System.Windows.Media.Colors.Green;
                StartStopServerButton.Content = "⏹ Stop Server";
                StartStopServerButton.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E74C3C"));
                
                // Then start the server
                await server.StartAsync();
            }
        }

        private void WorkersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisconnectWorkerButton.IsEnabled = WorkersDataGrid.SelectedItem != null;
        }

        private void ClientsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisconnectClientButton.IsEnabled = ClientsDataGrid.SelectedItem != null;
        }

        private void ClearAllData()
        {
            // Clear all collections
            logs.Clear();
            workers.Clear();
            clients.Clear();
            rooms.Clear();
            
            // Reset counters
            ClientsCountText.Text = "0";
            WorkersCountText.Text = "0";
            ActiveGamesText.Text = "0";
            
            // Clear server data
            server.ClearAllData();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (server.IsRunning)
            {
                var result = MessageBox.Show(
                    "Server is still running. Stop and exit?",
                    "Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                server.Stop();
            }

            base.OnClosing(e);
        }
    }

    // ==================== View Models ====================

    public class WorkerViewModel
    {
        public string WorkerId { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Status { get; set; } = "Idle";
        public string CurrentTask { get; set; } = "Idle";
        public int TasksCompleted { get; set; } = 0;
    }

    public class ClientViewModel
    {
        public string ClientId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = "Unknown";
    }

    public class RoomViewModel
    {
        public string RoomId { get; set; } = string.Empty;
        public string Player1Name { get; set; } = "Player 1";
        public string Player2Name { get; set; } = "Player 2";
        public string Duration { get; set; } = "00:00";
        public DateTime StartTime { get; set; } = DateTime.Now;
    }
}
