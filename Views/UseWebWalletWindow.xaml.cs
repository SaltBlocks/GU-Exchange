using GU_Exchange.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GU_Exchange
{
    /// <summary>
    /// Interaction logic for UseWebWalletWindow.xaml
    /// </summary>
    public partial class UseWebWalletWindow : Window
    {
        #region Class Parameters
        private readonly List<Task> Tasks;
        public bool AutoClose { get; set; }
        #endregion

        #region Constructors
        public UseWebWalletWindow()
        {
            InitializeComponent();
            Tasks = new();
            AutoClose = false;
            InitializeTasks();
        }
        public UseWebWalletWindow(Task task)
        {
            InitializeComponent();
            Tasks = new List<Task> { task };
            AutoClose = true;
            InitializeTasks();
        }

        public UseWebWalletWindow(List<Task> tasks)
        {
            InitializeComponent();
            Tasks = tasks;
            AutoClose = true;
            InitializeTasks();
        }

        public UseWebWalletWindow(IEnumerable<Task> tasks)
        {
            InitializeComponent();
            Tasks = tasks.ToList();
            AutoClose = true;
            InitializeTasks();
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Open the signing page in the browser.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                Log.Warning($"Failed to open Siging page in browser: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Close the window if the cancel button is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnEnd_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Cancel active requests if the window is closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            SignatureRequestServer.CancelRequests();
        }
        #endregion

        #region Supporting functions
        public void AddTask(Task task)
        {
            Tasks.Add(task);
            lblWebInstructions.Content = $"{Tasks.Count} action{(Tasks.Count == 1 ? "" : "s")} require{(Tasks.Count == 1 ? "s" : "")} your wallet signature.";
            if (Tasks.Count() == 1)
                TrackTasks();
        }
        /// <summary>
        /// Setup the window text and start tracking tasks.
        /// </summary>
        private void InitializeTasks()
        {
            tbLink.Text = $"http://localhost:{SignatureRequestServer.ClientPort}/";
            TrackTasks();
        }

        /// <summary>
        /// Keep track of active signing tasks.
        /// Close this window when all are complete.
        /// </summary>
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
            if (AutoClose)
                Close();
        }
        #endregion
    }
}
