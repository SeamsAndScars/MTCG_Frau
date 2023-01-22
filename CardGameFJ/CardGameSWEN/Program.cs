using Npgsql.Replication;
using Npgsql.Replication.TestDecoding;
using static CardGameSWEN.Database;
using static CardGameSWEN.Connection;

namespace CardGameSWEN;

public class Program
{
    static void Main(string[] args)
    {
        var connection = new Connection();
        connection.TcpConnection();
    }
}