using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;

namespace WebSocketClientExample
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            if (!args.Contains("-url"))
            {
                Console.WriteLine("Missing websocket URL");
                return;
            }
            int urlValueIndex = Array.IndexOf(args, "-url") + 1;
            if (!(args.ElementAtOrDefault(urlValueIndex) != null))
            {
                Console.WriteLine("Missing websocket URL");
                return;
            }
            String url = args[urlValueIndex];
            if (url.Trim().Length == 0)
            {
                Console.WriteLine("Empty websocket URL");
                return;
            }
            Uri serverUri = new Uri(url);
            ClientWebSocket webSocket = new ClientWebSocket();
            try
            {
                await webSocket.ConnectAsync(serverUri, CancellationToken.None);
            }
            catch
            {
                Console.WriteLine("Unable to connect");
                return;
            }
            Console.WriteLine("WebSocket connection opened");

            // Set "onMessage" listener to websocket to get sent messages from server (unsent notifications)
            _ = Task.Run(async () => {
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                    do
                    {
                        result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                        #pragma warning disable CS8604
                        string message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        #pragma warning restore CS8604
                        Console.WriteLine("Received message: " + message);

                        // Parse message JSON
                        dynamic messages = new string[] {};
                        try
                        {
                            messages = JArray.Parse(message);
                        } catch(Exception ex)
                        {
                            Console.WriteLine(ex);
                            return;
                        }

                        // Loop and show notification
                        foreach (var content in messages)
                        {
                            new ToastContentBuilder()
                            .AddArgument("action", "openApp").AddText("Daemon Alert").AddText(content.ToString()).Show();
                        }
                    }
                    while (!result.EndOfMessage);
                }
            });

            // Begin periodic "send" trigger message to websocket endpoint
            do
            {
                string sendMessage = "Hello server, get me the unsent notifications";
                ArraySegment<byte> sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(sendMessage));
                await webSocket.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                Console.WriteLine("\n\nSent message: " + sendMessage);
                
                // Add some trigger interval delay (in milliseconds)
                // 60000ms == 1 minute
                Thread.Sleep(60000);
            } while (true);
        }
    }
}
