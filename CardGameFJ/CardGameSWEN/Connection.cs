using System.Net;
using System.Net.Sockets;
using System.Text;


namespace CardGameSWEN
{
    public class Connection
    {
        public static TimeSpan expirationDate = TimeSpan.FromMinutes(30);

        private static string? logged_in_User;

        private static string? curr_token;
        public static string _TextFormat;

        // public static void Main(string[] args)
        // {
        // Create the server
        public void TcpConnection()
        {
            TcpListener server = new TcpListener(IPAddress.Parse("127.0.0.1"), 10001);
            server.Start();

            Console.WriteLine("Listening for incoming connections...");

            // Handle incoming requests in a loop
            while (true)
            {
                // Wait for a connection
                TcpClient client = server.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(HandleRequest, client);

                //HandleRequest(client);
            }
            // ReSharper disable once FunctionNeverReturns
        }

        public void HandleRequest(object? client)
        {
            var requestHandler = new RequestHandler();
            requestHandler.HandleRequest(client);
        }
    }
}