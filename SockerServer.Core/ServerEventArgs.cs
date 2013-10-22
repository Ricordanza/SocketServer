using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SocketServer.Core
{
    /// <summary>
    /// サーバーで発生したイベントデータが格納されているクラスです。
    /// </summary>
    public class ServerEventArgs
        : EventArgs
    {
        /// <summary>
        /// クライアントとのコネクション
        /// </summary>
        public ClientConnection Client { protected set; get; }

        /// <summary>
        /// 新しいこのクラスのインスタンスを構築します。
        /// </summary>
        /// <param name="c">クライアントとのコネクション</param>
        public ServerEventArgs(ClientConnection c)
        {
            Client = c;
        }
    }
}
