using ImageProcessor.Processors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for UseWebWalletWindow.xaml
    /// </summary>
    public partial class UseWebWalletWindow : Window
    {
        private List<Task> Tasks;

        public UseWebWalletWindow(Task task)
        {
            InitializeComponent();
            Tasks = new List<Task> { task };
            InitializeTasks();
        }

        public UseWebWalletWindow(List<Task> tasks)
        {
            InitializeComponent();
            Tasks = tasks;
            InitializeTasks();
        }

        public UseWebWalletWindow(IEnumerable<Task> tasks)
        {
            InitializeComponent();
            Tasks = tasks.ToList();
            InitializeTasks();
        }

        private void InitializeTasks()
        {
            tbLink.Text = $"http://localhost:{SignatureRequestServer.ClientPort}/";
            TrackTasks();
        }

        private async void TrackTasks()
        {
            lblWebInstructions.Content = $"{Tasks.Count} action{(Tasks.Count == 1 ? "" : "s")} require{(Tasks.Count == 1 ? "s" : "")} your wallet signature.";
            while (Tasks.Count > 0)
            {
                Task completedTask = await Task.WhenAny(Tasks);
                Tasks.Remove(completedTask);
                // Update UI with the completed tasks count
                lblWebInstructions.Content = $"{Tasks.Count} action{(Tasks.Count == 1 ? "" : "s")} require{(Tasks.Count == 1 ? "s" : "")} your wallet signature.";
            }
            Close();
        }

        private void ClientLink_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                string url = $"http://localhost:{SignatureRequestServer.ClientPort}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c start {url}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private void btnEnd_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            SignatureRequestServer.CancelRequests();
        }
    }
}
