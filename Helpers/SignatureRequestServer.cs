using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GU_Exchange.Helpers
{
    #region SignatureData
    /// <summary>
    /// Used when returning signed messages, together with the signing address and the resulting signature.
    /// </summary>
    class SignatureData
    {
        public string Address { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
    #endregion

    internal class SignatureRequestServer
    {
        #region Static Fields.
        public static string ClientText { get; set; } = "Click 'Sign' to complete pending actions.";
        public static int ClientPort { get; set; } = Settings.GetServerPort();
        public static string RequestedAddress { get; set; } = "*";
        private static readonly Dictionary<string, TaskCompletionSource<SignatureData>> s_signatureRequestTasks = new();
        private static HttpListener? s_listener;
        #endregion

        #region Check/Change server status.
        /// <summary>
        /// Start the server if it is not already running.
        /// </summary>
        public static void StartServer()
        {
            if (IsRunning())
            {
                return;
            }
            Task.Run(() => ServerLoop());
        }

        /// <summary>
        /// Cancel all signature requests and stop the server.
        /// </summary>
        public static void StopServer()
        {
            CancelRequests();
            if (s_listener != null && s_listener.IsListening)
            {
                s_listener.Stop();
            }
        }

        /// <summary>
        /// Check if the server is currently running.
        /// </summary>
        /// <returns></returns>
        public static bool IsRunning()
        {
            if (s_listener != null && s_listener.IsListening)
            {
                return true;
            }
            return false;
        }
        #endregion

        /// <summary>
        /// Request the user sign the provided message. 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static async Task<SignatureData> RequestSignatureAsync(string message, CancellationToken? cancelToken = null)
        {
            bool previouslyRequested;
            lock (s_signatureRequestTasks)
            {
                previouslyRequested = s_signatureRequestTasks.ContainsKey(message);
            }
            if (previouslyRequested)
            {
                return await s_signatureRequestTasks[message].Task;
            }
            TaskCompletionSource<SignatureData> taskCompletionSource = new TaskCompletionSource<SignatureData>();
            lock (s_signatureRequestTasks)
            {
                s_signatureRequestTasks.Add(message, taskCompletionSource);
            }

            // Attach the cancellation token to the task if provided
            cancelToken?.Register(() =>
            {
                taskCompletionSource.TrySetCanceled();
                lock (s_signatureRequestTasks)
                {
                    s_signatureRequestTasks.Remove(message);
                }
            });
            return await taskCompletionSource.Task;
        }

        /// <summary>
        /// Loop to handle clients using the signature server to GU Exchange.
        /// </summary>
        private static void ServerLoop()
        {
            int startAttempts = 0;
            while (startAttempts < 10)
            {
                try
                {
                    using (s_listener = new HttpListener())
                    {
                        s_listener.Prefixes.Add($"http://localhost:{ClientPort}/");
                        s_listener.Start();
                        startAttempts = 10;
                        Log.Information($"Web server started. Listening on http://localhost:{ClientPort}/");

                        while (s_listener.IsListening)
                        {
                            HttpListenerContext context;

                            try
                            {
                                context = s_listener.GetContext();
                            }
                            catch (HttpListenerException)
                            {
                                break;
                            }

                            if (context != null)
                            {
                                HttpListenerRequest request = context.Request;
                                HttpListenerResponse response = context.Response;

                                if (request.Url == null)
                                {
                                    response.StatusCode = 404;
                                    response.Close();
                                    continue;
                                }

                                string endpoint = request.Url.LocalPath;

                                switch (endpoint)
                                {
                                    case "/":
                                        // Step 3: Serve HTML content
                                        ServeHtml(response);
                                        break;
                                    case "/address":
                                        ReturnActiveAddress(response);
                                        break;
                                    case "/messages":
                                        // Step 4: Return appropriate data for /message
                                        ReturnMessages(response);
                                        break;
                                    case "/action":
                                        // Step 4: Return appropriate data for /action
                                        ReturnClientText(response);
                                        break;
                                    case "/signature":
                                        // Step 4: Handle signature request
                                        HandleSignatureReceived(request, response);
                                        break;
                                    case "/cancel":
                                        CancelRequests(response);
                                        break;
                                    default:
                                        response.StatusCode = 404;
                                        break;
                                }

                                response.Close();
                            }
                        }
                        Log.Information("Server stopped");
                    }
                }
                catch (Exception ex)
                {
                    // Handle other exceptions that may occur during server setup
                    Log.Warning($"An error occurred while running the signing server. {ex.Message}: {ex.StackTrace}");
                    ClientPort++;
                    startAttempts++;
                }

            }
        }

        #region Handle HTTP traffic.
        /// <summary>
        /// Send the signing page to the user.
        /// </summary>
        /// <param name="response"></param>
        static void ServeHtml(HttpListenerResponse response)
        {
            string htmlFilePath = "server/RequestClient.html";
            string htmlContent = File.ReadAllText(htmlFilePath).Replace("%PORT%", ClientPort.ToString());
            byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Send the messages that are requested for signing to the user.
        /// </summary>
        /// <param name="response"></param>
        static void ReturnMessages(HttpListenerResponse response)
        {
            string message;
            lock (s_signatureRequestTasks)
            {
                message = JsonConvert.SerializeObject(s_signatureRequestTasks.Keys);
            }
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Send the string the webclient should show.
        /// </summary>
        /// <param name="response"></param>
        static void ReturnClientText(HttpListenerResponse response)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(ClientText);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Send the address that is requested to be used for signing.
        /// </summary>
        /// <param name="response"></param>
        static void ReturnActiveAddress(HttpListenerResponse response)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(RequestedAddress);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Cancel all pending signature request in response to the user pressing cancel on the webclient.
        /// </summary>
        /// <param name="response"></param>
        static void CancelRequests(HttpListenerResponse response)
        {
            int requestCount = s_signatureRequestTasks.Count;
            byte[] buffer;
            CancelRequests();
            buffer = Encoding.UTF8.GetBytes($"{requestCount} signature requests cancelled.");
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Handle a signature sent back by the webclient.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        static async void HandleSignatureReceived(HttpListenerRequest request, HttpListenerResponse response)
        {
            byte[] buffer = Encoding.UTF8.GetBytes("Invalid signature received");
            using (StreamReader reader = new StreamReader(request.InputStream))
            {
                string requestBody = await reader.ReadToEndAsync();
                SignatureData? signatureData = JsonConvert.DeserializeObject<SignatureData>(requestBody);
                if (signatureData != null && s_signatureRequestTasks.ContainsKey(signatureData.Message))
                {
                    TaskCompletionSource<SignatureData> taskCompletionSource = s_signatureRequestTasks[signatureData.Message];
                    s_signatureRequestTasks.Remove(signatureData.Message);
                    taskCompletionSource.SetResult(signatureData);
                    if (s_signatureRequestTasks.Count == 0)
                    {
                        buffer = Encoding.UTF8.GetBytes("Signing complete!");
                    }
                    else
                    {
                        buffer = Encoding.UTF8.GetBytes($"{s_signatureRequestTasks.Count} signature(s) pending");
                    }

                }
            }


            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        #endregion

        #region Supporting methods
        /// <summary>
        /// Cancel all currently active signature request.
        /// </summary>
        public static void CancelRequests()
        {
            lock (s_signatureRequestTasks)
            {
                foreach (var taskCompletionSource in s_signatureRequestTasks.Values)
                {
                    taskCompletionSource.TrySetCanceled();
                }
                s_signatureRequestTasks.Clear();
            }
        }
        #endregion
    }
}
