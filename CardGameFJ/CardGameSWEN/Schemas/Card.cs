using System.Linq.Expressions;

namespace CardGameSWEN;

public class Card
{
    public string Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Damage { get; set; }
    public int package_id { get; set; }
    
    public string Type { get; set; }
    
    public string Element { get; set; }

}

public class ReturnCard
{

    public string Id { get; set; }
    public string Name { get; set; }
    public double Damage { get; set; }
}



