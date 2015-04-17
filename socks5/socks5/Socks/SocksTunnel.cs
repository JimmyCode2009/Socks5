using socks5.Plugin;
using socks5.Socks;
using socks5.TCP;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace socks5
{
    class SocksTunnel
    {
        public SocksRequest Req;
        public SocksRequest ModifiedReq;

        public SocksClient Client;
        public Client RemoteClient;

        private List<DataHandler> Plugins = new List<DataHandler>();

        private int Timeout = 10000;
        private int PacketSize = 65535;

        public SocksTunnel(SocksClient p, SocksRequest req, SocksRequest req1, int packetSize, int timeout)
        {
            RemoteClient = new Client(new Socket(SocketType.Stream, ProtocolType.Tcp), PacketSize);
            Client = p;
            Req = req;
            ModifiedReq = req1;
            PacketSize = packetSize;
            Timeout = timeout;
        }

        public void Open()
        {
            if (ModifiedReq.Address == null || ModifiedReq.Port <= -1 || ModifiedReq.IP == null) { Client.Client.Disconnect(); return; }

            Console.WriteLine("{0}:{1}({2})", ModifiedReq.Address, ModifiedReq.Port, ModifiedReq.IP);
            foreach (ConnectSocketOverrideHandler conn in PluginLoader.LoadPlugin(typeof(ConnectSocketOverrideHandler)))
                if (conn.Enabled)
                {
                    Client pm = conn.OnConnectOverride(ModifiedReq);
                    if (pm != null)
                    {
                        //check if it's connected.
                        if (pm.Sock.Connected)
                        {
                            RemoteClient = pm;
                            //send request right here.
                            byte[] shit = Req.GetData(true);
                            shit[1] = 0x00;
                            //gucci let's go.
                            Client.Client.Send(shit);
                            ConnectHandler(null);
                            return;
                        }
                    }
                }
            var socketArgs = new SocketAsyncEventArgs { RemoteEndPoint = new IPEndPoint(ModifiedReq.IP, ModifiedReq.Port) };
            socketArgs.Completed += socketArgs_Completed;
            RemoteClient.Sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
            RemoteClient.Sock.ReceiveTimeout = 10000;
            RemoteClient.Sock.SendTimeout = 10000;

            if (!RemoteClient.Sock.ConnectAsync(socketArgs))
                ConnectHandler(socketArgs);
        }

        void socketArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            byte[] request = Req.GetData(true); // Client.Client.Send(Req.GetData());
            if (e.SocketError != SocketError.Success)
            {
                Console.WriteLine("Error while connecting: {0}", e.SocketError.ToString());
                request[1] = (byte)SocksError.Unreachable;
            }
            else
            {
                request[1] = 0x00;
            }

            Client.Client.Send(request);

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    //connected;
                    ConnectHandler(e);
                    break;
            }
        }

        private void ConnectHandler(SocketAsyncEventArgs e)
        {
#if DEBUG
            //Console.WriteLine(string.Format("Tunnel:  \t\r\nClient:{0} -> {1};  \t\r\n Remote:{2} -> {3}", Client.Client.Sock.RemoteEndPoint,
            //    Client.Client.Sock.LocalEndPoint, RemoteClient.Sock.LocalEndPoint, RemoteClient.Sock.RemoteEndPoint));
#endif
            //start receiving from both endpoints.
            try
            {
                //all plugins get the event thrown.
                foreach (DataHandler data in PluginLoader.LoadPlugin(typeof(DataHandler)))
                    Plugins.Push(data);
                Client.Client.onDataReceived += Client_onDataReceived;
                RemoteClient.onDataReceived += RemoteClient_onDataReceived;
                RemoteClient.onClientDisconnected += RemoteClient_onClientDisconnected;
                Client.Client.onClientDisconnected += Client_onClientDisconnected;
                RemoteClient.ReceiveAsync();
                Client.Client.ReceiveAsync();
            }
            catch
            {
                RemoteClient.Disconnect();
                Client.Client.Disconnect();
            }
        }
        bool disconnected = false;
        void Client_onClientDisconnected(object sender, ClientEventArgs e)
        {
            if (disconnected) return;

            Console.WriteLine("Client DC'd @" + Client.Client.Sock.RemoteEndPoint);

            disconnected = true;
            RemoteClient.Disconnect();
        }

        void RemoteClient_onClientDisconnected(object sender, ClientEventArgs e)
        {
            if (disconnected) return;


            Console.WriteLine("\tRemote DC'd @" + RemoteClient.Sock.RemoteEndPoint);

            disconnected = true;

            Client.Client.Disconnect();

        }

        void RemoteClient_onDataReceived(object sender, DataEventArgs e)
        {
            e.Request = this.ModifiedReq;
            foreach (DataHandler f in Plugins)
                if (f.Enabled)
                    f.OnDataReceived(this, e);
            Client.Client.Send(e.Buffer, e.Offset, e.Count);
            if (!RemoteClient.Receiving)
                RemoteClient.ReceiveAsync();
            if (!Client.Client.Receiving)
                Client.Client.ReceiveAsync();
        }

        void Client_onDataReceived(object sender, DataEventArgs e)
        {
            e.Request = this.ModifiedReq;
            foreach (DataHandler f in Plugins)
                if (f.Enabled)
                    f.OnDataSent(this, e);
            RemoteClient.Send(e.Buffer, e.Offset, e.Count);
            if (!Client.Client.Receiving)
                Client.Client.ReceiveAsync();
            if (!RemoteClient.Receiving)
                RemoteClient.ReceiveAsync();
        }
    }
}
