using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleApp1
{
    /// <summary>
    /// Socket服务端
    /// </summary>
    public class WebSocketServer : IDisposable
    {
        #region 私有变量
        /// <summary>
        /// ip
        /// </summary>
        private string _ip = string.Empty;
        /// <summary>
        /// 端口
        /// </summary>
        private int _port = 0;
        /// <summary>
        /// 服务器地址
        /// </summary>
        private string _serverLocation = string.Empty;
        /// <summary>
        /// Socket对象
        /// </summary>
        private Socket _socket = null;
        /// <summary>
        /// 监听的最大连接数
        /// </summary>
        private int maxListenConnect = 10;
        /// <summary>
        /// 是否关闭Socket对象
        /// </summary>
        private bool isDisposed = false;

        private ILogger logger = null;
        /// <summary>
        /// buffer缓存区字节数
        /// </summary>
        private int maxBufferSize = 0;
        /// <summary>
        /// 第一个字节,以0x00开始
        /// </summary>
        private byte[] FirstByte;
        /// <summary>
        /// 最后一个字节,以0xFF结束
        /// </summary>
        private byte[] LastByte;
        #endregion

        /// <summary>
        /// 存放SocketConnection集合
        /// </summary>
        List<SocketConnection> SocketConnections = new List<SocketConnection>();

        #region 构造函数
        public WebSocketServer(int port, ILogger log)
        {
            _port = port;
            logger = log;
            Initialize();
        }
        #endregion

        /// <summary>
        /// 初始化私有变量
        /// </summary>
        private void Initialize()
        {
            isDisposed = false;
            maxBufferSize = 1024 * 1024;
            maxListenConnect = 500;
            FirstByte = new byte[maxBufferSize];
            LastByte = new byte[maxBufferSize];
            FirstByte[0] = 0x00;
            LastByte[0] = 0xFF;
        }

        /// <summary>
        /// 开启服务
        /// </summary>
        public void StartServer()
        {
            try
            {
                //实例化套接字
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //创建网络端点,包括ip和port
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, _port);
                //将socket与本地端点绑定
                _socket.Bind(endPoint);
                //设置最大监听数
                _socket.Listen(maxListenConnect);

                _ip = endPoint.Address.ToString();
                _serverLocation = string.Format("ws://{0}:{1}", _ip, _port);

                //logger?.Log(string.Format("聊天服务器启动。监听地址：{0}, 端口：{1}", endPoint.Address.ToString(), _port));
                logger?.Info(string.Format("WebSocket服务器地址: ws://{0}:{1}", endPoint.Address.ToString(), _port));

                //开始监听客户端
                Thread thread = new Thread(ListenClientConnect);
                thread.Start();
            }
            catch (Exception ex)
            {
                logger?.Error("启动监听出错", ex);
            }
        }

        /// <summary>
        /// 监听客户端连接
        /// </summary>
        private void ListenClientConnect()
        {
            try
            {
                while (true)
                {
                    //为新建连接创建的Socket
                    Socket socket = _socket.Accept();
                    if (socket != null)
                    {
                        //线程不休眠的话,会导致回调函数的AsyncState状态出异常
                        Thread.Sleep(100);
                        SocketConnection conn = new SocketConnection(_ip, _port, _serverLocation, logger);
                        //绑定事件
                        conn.Connected += SocketConnection_Connected;
                        conn.DataReceived += SocketConnection_DataReceived;
                        conn.ConnectionClosing += SocketConnection_ConnectionClosing;
                        //存入集合,以便在Socket发送消息时发送给所有连接的Socket套接字
                        SocketConnections.Add(conn);
                        conn.Start(socket);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Error("listen client connect error", ex);
            }
        }

        /// <summary>
        /// SocketConnection监听的新连接事件
        /// </summary>
        /// <param name="loginName"></param>
        /// <param name="args"></param>
        private void SocketConnection_Connected(object sender, string loginName)
        {
        }
        /// <summary>
        /// SocketConnection监听的消息接收事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="msgData"></param>
        private void SocketConnection_DataReceived(object sender, string msgData)
        {
            //新用户连接进来时显示欢迎信息
            //SocketConnection socketConnection = sender as SocketConnection;
            Send(msgData);
        }
        /// <summary>
        /// SocketConnection监听的断开连接事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void SocketConnection_ConnectionClosing(object sender, string message)
        {
            if (sender is SocketConnection conn)
            {
                Send(message);
                conn.Close();
                SocketConnections.Remove(conn);
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="message"></param>
        public void Send(string message)
        {
            //给所有连接上的发送消息
            foreach (SocketConnection socket in SocketConnections)
            {
                if (!socket.IsConnected)
                {
                    continue;
                }
                try
                {
                    if (socket.IsDataMasked)
                    {
                        DataFrame dataFrame = new DataFrame(message);
                        socket.Send(dataFrame.GetBytes());
                    }
                    else
                    {
                        socket.Send(FirstByte);
                        socket.Send(Encoding.UTF8.GetBytes(message));
                        socket.Send(LastByte);
                    }
                }
                catch (Exception ex)
                {
                    logger?.Error("发送消息出错", ex);
                }
            }
        }
        
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                if (_socket != null)
                {
                    try
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                        _socket.Close();
                        _socket = null;
                    }
                    catch (ObjectDisposedException) { }
                    catch (Exception ex)
                    {
                        logger?.Error("关闭监听Socket出错", ex);
                    }
                }
                foreach (SocketConnection conn in SocketConnections)
                {
                    conn.Close();
                }
                SocketConnections.Clear();
                //GC.SuppressFinalize(this);
            }
        }
        public void Close()
        {
            Dispose();
        }
    }
}