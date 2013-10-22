using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SocketServer.Core
{
    /// <summary>
    /// 受信データが格納されているクラスです。
    /// </summary>
    public class ReceivedDataEventArgs
        : EventArgs
    {
        /// <summary>
        /// クライアントとのコネクション
        /// </summary>
        public ClientConnection Client { protected set; get; }

        /// <summary>
        /// 受信データ
        /// </summary>
        public string ReceivedString { protected set; get; }

        /// <summary>
        /// 新しいこのクラスのインスタンスを構築します。
        /// </summary>
        /// <param name="c">クライアントとのコネクション</param>
        /// <param name="str">受信データ</param>
        public ReceivedDataEventArgs(ClientConnection c, string str)
            : base()
        {
            Client = c;
            ReceivedString = str;
        }
    }
}
