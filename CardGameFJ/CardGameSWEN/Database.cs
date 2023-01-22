using Npgsql;

namespace CardGameSWEN;

public class Database
{
    public NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(
            @"Server=localhost;Port=5432;User Id=postgres; Password=admin; Database=TCGSemesterprojekt");
    }
}