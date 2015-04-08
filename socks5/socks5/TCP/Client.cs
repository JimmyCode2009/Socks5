using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace socks5.TCP
{
    public class Client
    {
        public event EventHandler<ClientEventArgs> onClientDisconnected;

        public event EventHandler<DataEventArgs> onDataReceived = delegate { };
        public event EventHandler<DataEventArgs> onDataSent = delegate { };

        public Socket Sock { get; set; }
        private byte[] buffer;
        private int packetSize = 4096;
        public bool Receiving = false;

        public Client(Socket sock, int PacketSize)
        {
            //start the data exchange.
            Sock = sock;
            onClientDisconnected = delegate { };
            buffer = new byte[PacketSize];
            packetSize = PacketSize;
        }

        private bool SocketConnected(Socket s)
        {
            if (!s.Connected) return false;
            bool part1 = s.Poll(10000, SelectMode.SelectError);
            bool part2 = (s.Available == 0);
            if (part1 && part2)
                return false;
            else
                return true;
        }
        private void DataReceived(IAsyncResult res)
        {
            Receiving = false;
            try
            {
                SocketError err = SocketError.Success;
                if (disposed)
                    return;
                var socket = ((Socket)res.AsyncState);
                if (socket == null)
                {
                    Console.WriteLine(string.Format("DataReceived DCing: SOCKET NULL! @{0}", "null"));
                    Disconnect();
                    return;
                }
                int received = 0;
                //if (!SocketConnected(socket))
                //{
                //    Console.WriteLine(string.Format("DataReceived DCing: SOCKDC'd={0}", true));
                //    Disconnect();
                //    return;

                //}
                received = socket.EndReceive(res, out err);
                if (received <= 0 || err != SocketError.Success)
                {
#if DEBUG
                    Console.WriteLine(string.Format("DataReceived DCing: recvd={0},Err={1}", received, err));
#endif
                    this.Disconnect();
                    return;
                }
                // Console.WriteLine("Data Received: " + (received / 1024.0).ToString("#0.00")+ "KB from"+socket.RemoteEndPoint);
                DataEventArgs data = new DataEventArgs(this, buffer, received);
                this.onDataReceived(this, data);

            }
            catch (Exception ex)
            {
               // Console.WriteLine(">>>> Error In DataReceived! DCing! <<<< " );
                this.Disconnect();
            }
        }

        public int Receive(byte[] data, int offset, int count)
        {
            try
            {
               
                int received = this.Sock.Receive(data, offset, count, SocketFlags.None);
                if (received <= 0)
                {
#if DEBUG
                    Console.WriteLine(string.Format("DCing: recvd={0},Err={1}", received, Sock.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error)));
#endif
                    this.Disconnect();
                    return -1;
                }
                DataEventArgs dargs = new DataEventArgs(this, data, received);
                //this.onDataReceived(this, dargs);
                return received;
            }
            catch
            {
                Console.WriteLine(">>>> Error In Receive! DCing! <<<<");
                this.Disconnect();
                return -1;
            }
        }

        public void ReceiveAsync(int buffersize = -1)
        {
            try
            {
                if (Sock == null)
                {
                    return;
                }
                if (buffersize > -1)
                {
                    buffer = new byte[buffersize];
                }
                Receiving = true;
                if (!Sock.Connected)
                {
                    return;
                }
                Sock.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(DataReceived), Sock);
            }
            catch (Exception ex)
            {
               // Console.WriteLine(">>>> Error In ReceiveAsync! DCing! <<<<");
                this.Disconnect();
            }
        }


        public void Disconnect()
        {

            try
            {
                if (!this.disposed)
                {
#if DEBUG
                    Console.WriteLine("DC'ing... @" +
                                      (Sock != null
                                          ? (Sock.Connected ? Sock.RemoteEndPoint.ToString() : "DC'd")
                                          : "NULLSOCK"));
#endif
                    onClientDisconnected(this, new ClientEventArgs(this));
                    if (this.Sock != null && this.Sock.Connected)
                    {
                        this.Sock.Shutdown(SocketShutdown.Both);
                        this.Sock.Close();
                        //this.Sock = null;
                        return;
                    }
                    this.Dispose();
                }
            }
            catch (SocketException sex)
            {
#if DEBUG
                Console.WriteLine("Disconnecting... @" + sex.SocketErrorCode);
#endif
            }
            catch {  }
        }

        private void DataSent(IAsyncResult res)
        {
            try
            {
                int sent = ((Socket)res.AsyncState).EndSend(res);

                if (sent < 0)
                {
                    this.Sock.Shutdown(SocketShutdown.Send);
                    Disconnect();
                    return;
                }
                Console.WriteLine("Data Sent: " + sent / 1024.0 + "KB");
                DataEventArgs data = new DataEventArgs(this, new byte[0] { }, sent);
                this.onDataSent(this, data);
            }
            catch { this.Disconnect(); }
        }

        public bool Send(byte[] buff)
        {
            return Send(buff, 0, buff.Length);
        }

        public void SendAsync(byte[] buff, int offset, int count)
        {
            try
            {
                if (this.Sock != null && this.Sock.Connected)
                {
                    this.Sock.BeginSend(buff, offset, count, SocketFlags.None, new AsyncCallback(DataSent), this.Sock);
                }
            }
            catch
            {
                Console.WriteLine(">>>> Error In SendAsync! DCing! <<<<");
                this.Disconnect();
            }
        }

        public bool Send(byte[] buff, int offset, int count)
        {
            try
            {
                if (this.Sock != null)
                {
                    if (this.Sock.Send(buff, offset, count, SocketFlags.None) <= 0 && count > 0)
                    {
                        Console.WriteLine("Send Dcing");
                        this.Disconnect();
                        return false;
                    }
                    DataEventArgs data = new DataEventArgs(this, buff, count);
                    this.onDataSent(this, data);
                    return true;
                }
                return false;
            }
            catch
            {
                Console.WriteLine(">>>> Error In Send! DCing! <<<<");
                this.Disconnect();
                return false;
            }
        }
        bool disposed = false;

        // Public implementation of Dispose pattern callable by consumers. 
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern. 
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                // Free any other managed objects here. 
                //
                Sock = null;
                buffer = null;
                onClientDisconnected = null;
                onDataReceived = null;
                onDataSent = null;
            }

            // Free any unmanaged objects here. 
            //
            disposed = true;
        }
    }
}
