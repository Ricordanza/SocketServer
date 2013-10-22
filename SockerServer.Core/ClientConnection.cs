using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;

namespace SocketServer.Core
{
    /// <summary>
    /// クライアントとのコネクションを管理します。<br />
    /// このクラスはクライアントとの接続毎に作成されます。
    /// </summary>
    public class ClientConnection
        : IDisposable
    {
        #region IDisposable メンバ

        /// <summary>
        /// 破棄する
        /// </summary>
        public virtual void Dispose()
        {
            Close();
        }

        #endregion

        #region イベント

        /// <summary>
        /// データを受信した時に発生するイベントです。
        /// </summary>
        public event EventHandler<ReceivedDataEventArgs> ReceivedData;

        /// <summary>
        /// ReceivedDataを発生させます。
        /// </summary>
        /// <param name="e">イベントデータを格納している<see cref="SocketServer.Core.ReceivedDataEventArgs"/></param>
        protected virtual void OnReceivedData(ReceivedDataEventArgs e)
        {
            if (ReceivedData != null)
                ReceivedData(this, e);
        }

        /// <summary>
        /// 接続が切断された場合に発生するイベントです。
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Disconnectedを発生させます。
        /// </summary>
        /// <param name="e">イベントデータを格納している<see cref="System.EventArgs"/></param>
        protected virtual void OnDisconnected(EventArgs e)
        {
            if (Disconnected != null)
                Disconnected(this, e);
        }

        #endregion

        #region フィールド

        /// <summary>
        /// 受信開始フラグ
        /// </summary>
        private bool _startedReceiving = false;

        /// <summary>
        /// 同期オブジェクト
        /// </summary>
        private readonly object _lock = new object();

        #endregion

        #region プロパティ

        /// <summary>
        /// 使用する文字コード
        /// </summary>
        protected Encoding Encoding { set; get; }

        /// <summary>
        /// 接続の基になるSocket
        /// </summary>
        protected Socket Client { set; get; }

        /// <summary>
        /// ローカルエンドポイント
        /// </summary>
        public IPEndPoint LocalEndPoint { protected set; get; }

        /// <summary>
        /// ローカルエンドポイント
        /// </summary>
        public IPEndPoint RemoteEndPoint { protected set; get; }

        /// <summary>
        /// 接続が閉じているか判定します。
        /// </summary>
        public bool IsClosed { get { return Client == null; } }

        /// <summary>
        /// 一回で受信できる最大バイト
        /// </summary>
        protected int MaxReceiveLenght { set; get; }

        /// <summary>
        /// 受信したデータ
        /// </summary>
        protected MemoryStream ReceivedBytes { set; get; }

        #endregion

        /// <summary>
        /// 新しいこのクラスのインスタンスを構築します。
        /// </summary>
        public ClientConnection()
            : this(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>
        /// 新しいこのクラスのインスタンスを構築します。
        /// </summary>
        /// <param name="soc">接続の基となるソケット</param>
        public ClientConnection(Socket soc)
            : base()
        {
            Initialize();

            Client = soc;
            LocalEndPoint = (IPEndPoint) soc.LocalEndPoint;
            RemoteEndPoint = (IPEndPoint) soc.RemoteEndPoint;
        }

        /// <summary>
        /// このクラスのインスタンスを初期化します。
        /// </summary>
        private void Initialize()
        {
            Encoding = Encoding.UTF8;
            MaxReceiveLenght = int.MaxValue;
        }

        /// <summary>
        /// サーバーに接続します。
        /// </summary>
        /// <param name="host">ホスト名</param>
        /// <param name="port">ポート番号</param>
        public void Connect(string host, int port)
        {
            if (IsClosed)
                throw new ApplicationException("閉じています。");
            if (Client.Connected)
                throw new ApplicationException("すでに接続されています。");

            // 接続する
            IPEndPoint ipEnd = new IPEndPoint(Dns.Resolve(host).AddressList[0], port);
            Client.Connect(ipEnd);

            LocalEndPoint = (IPEndPoint) Client.LocalEndPoint;
            RemoteEndPoint = (IPEndPoint) Client.RemoteEndPoint;

            // 非同期データ受信を開始する
            this.StartReceive();
        }

        /// <summary>
        /// 切断する
        /// </summary>
        public void Close()
        {
            lock (_lock)
            {
                if (IsClosed)
                    return;

                // 接続を閉じる
                Client.Shutdown(SocketShutdown.Both);
                Client.Close();
                Client = null;
                if (ReceivedBytes != null)
                {
                    ReceivedBytes.Close();
                    ReceivedBytes = null;
                }
            }
            // イベントを発生
            this.OnDisconnected(EventArgs.Empty);
        }

        /// <summary>
        /// 文字列を送信します。
        /// </summary>
        /// <param name="str">送信する文字列</param>
        public void Send(string str)
        {
            if (IsClosed)
                throw new ApplicationException("閉じています。");

            // 文字列をByte型配列に変換
            byte[] sendBytes = this.Encoding.GetBytes(str + Consts.MESSAGE_EOF);

            lock (this._lock)
            {
                // データを送信する
                Client.Send(sendBytes);
            }
        }

        /// <summary>
        /// メッセージを送信する
        /// </summary>
        /// <param name="msg">送信するメッセージ</param>
        public virtual void SendMessage(string msg)
        {
            // EOFを削除して送信
            Send(msg.Replace(Consts.MESSAGE_EOF, string.Empty));
        }

        /// <summary>
        /// データの非同期受信を開始します。
        /// </summary>
        public void StartReceive()
        {
            if (IsClosed)
                throw new ApplicationException("閉じています。");
            if (_startedReceiving)
                throw new ApplicationException("StartReceiveがすでに呼び出されています。");

            // 初期化
            byte[] receiveBuffer = new byte[1024];
            ReceivedBytes = new MemoryStream();
            _startedReceiving = true;

            // 非同期受信を開始
            this.Client.BeginReceive(receiveBuffer,
                0, receiveBuffer.Length,
                SocketFlags.None, new AsyncCallback(ReceiveDataCallback),
                receiveBuffer);
        }

        /// <summary>
        /// BeginReceiveのコールバックイベントです。
        /// </summary>
        /// <param name="result">非同期操作のステータス</param>
        private void ReceiveDataCallback(IAsyncResult result)
        {
            int len = -1;
            // 読み込んだ長さを取得
            try
            {
                lock (this._lock)
                    len = Client.EndReceive(result);
            }
            catch
            {
            }

            //切断されたか調べる
            if (len <= 0)
            {
                Close();
                return;
            }

            // 受信したデータを取得する
            byte[] receiveBuffer = (byte[]) result.AsyncState;

            // 受信したデータを蓄積する
            ReceivedBytes.Write(receiveBuffer, 0, len);
            // 最大値を超えた時は、接続を閉じる
            if (ReceivedBytes.Length > MaxReceiveLenght)
            {
                Close();
                return;
            }

            // 最後まで受信したか調べる
            if (ReceivedBytes.Length >= 2)
            {
                ReceivedBytes.Seek(-2, SeekOrigin.End);

                // 受信したデータを文字列に変換
                string str = Encoding.GetString(ReceivedBytes.ToArray());

                // 終了文字が存在する場合
                if (str.IndexOf(Consts.MESSAGE_EOF) > -1)
                {
                    // 最後まで受信した時
                    ReceivedBytes.Close();

                    // コンソールに受信したメッセージを落とす
                    Console.WriteLine(str.Replace(Consts.MESSAGE_EOF, string.Empty));

                    // イベントを発生させる
                    OnReceivedData(new ReceivedDataEventArgs(this, str));

                    // 受信データを初期化する
                    ReceivedBytes = new System.IO.MemoryStream();
                }
                else
                    ReceivedBytes.Seek(0, SeekOrigin.End);
            }

            lock (_lock)
            {
                // 再び受信開始
                Client.BeginReceive(receiveBuffer,
                    0, receiveBuffer.Length,
                    SocketFlags.None, new AsyncCallback(ReceiveDataCallback)
                    , receiveBuffer);
            }
        }
    }
}
