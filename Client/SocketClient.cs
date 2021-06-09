using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    public class SocketClient
    {
        private readonly string _target;
        private readonly int _port;

        private readonly ManualResetEvent _signal = new(false);
        
        public SocketClient(string target, int port)
        {
            _target = target;
            _port = port;
        }

        public void SendData(string data)
        {
            var ipAddress = IPAddress.Parse(_target);
            var endPoint = new IPEndPoint(ipAddress, _port);
            var client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _signal.Reset();
            
            client.BeginConnect(endPoint, ConnectCallback, client);
            
            // wait for connect to finish
            _signal.WaitOne();
            _signal.Reset();

            var dataBytes = Encoding.UTF8.GetBytes(data);
            
            client.BeginSend(
                dataBytes,
                0,
                dataBytes.Length,
                SocketFlags.None,
                SendCallback,
                client);

            // wait for send to finish
            _signal.WaitOne();
            _signal.Reset();
            
            var state = new SocketState(client);
            client.BeginReceive(
                state.Buffer,
                0,
                SocketState.BufferSize,
                SocketFlags.None,
                ReceiveCallback,
                state);
            
            // wait for receive to finish
            _signal.WaitOne();
            _signal.Reset();
            
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var state = (SocketState) ar.AsyncState;
            var client = state!.ClientSocket;

            var read = client.EndReceive(ar);
            Debug.WriteLine($"Received {read} bytes");
            
            if (state.Final is null)
            {
                state.Final = new byte[read];
                Array.Copy(state.Buffer, state.Final, read);
            }
            else
            {
                var current = state.Final;
                var currentSize = current.Length;
                
                Array.Resize(ref current, current.Length + read);
                Buffer.BlockCopy(state.Buffer, 0, current, currentSize, read);

                state.Final = current;
            }
            
            if (read < SocketState.BufferSize)
            {
                var message = Encoding.UTF8.GetString(state.Final);
                Console.WriteLine($"Reply from Server: {message}");
            }
            else
            {
                client.BeginReceive(
                    state.Buffer,
                    0,
                    SocketState.BufferSize,
                    SocketFlags.None,
                    ReceiveCallback,
                    state);
            }

            _signal.Set();
        }

        private void SendCallback(IAsyncResult ar)
        {
            var client = (Socket) ar.AsyncState;
            var sent = client!.EndSend(ar);
            
            Debug.WriteLine($"Sent {sent} bytes");

            _signal.Set();
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            var client = (Socket) ar.AsyncState;
            client!.EndConnect(ar);

            _signal.Set();
        }
    }
    
    public class SocketState
    {
        public const int BufferSize = 10;
        
        public byte[] Buffer { get; }
        public byte[] Final { get; set; }
        public Socket ClientSocket { get; }

        public SocketState(Socket client)
        {
            ClientSocket = client;
            Buffer = new byte[BufferSize];
        }
    }
}