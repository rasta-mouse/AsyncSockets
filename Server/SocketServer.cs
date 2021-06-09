using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Server
{
    public class SocketServer
    {
        private readonly int _bindPort;
        private readonly ManualResetEvent _signal = new(false);

        public SocketServer(int bindPort)
        {
            _bindPort = bindPort;
        }
        
        public void Run()
        {
            var address = IPAddress.Any;
            var endPoint = new IPEndPoint(address, _bindPort);
            
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(endPoint);
            socket.Listen(100);

            while (true)
            {
                _signal.Reset();
                socket.BeginAccept(AcceptCallback, socket);
                _signal.WaitOne();
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            var socket = (Socket) ar.AsyncState;
            var client = socket?.EndAccept(ar);

            _signal.Set();

            var state = new SocketState(client);
            
            client?.BeginReceive(
                state.Buffer,
                0,
                SocketState.BufferSize,
                SocketFlags.None,
                ReceiveCallback,
                state);
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

            // interesting bug here
            // if message size == BufferSize on the last message, it will continue trying to read
            // even though there's no more data being sent
            if (read < SocketState.BufferSize)
            {
                SendResponse(client, state.Final);
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
        }

        private void SendResponse(Socket client, byte[] data)
        {
            var message = Encoding.UTF8.GetString(data);
            Console.WriteLine($"Message from Client: {message}");
            
            var response = $"The time is {DateTime.UtcNow}";
            var bytes = Encoding.UTF8.GetBytes(response);

            client.BeginSend(
                bytes,
                0,
                bytes.Length,
                SocketFlags.None,
                SendCallback,
                client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            var client = (Socket) ar.AsyncState;
            var sent = client!.EndSend(ar);
            
            Debug.WriteLine($"Sent {sent} bytes");
            
            client.Shutdown(SocketShutdown.Both);
            client.Close();
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