using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace SocketServer.Core
{
    #region 列挙型
    /// <summary>
    /// サーバーの状態
    /// </summary>
    public enum ServerState
    {
        None,
        Listening,
        Stopped
    }
    #endregion

    /// <summary>
    /// 複数クライアントから同時接続が可能なTcpServerです。
    /// </summary>
    public class MultiConnectionServer
    {
		#region IDisposable メンバ
		/// <summary>
		/// 破棄する
		/// </summary>
		public virtual void Dispose()
		{
			this.Close();
		}
		#endregion

        #region フィールド

        /// <summary>
        /// 同期オブジェクト
        /// </summary>
        private readonly object _lock = new object();

        #endregion

		#region イベント

		/// <summary>
        /// クライアントがデータを受信した時に発生するイベントです。
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
		/// クライアントが切断した場合に発生します。
		/// </summary>
        public event EventHandler<ServerEventArgs> DisconnectedClient;

        /// <summary>
        /// DisconnectedClientを発生させます。
        /// </summary>
        /// <param name="e">イベントデータを格納している<see cref="SocketServer.Core.ServerEventArgs"/></param>
		protected virtual void OnDisconnectedClient(ServerEventArgs e)
		{
			if (DisconnectedClient != null)
				DisconnectedClient(this, e);
		}

		#endregion

		#region プロパティ

		/// <summary>
		/// 接続の基になるSocket
		/// </summary>
        protected Socket Server { set; get; }

		/// <summary>
		/// 状態
		/// </summary>
        public ServerState ServerState { protected set; get; }

		/// <summary>
		/// ローカルエンドポイント
		/// </summary>
		public IPEndPoint LocalEndPoint { protected set; get; }

		/// <summary>
		/// 接続中のクライアント
		/// </summary>
        public virtual List<ClientConnection> AcceptedClients { protected set; get; }

		/// <summary>
		/// 同時接続を許可するクライアント数
		/// </summary>
        public int MaxClients { protected set; get; }

		#endregion

		/// <summary>
        /// 新しいこのクラスのインスタンスを構築します。
		/// </summary>
        public MultiConnectionServer()
		{
            MaxClients = 100;
			Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            AcceptedClients = new List<ClientConnection>();
		}

		/// <summary>
		/// Listenを開始します。
		/// </summary>
		/// <param name="host">ホスト名</param>
		/// <param name="portNum">ポート番号</param>
        /// <param name="backlog">保留中の接続のキューの最大長</param>
		public void Listen(string host, int portNum, int backlog)
		{
			if (Server == null)
				throw new ApplicationException("破棄されています。");
			if (ServerState != ServerState.None)
				throw new ApplicationException("すでにListen中です。");

			LocalEndPoint = new IPEndPoint(Dns.Resolve(host).AddressList[0], portNum);
            Server.Bind(LocalEndPoint);
				
			// Listenを開始する
            Server.Listen(backlog);
            ServerState = ServerState.Listening;

			// 接続要求施行を開始する
            Server.BeginAccept(new AsyncCallback(this.AcceptCallback), null);
		}

        /// <summary>
        /// Listenを開始します。
        /// </summary>
        /// <param name="host">ホスト名</param>
        /// <param name="portNum">ポート番号</param>
		public void Listen(string host, int portNum)
		{
			Listen(host, portNum, 100);
		}

		/// <summary>
		/// 接続中のすべてのクライアントにメッセージを送信します。
		/// </summary>
		/// <param name="str">送信する文字列</param>
		public virtual void SendMessageToAllClients(string msg)
		{
            // EOFを削除して送信
            SendToAllClients(msg.Replace(Consts.MESSAGE_EOF, string.Empty));
		}

		/// <summary>
		/// クライアントにエラーメッセージを送信します。
		/// </summary>
		/// <param name="client">送信先のクライアント</param>
		/// <param name="msg">送信するエラーメッセージ</param>
		protected virtual void SendErrorMessage(ClientConnection client, string msg)
		{
			client.SendMessage(msg);
		}

		/// <summary>
		/// 監視を中止します。
		/// </summary>
		public void StopListen()
		{
			lock (_lock)
			{
				if (Server == null)
					return;

                Server.Close();
                Server = null;
				ServerState = ServerState.Stopped;
			}
		}

		/// <summary>
		/// 閉じる
		/// </summary>
		public void Close()
		{
			StopListen();
			CloseAllClients();
		}

		/// <summary>
		/// 接続中のクライアントを閉じる
		/// </summary>
		public void CloseClient(ClientConnection client)
		{
			AcceptedClients.Remove(client);
			client.Close();
		}

		/// <summary>
		/// 接続中のすべてのクライアントを閉じる
		/// </summary>
		public void CloseAllClients()
		{
            lock (_lock)
			{
				while (AcceptedClients.Count > 0)
                    CloseClient(AcceptedClients[0]);
			}
		}

		/// <summary>
		/// 接続中のすべてのクライアントに文字列を送信します。
		/// </summary>
		/// <param name="str">送信する文字列</param>
		protected void SendToAllClients(string str)
		{
            lock (_lock)
            {
                foreach(var client in AcceptedClients)
                    client.Send(str);
            }
		}

		/// <summary>
		/// サーバーで使用するクライアントクラスを作成する
		/// </summary>
		/// <param name="soc">基になるSocket</param>
		/// <returns>クライアントクラス</returns>
		protected virtual ClientConnection CreateChatClient(Socket soc)
		{
			return new ClientConnection(soc);
		}

        /// <summary>
        /// BeginAcceptのコールバックイベントです。
        /// </summary>
        /// <param name="result">非同期操作のステータス</param>
		private void AcceptCallback(IAsyncResult result)
		{
			// 接続要求を受け入れる
			Socket soc = null;
			try
			{
                lock (_lock)
                    soc = Server.EndAccept(result);
			}
			catch
			{
				Close();
				return;
			}

            // ClientConnectionの作成
			ClientConnection client = this.CreateChatClient(soc);

			// 最大数を超えていないか
			if (AcceptedClients.Count >= this.MaxClients)
			{
				client.Close();
			}
			else
			{
				// コレクションに追加
				AcceptedClients.Add(client);
				// イベントハンドラの追加
				client.Disconnected += new EventHandler(client_Disconnected);
				client.ReceivedData += new EventHandler<ReceivedDataEventArgs>(client_ReceivedData);

				// データ受信開始
				if (!client.IsClosed)
					client.StartReceive();
			}

			// 接続要求施行を再開する
			Server.BeginAccept(new AsyncCallback(AcceptCallback), null);
		}

		#region クライアントのイベントハンドラ

        /// <summary>
        /// クライアントが切断した時のイベントです。
        /// </summary>
        /// <param name="sender">イベントソース</param>
        /// <param name="e">イベントデータを格納している<see cref="System.EventArgs"/></param>
		private void client_Disconnected(object sender, EventArgs e)
		{
            ClientConnection client = sender as ClientConnection;

			// リストから削除する
            AcceptedClients.Remove(client);

			//イベントを発生
            OnDisconnectedClient(new ServerEventArgs(client));
		}

        /// <summary>
        /// クライアントからデータを受信した時のイベントです。
        /// </summary>
        /// <param name="sender">イベントソース</param>
        /// <param name="e">イベントデータを格納している<see cref="SocketServer.Core.ReceivedDataEventArgs"/></param>
		private void client_ReceivedData(object sender, ReceivedDataEventArgs e)
		{
			// イベントを発生
			OnReceivedData(new ReceivedDataEventArgs(
				(ClientConnection) sender, e.ReceivedString));
		}
		#endregion
    }
}
