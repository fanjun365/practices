using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.WebSockets;

    namespace HandlerProject
    {
        //Handler Class which is main entry point.  
        public class WebSocketHandler : HttpTaskAsyncHandler
        {
            public override bool IsReusable
            {
                get
                {
                    return false;
                }
            }

            //Socket Object, Although I have created a Static Dictionary of Socket objects just to show the sample working. What I do is create this Socket object for each user and  
            //keeps it into the dictionary. You can obviously change the implementation in real-time.  
            private WebSocket Socket { get; set; }

            //Overriden menthod Process Request async/await featur has been used.  
            public override async Task ProcessRequestAsync(HttpContext httpContext)
            {
                //task is executed  
                await Task.Run(() =>
                {
                    //Checks if it is a Web Socket Request  
                    if (httpContext.IsWebSocketRequest)
                    {
                        httpContext.AcceptWebSocketRequest(async delegate (AspNetWebSocketContext aspNetWebSocketContext)
                        {
                            Socket = aspNetWebSocketContext.WebSocket;

                            //Checks if the connection is not already closed  
                            while (Socket != null || Socket.State != WebSocketState.Closed)
                            {
                                //Recieves the message from client  
                                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                                WebSocketReceiveResult webSocketReceiveResult = await Socket.ReceiveAsync(buffer, CancellationToken.None);

                                //Here I have handled the case of text-based communication, you can also put down your hode to handle byte arrays, etc.  
                                switch (webSocketReceiveResult.MessageType)
                                {
                                    case WebSocketMessageType.Text:
                                        OnMessageReceived(Encoding.UTF8.GetString(buffer.Array, 0, webSocketReceiveResult.Count));
                                        break;
                                }
                            }
                        });
                    }
                });
            }

            //Sends message to the client  
            private async Task SendMessageAsync(string message, WebSocket socket)
            {
                await SendMessageAsync(Encoding.UTF8.GetBytes(message), socket);
            }

            //Sends the message to the client  
            private async Task SendMessageAsync(byte[] message, WebSocket socket)
            {
                await socket.SendAsync(
                    new ArraySegment<byte>(message),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }

            //This message is fired and parent can forget about this, what this method does it gets the message and push it to the various clients which are connected  
            protected void OnMessageReceived(string message)
            {
                Task task;

                if (message.IndexOf("JOINEDSAMPLECHAT") == 0)
                {
                    WebSocketDictionary.Sockets[message.Replace("JOINEDSAMPLECHAT:", string.Empty)] = Socket;
                    foreach (string key in WebSocketDictionary.Sockets.Keys)
                    {
                        task = SendMessageAsync(string.Concat(message.Replace("JOINEDSAMPLECHAT:", string.Empty), " Joined Chat."), WebSocketDictionary.Sockets[key]);
                    }
                }
                else
                {
                    if (message.IndexOf("BROADCAST") == 0)
                    {
                        foreach (string key in WebSocketDictionary.Sockets.Keys)
                        {
                            task = SendMessageAsync(message.Replace("BROADCAST:", string.Empty), WebSocketDictionary.Sockets[key]);
                        }
                    }
                }
            }
        }

        internal class WebSocketDictionary
        {
            public static Dictionary<string, WebSocket> Sockets = new Dictionary<string, WebSocket>();
        }
    }
}
