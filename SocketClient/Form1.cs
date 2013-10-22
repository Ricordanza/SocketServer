using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace SocketClient
{
    public partial class Form1 : Form
    {
        private TcpClient _client;

        public Form1()
        {
            InitializeComponent();

            _client = new TcpClient();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text = Dns.GetHostName();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _client.Connect(textBox1.Text, 5500);
            button1.Enabled = false;
            textBox1.Enabled = false;
            button2.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox2.Text.Trim()))
                return;

            NetworkStream stream = _client.GetStream();
            string data = textBox2.Text + SocketServer.Core.Consts.MESSAGE_EOF;
            byte[] sendData = Encoding.Default.GetBytes(data);

            stream.Write(sendData, 0, sendData.Length);

            //サーバーから送られたデータを受信する
            MemoryStream ms = new MemoryStream();
            byte[] resBytes = new byte[256];
            int resSize;
            do
            {
                // データの一部を受信する
                resSize = stream.Read(resBytes, 0, resBytes.Length);
                // Readが0を返した時はサーバーが切断したと判断
                if (resSize == 0)
                {
                    Console.WriteLine("サーバーが切断しました。");
                    return;
                }

                // 受信したデータを蓄積する
                ms.Write(resBytes, 0, resSize);
            }
            while (stream.DataAvailable);

            // 受信したデータを文字列に変換
            string recivingData = Encoding.Default.GetString(ms.ToArray());
            Console.WriteLine("Recived : " + recivingData.Replace(SocketServer.Core.Consts.MESSAGE_EOF, string.Empty));

            textBox2.Focus();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_client != null)
                _client.Close();
        }
    }
}
