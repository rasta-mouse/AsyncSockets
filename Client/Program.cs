using System;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new SocketClient("127.0.0.1", 4444);

            while (true)
            {
                Console.Write("> ");
                
                var read = Console.ReadLine();
                client.SendData(read);
            }
        }
    }
}