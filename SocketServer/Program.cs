using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SocketServer.Core;

namespace SocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            MultiConnectionServer server = new MultiConnectionServer();
            server.ReceivedData += (sender, arg) => ((ClientConnection) arg.Client).Send(DateTime.Now.ToLongTimeString());
            server.Listen("0.0.0.0", 5500);

            while (true)
            {
                System.Threading.Thread.Sleep(1000);
            }
        }

    }
}
