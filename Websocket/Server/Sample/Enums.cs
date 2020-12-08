using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    public enum MessageType
    {
        Error = -1,
        None = 0,
        /// <summary>
        /// 登录
        /// </summary>
        Login = 1,
        /// <summary>
        /// 退出
        /// </summary>
        Logout = 2,
        /// <summary>
        /// 聊天消息
        /// </summary>
        ChatInfo = 3,
    }


    public class MessageInfo
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public Guid Identity { get; set; }
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// 消息类型
        /// </summary>
        public MessageType MsgType { get; set; }
        /// <summary>
        /// 发送信息
        /// </summary>
        public string Message { get; set; }
    }

}
