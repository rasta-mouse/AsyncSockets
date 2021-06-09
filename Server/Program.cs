namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new SocketServer(4444);
            server.Run();
        }
    }
}