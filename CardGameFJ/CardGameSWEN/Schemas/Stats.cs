namespace CardGameSWEN;

public class Stats
{
    public string Name { get; set; }
    public int Elo { get; set; } = 100;
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;

    public string WL_Ratio { get; set; } = "0";

}