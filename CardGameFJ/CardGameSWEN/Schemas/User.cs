using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;

namespace CardGameSWEN;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public int Coins { get; set; }

    public string Token { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int? Elo { get; set; } = null;
}

public class ReturnUser
{
    
    public string Name { get; set; }

    public string Bio { get; set; }

    public string Image { get; set; }
}

public class CurrUser
{
    public string Username { get; set; }
    public string Password { get; set; }
}
