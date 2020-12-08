using Newtonsoft.Json;
using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ConsoleApp1
{
    /// <summary>
    /// Socket成功建立的连接
    /// </summary>
    public class SocketConnection : IDisposable
    {
        /// <summary>
        /// 新的Socket连接
        /// </summary>
        private Socket bindSocket = null;

        #region Socket监听事件
        /// <summary>
        /// 新连接事件（参数:loginName）
        /// </summary>
        public event EventHandler<string> Connected;
        /// <summary>
        /// 数据接收事件（参数:data）
        /// </summary>
        public event EventHandler<string> DataReceived;
        /// <summary>
        /// 断开连接事件（参数:message）
        /// </summary>
        public event EventHandler<string> ConnectionClosing;
        ///// <summary>
        ///// 连接已断开事件
        ///// </summary>
        //public event EventHandler ConnectionClosed;
        #endregion

        #region 私有变量
        private string _ip = string.Empty;
        private int _port = 0;
        private string _serverLocation = string.Empty;

        private ILogger logger;
        public string LoginId { get; set; }
        public bool IsDataMasked { get; set; }
        /// <summary>
        /// 最大缓存区字节数
        /// </summary>
        private int maxBufferSize = 0;
        /// <summary>
        /// 握手协议信息
        /// </summary>
        private string handshake = string.Empty;
        /// <summary>
        /// 握手协议信息(new)
        /// </summary>
        private string newHandshake = string.Empty;
        /// <summary>
        /// 接收消息的数据缓存区
        /// </summary>
        public byte[] receivedDataBuffer;
        private byte[] firstByte;
        private byte[] lastByte;
        private byte[] serverKey1;
        private byte[] serverKey2;
        #endregion

        /// <summary>
        /// 是否已链接
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (bindSocket != null)
                    return bindSocket.Connected;
                return false;
            }
        }

        /// <summary>
        /// 绑定的长连接IP+端口
        /// </summary>
        public string ClientIpAndPort
        {
            get
            {
                if (bindSocket != null)
                {
                    try
                    {
                        System.Net.IPEndPoint rep = bindSocket.RemoteEndPoint as System.Net.IPEndPoint;
                        if (rep != null)
                            return string.Format("{0}:{1}", rep.Address.ToString(), rep.Port.ToString());
                    }
                    catch { }
                }
                return string.Empty;
            }
        }

        #region 构造函数
        public SocketConnection(ILogger log)
        {
            Initialize(log);
        }

        public SocketConnection(string ip, int port, string serverLocation, ILogger log)
        {
            _ip = ip;
            _port = port;
            _serverLocation = serverLocation;
            Initialize(log);
        }
        #endregion

        public void Start(Socket socket)
        {
            bindSocket = socket;
            //从开始连接的Socket中异步接收消息
            bindSocket.BeginReceive(receivedDataBuffer, 0, receivedDataBuffer.Length, 0,
                new AsyncCallback(HandleHandshake), socket.Available);
        }

        public void Close()
        {
            try
            {
                if (bindSocket != null)
                {
                    bindSocket.Shutdown(SocketShutdown.Both);
                    bindSocket.Close();
                    bindSocket = null;
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                logger?.Error("关闭客户端Socket连接出错[客户端" + this.ClientIpAndPort + "]", ex);
            }
            //finally
            //{
            //    try
            //    {
            //        ConnectionClosed?.Invoke(this, EventArgs.Empty);
            //    }
            //    catch (Exception ex)
            //    {
            //        logger?.Error("ServerConnection 发送连接关闭事件时出现异常", ex);
            //    }
            //}
        }

        public void Send(byte[] buffer)
        {
            bindSocket?.Send(buffer);
        }

        /// <summary>
        /// 初始化变量
        /// </summary>
        private void Initialize(ILogger log)
        {
            logger = log;
            maxBufferSize = 1024 * 1024;
            receivedDataBuffer = new byte[maxBufferSize];
            firstByte = new byte[maxBufferSize];
            lastByte = new byte[maxBufferSize];
            firstByte[0] = 0x00;
            lastByte[0] = 0xFF;

            //webSocket携带头信息
            handshake = "HTTP/1.1 101 Web Socket Protocol Handshake" + Environment.NewLine;
            handshake += "Upgrade: WebSocket" + Environment.NewLine;
            handshake += "Connection: Upgrade" + Environment.NewLine;
            handshake += "Sec-WebSocket-Origin: " + "{0}" + Environment.NewLine;
            handshake += string.Format("Sec-WebSocket-Location: " + "ws://{0}:{1}" + Environment.NewLine, _ip, _port);
            handshake += Environment.NewLine;

            newHandshake = "HTTP/1.1 101 Switching Protocols" + Environment.NewLine;
            newHandshake += "Upgrade: WebSocket" + Environment.NewLine;
            newHandshake += "Connection: Upgrade" + Environment.NewLine;
            newHandshake += "Sec-WebSocket-Accept: {0}" + Environment.NewLine;
            newHandshake += Environment.NewLine;
        }

        /// <summary>
        /// 处理异步接收消息回调方法
        /// </summary>
        /// <param name="asyncResult"></param>
        private void HandleHandshake(IAsyncResult asyncResult)
        {
            try
            {
                string header = "Sec-WebSocket-Version:";
                int HandshakeLength = (int)asyncResult.AsyncState;
                byte[] last8Bytes = new byte[8];

                UTF8Encoding encoding = new UTF8Encoding();
                string rawClientHandshake = encoding.GetString(receivedDataBuffer, 0, HandshakeLength);

                Array.Copy(receivedDataBuffer, HandshakeLength - 8, last8Bytes, 0, 8);
                //现在使用的是比较新的WebSocket协议
                if (rawClientHandshake.IndexOf(header) != -1)
                {
                    IsDataMasked = true;
                    string[] rawClientHandshakeLines = rawClientHandshake.Split(new string[] { Environment.NewLine }, System.StringSplitOptions.RemoveEmptyEntries);

                    string acceptKey = "";
                    foreach (string line in rawClientHandshakeLines)
                    {
                        if (line.Contains("Sec-WebSocket-Key:"))
                        {
                            acceptKey = ComputeWebSocketHandshakeSecurityHash09(line.Substring(line.IndexOf(":") + 2));
                        }
                    }
                    newHandshake = string.Format(newHandshake, acceptKey);
                    byte[] newHandshakeText = Encoding.UTF8.GetBytes(newHandshake);
                    //将数据异步发送到连接的socket上
                    bindSocket.BeginSend(newHandshakeText, 0, newHandshakeText.Length, SocketFlags.None, HandshakeFinished, null);
                    return;
                }

                string clientHandshake = encoding.GetString(receivedDataBuffer, 0, receivedDataBuffer.Length - 8);
                string[] clientHandshakeLines = clientHandshake.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                logger?.Info("新的连接请求来自:" + bindSocket.LocalEndPoint + ".正准备进行连接...");

                // Welcome the new client
                foreach (string line in clientHandshakeLines)
                {
                    logger?.Info(line);
                    if (line.Contains("Sec-WebSocket-Key1:"))
                        BuildServerPartialKey(1, line.Substring(line.IndexOf(":") + 2));
                    if (line.Contains("Sec-WebSocket-Key2:"))
                        BuildServerPartialKey(2, line.Substring(line.IndexOf(":") + 2));
                    if (line.Contains("Origin:"))
                        try
                        {
                            handshake = string.Format(handshake, line.Substring(line.IndexOf(":") + 2));
                        }
                        catch
                        {
                            handshake = string.Format(handshake, "null");
                        }
                }
                //为客户端建立响应
                byte[] handshakeText = Encoding.UTF8.GetBytes(handshake);
                byte[] serverHandshakeResponse = new byte[handshakeText.Length + 16];
                byte[] serverKey = BuildServerFullKey(last8Bytes);
                Array.Copy(handshakeText, serverHandshakeResponse, handshakeText.Length);
                Array.Copy(serverKey, 0, serverHandshakeResponse, handshakeText.Length, 16);

                logger?.Info("发送握手信息 ...");
                bindSocket.BeginSend(serverHandshakeResponse, 0, handshakeText.Length + 16, 0, HandshakeFinished, null);
                logger?.Info(handshake);
            }
            catch (Exception ex)
            {
                logger?.Error("handshake error", ex);
            }
        }

        /// <summary>
        /// 由服务端向客户端发送消息完成回调
        /// </summary>
        /// <param name="asyncResult"></param>
        private void HandshakeFinished(IAsyncResult asyncResult)
        {
            //结束挂起的异步发送
            bindSocket.EndSend(asyncResult);
            bindSocket.BeginReceive(receivedDataBuffer, 0, receivedDataBuffer.Length,
                0, new AsyncCallback(Read), null);
            Connected?.Invoke(this, "");
        }

        private void Read(IAsyncResult asyncResult)
        {
            if (!bindSocket.Connected)
            {
                return;
            }
            string message = string.Empty;
            DataFrame dataFrame = new DataFrame(receivedDataBuffer);
            try
            {
                if (!IsDataMasked)
                {
                    //WebSocket协议:消息以0x00和0xFF作为填充字节发送
                    UTF8Encoding encoding = new UTF8Encoding();
                    int startIndex = 0;
                    int endIndex = 0;

                    // Search for the start byte
                    while (receivedDataBuffer[startIndex] == firstByte[0])
                    {
                        startIndex++;
                    }
                    // Search for the end byte
                    endIndex = startIndex + 1;
                    while (receivedDataBuffer[endIndex] != lastByte[0] && endIndex != maxBufferSize - 1)
                    {
                        endIndex++;
                    }
                    if (endIndex == maxBufferSize - 1)
                    {
                        endIndex = maxBufferSize;
                    }
                    // Get the message
                    message = encoding.GetString(receivedDataBuffer, startIndex, endIndex - startIndex);
                }//if
                else
                {
                    message = dataFrame.Text;
                }

                if ((message.Length == maxBufferSize && message[0] == Convert.ToChar(65533)) ||
                      message.Length == 0)
                {
                    //断开连接
                    logger?.Info("message");
                    if (string.IsNullOrEmpty(message))
                    {
                        MessageInfo messageInfo = new MessageInfo()
                        {
                            MsgType = MessageType.None,
                            Message = ""
                        };
                        message = JsonConvert.SerializeObject(messageInfo);
                    }
                    ConnectionClosing?.Invoke(this, message);
                }
                else
                {
                    if (DataReceived != null)
                    {
                        logger?.Info("接受到的信息 [\"" + message + "\"]");
                        //消息发送
                        DataReceived?.Invoke(this, message);
                    }
                    Array.Clear(receivedDataBuffer, 0, receivedDataBuffer.Length);
                    bindSocket.BeginReceive(receivedDataBuffer, 0, receivedDataBuffer.Length, 0, Read, null);
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Socket连接将被终止", ex);
                MessageInfo messageInfo = new MessageInfo()
                {
                    MsgType = MessageType.Error,
                    Message = ex.Message + Environment.NewLine + "Socket连接将被终止"
                };
                ConnectionClosing?.Invoke(this, JsonConvert.SerializeObject(messageInfo));
            }
        }

        private byte[] BuildServerFullKey(byte[] last8Bytes)
        {
            byte[] concatenatedKeys = new byte[16];
            Array.Copy(serverKey1, 0, concatenatedKeys, 0, 4);
            Array.Copy(serverKey2, 0, concatenatedKeys, 4, 4);
            Array.Copy(last8Bytes, 0, concatenatedKeys, 8, 8);

            // MD5 Hash
            MD5 MD5Service = MD5.Create();
            return MD5Service.ComputeHash(concatenatedKeys);
        }

        private void BuildServerPartialKey(int keyNum, string clientKey)
        {
            string partialServerKey = "";
            byte[] currentKey;
            int spacesNum = 0;
            char[] keyChars = clientKey.ToCharArray();
            foreach (char currentChar in keyChars)
            {
                if (char.IsDigit(currentChar)) partialServerKey += currentChar;
                if (char.IsWhiteSpace(currentChar)) spacesNum++;
            }
            try
            {
                currentKey = BitConverter.GetBytes((int)(Int64.Parse(partialServerKey) / spacesNum));
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(currentKey);
                }

                if (keyNum == 1)
                {
                    serverKey1 = currentKey;
                }
                else
                {
                    serverKey2 = currentKey;
                }
            }
            catch
            {
                if (serverKey1 != null)
                {
                    Array.Clear(serverKey1, 0, serverKey1.Length);
                }
                if (serverKey2 != null)
                {
                    Array.Clear(serverKey2, 0, serverKey2.Length);
                }
            }
        }

        private string ComputeWebSocketHandshakeSecurityHash09(string secWebSocketKey)
        {
            const String MagicKEY = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            String secWebSocketAccept = String.Empty;
            // 1. Combine the request Sec-WebSocket-Key with magic key.
            String ret = secWebSocketKey + MagicKEY;
            // 2. Compute the SHA1 hash
            SHA1 sha = new SHA1CryptoServiceProvider();
            byte[] sha1Hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ret));
            // 3. Base64 encode the hash
            secWebSocketAccept = Convert.ToBase64String(sha1Hash);
            return secWebSocketAccept;
        }

        public void Dispose()
        {
            //ConnectionClosed = null;
            Close();
        }
    }
}