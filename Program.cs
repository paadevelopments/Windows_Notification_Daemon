using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;

namespace NotificationDaemon
{
    class Daemon
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

            // CancellationTokenSource source = new CancellationTokenSource();
            // CancellationToken token;

            // Establish WS connection
            BeginConnection:
            ClientWebSocket webSocket = new ClientWebSocket();
            try
            {
                await webSocket.ConnectAsync(serverUri, CancellationToken.None);
            }
            catch
            {
                Console.WriteLine("Unable to connect. Retrying..");
                Thread.Sleep(5000);
                goto BeginConnection;
            }
            Console.WriteLine("WebSocket connection opened");

            // Set cancelation token for message listener
            // token = source.Token;

            // Set "onMessage" listener to websocket to get sent messages from server (unsent notifications)
            _ = Task.Run(async () => {
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult? result = null;
                    ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                    do
                    {
                        try
                        {
                            result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                            #pragma warning disable CS8604
                            string message = Encoding.UTF8.GetString(buffer.Array, 0, result!.Count);
                            #pragma warning restore CS8604
                            Console.WriteLine("Received message: " + message);

                            // Parse message JSON
                            dynamic messages = new string[] { };
                            try
                            {
                                messages = JArray.Parse(message);
                            }
                            catch (Exception ex)
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
                        catch (WebSocketException webSocketException)
                        {
                            if (webSocketException.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                            {
                                Console.WriteLine("\n\nConnection closed. Will reconnect on next request.\n\n");
                                // Turns out, canceling the task will permanently remove the lsitener.
                                // source?.Cancel();
                            }
                        }
                    }
                    while (!result!.EndOfMessage);
                }
            });
            // },token);

            // Begin periodic "send" trigger message to websocket endpoint
            SendAction:
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    string sendMessage = "Hello server, get me the unsent notifications";
                    ArraySegment<byte> sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(sendMessage));
                    await webSocket.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine("\n\nSent message: " + sendMessage);
                }
                catch (WebSocketException webSocketException)
                {
                    Console.WriteLine("Send error due to " + webSocketException.ToString());
                }
                Thread.Sleep(30000);
                goto SendAction;
            }
            else
            {
                Thread.Sleep(5000);
                goto BeginConnection;
            }
        }
    }
}
