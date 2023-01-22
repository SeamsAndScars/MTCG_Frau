namespace CardGameSWEN;

public class Scoreboard
{
    public string Username { get; set; }
    public int Elo { get; set; }

    public int Wins { get; set; }
    public int Losses { get; set; }

    public string WL_Ratio { get; set; } 
}