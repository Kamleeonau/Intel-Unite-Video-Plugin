using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace UniteVideoPlugin
{
    class HTTPServer
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 8998);

        public HTTPServer(int port = 8998)
        {
            //TcpListener listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            listener.Start();
            accept();
        }

        private void accept()
        {
            listener.BeginAcceptTcpClient(new AsyncCallback(onConnection), listener);
        }

        private void onConnection(IAsyncResult result)
        {
            Console.WriteLine("Got incoming connection");
            // accept the next connection
            accept();
            TcpClient client = listener.EndAcceptTcpClient(result);
            NetworkStream ns = client.GetStream();

            // read request from client. we need to be able to time this out


        }
    }
}
