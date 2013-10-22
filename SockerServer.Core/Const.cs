using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SocketServer.Core
{
    /// <summary>
    /// 定数一覧です。
    /// </summary>
    public class Consts
    {
        /// <summary>
        /// Tcpで送受信するメッセージのEOF文字列です。
        /// </summary>
        public static readonly string MESSAGE_EOF = "<<EOF>>";
    }
}
