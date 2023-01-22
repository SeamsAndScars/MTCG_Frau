using Newtonsoft.Json;
using Npgsql;
using static CardGameSWEN.UserFunctions;
using static CardGameSWEN.Battle;
using static CardGameSWEN.CardFunctions;

namespace CardGameSWEN
{
    public class BattleMechanics
    {
        private static bool _userWaiting = false;
        private CardFunctions _cardFunctions = new CardFunctions();
        private UserFunctions _userFunctions = new UserFunctions();
        private Battle _battle = new Battle();

        public class BattleResult
        {
            public List<RoundResult>? RoundResults { get; init; }
            public string? Winner { get; init; }
            public List<Card>? Player1Deck { get; set; }
            public List<Card>? Player2Deck { get; set; }

            public string? Player1Name { get; init; }

            public string? Player2Name { get; init; }
        }

        public class RoundResult
        {
            public string? Player1Card { get; init; }
            public string? Player2Card { get; init; }
            public double Player1Damage { get; init; }
            public double Player2Damage { get; init; }
            public string? Winner { get; init; }
        }

        private static readonly Dictionary<string, User> Users = new Dictionary<string, User>();

        public BattleResult EnterLobby(string token)
        {
            //check if user is waiting
            if (_userWaiting)
            {
                Users.Add(token, _userFunctions.GetUser(token));
                var player1 = Users.First();
                var player2 = Users.Last();
                var decks = BattleDecks(player1, player2);
                var battleResult =
                    StartBattle(decks.Player1Deck, decks.Player2Deck, player1.Value.Username, player2.Value.Username);
                Users.Clear();
                _userWaiting = false;
                return battleResult;
            }
            else
            {
                Users.Add(token, _userFunctions.GetUser(token));
                _userWaiting = true;
            }

            return null!;
        }


        private (List<Card>Player1Deck, List<Card>Player2Deck) BattleDecks(KeyValuePair<string, User> player1,
            KeyValuePair<string, User> player2)
        {
            List<Card> player1Deck = new List<Card>();
            List<Card> player2Deck = new List<Card>();


            player1Deck = _cardFunctions.GetDeck(player1.Key);

            player2Deck = _cardFunctions.GetDeck(player2.Key);


            return (player1Deck, player2Deck);
        }

        public static BattleResult StartBattle(List<Card> player1Deck, List<Card> player2Deck, string player1Name,
            string player2Name)
        {
            int roundCount = 0;
            int player1Wins = 0;
            int player2Wins = 0;

            List<RoundResult> roundResults = new List<RoundResult>();

            //Rounds
            Random random = new Random();
            while (roundCount < 100 && player1Deck.Count > 0 && player2Deck.Count > 0)
            {
                // Choose cards for the round
                // Choose cards for the round
                int randomIndex1 = random.Next(player1Deck.Count);
                int randomIndex2 = random.Next(player2Deck.Count);
                Card player1Card = player1Deck[randomIndex1];
                Card player2Card = player2Deck[randomIndex2];

                // Determine the winner of the round
                string roundWinner = null!;

                //Fight
                (double player1Damage, double player2Damage) = Fight(player1Card, player2Card);

                if (player1Damage > player2Damage)
                {
                    roundWinner = player1Name;
                    player1Wins++;
                    player1Deck.Add(player2Card);
                    player2Deck.Remove(player2Card);
                }
                else if (player2Damage > player1Damage)
                {
                    roundWinner = player2Name;
                    player2Wins++;
                    player2Deck.Add(player1Card);
                    player1Deck.Remove(player1Card);
                }
                else if (player1Damage == player2Damage)
                {
                    roundWinner = "Draw";
                    // No card should be removed
                }

                // Add the round result to the list
                roundResults.Add(new RoundResult
                {
                    Player1Card = player1Card.Name,
                    Player2Card = player2Card.Name,
                    Player1Damage = player1Damage,
                    Player2Damage = player2Damage,
                    Winner = roundWinner
                });

                roundCount++;
            }

            // Determine the overall winner of the battle
            string overallWinner = null!;
            if (player1Wins > player2Wins)
            {
                overallWinner = player1Name;
            }
            else if (player2Wins > player1Wins)
            {
                overallWinner = player2Name;
            }
            else if (player1Wins == player2Wins)
            {
                overallWinner = "A Draw! How Cringe.";
            }

            return new BattleResult
            {
                RoundResults = roundResults,
                Winner = overallWinner,
                Player1Deck = player1Deck,
                Player2Deck = player2Deck,
                Player1Name = player1Name,
                Player2Name = player2Name
            };
        }

        public (int, string) BattleString(string token)
        {
            var userID = _userFunctions.GetUserIdFromToken(token);
            if (userID == 0)
            {
                return (401, "Access token is missing or invalid");
            }

            var battleResult = EnterLobby(token);
            var battleString = "";
            if (battleResult == null)
            {
                return (202, "Waiting for another player to join the lobby");
            }

            var numberOfRound = battleResult.RoundResults.Count();
            int currRound = 0;
            string? Loser = "";

            foreach (var round in battleResult.RoundResults)
            {
                currRound++;
                battleString += "|Round:" + currRound + "|\n" + battleResult.Player1Name + " Card: " +
                                round.Player1Card +
                                "(" +
                                round.Player1Damage + ")" + " vs. " + battleResult.Player2Name +
                                " Card: " + round.Player2Card + "(" + round.Player2Damage + ")" +
                                "\n|Winner:" + round.Winner + "|\n\n";
            }

            battleString += "\n" + "Number of Round Played:" + numberOfRound + "\n" + "--- Overall Winner: " +
                            battleResult.Winner + " ---\n" + "|--5 Coins earned--|\n" + "|--3 Elo earned--|\n";


            if (battleResult.Winner == battleResult.Player1Name)
            {
                Loser = battleResult.Player2Name;
            }
            else if (battleResult.Winner == battleResult.Player2Name)
            {
                Loser = battleResult.Player1Name;
            }
            else
            {
                Loser = "Draw";
            }

            //Elo gain and loss
            if (Loser != "Draw")
            {
                _battle.UpdateElo(battleResult.Winner, Loser);
            }

            return (200, battleString);
        }


        private static (double, double) Fight(Card player1Card, Card player2Card)
        {
            //Check if Spell

            (player1Card.Element, player1Card.Type) = CheckElement(player1Card);
            (player2Card.Element, player2Card.Type) = CheckElement(player2Card);

            // Calculate damage for each card
            double player1Damage = player1Card.Damage;
            double player2Damage = player2Card.Damage;


            // Apply element type bonus if applicable
            if (player1Card.Type == "Monster" && player2Card.Type == "Monster")
            {
                // No element benefits or disadvantages for monsters fighting against each other
                // Continue with damage calculation
            }
            else
            {
                switch (player1Card.Element)
                {
                    case "Water":
                        if (player2Card.Element == "Fire")
                        {
                            player1Damage *= 2;
                        }
                        else if (player2Card.Element == "Normal")
                        {
                            player1Damage /= 2;
                        }

                        break;
                    case "Fire":
                        if (player2Card.Element == "Water")
                        {
                            player1Damage /= 2;
                        }
                        else if (player2Card.Element == "Normal")
                        {
                            player1Damage *= 2;
                        }

                        break;
                    case "Normal":
                        if (player2Card.Element == "Water")
                        {
                            player1Damage *= 2;
                        }
                        else if (player2Card.Element == "Fire")
                        {
                            player1Damage /= 2;
                        }

                        break;
                }

                switch (player2Card.Element)
                {
                    case "Water":
                        if (player1Card.Element == "Fire")
                        {
                            player2Damage *= 2;
                        }
                        else if (player1Card.Element == "Normal")
                        {
                            player2Damage /= 2;
                        }

                        break;
                    case "Fire":
                        if (player1Card.Element == "Normal")
                        {
                            player2Damage *= 2;
                        }
                        else if (player1Card.Element == "Water")
                        {
                            player2Damage /= 2;
                        }

                        break;
                    case "Normal":
                        if (player1Card.Element == "Water")
                        {
                            player2Damage *= 2;
                        }
                        else if (player1Card.Element == "Fire")
                        {
                            player2Damage /= 2;
                        }

                        break;
                }
            }

            (player1Damage, player2Damage) = CheckSpecialities(player1Card, player2Card, player1Damage, player2Damage);

            return (player1Damage, player2Damage);
        }

        enum Specialities
        {
            Goblin,
            Dragon,
            Wizzard,
            Ork,
            Knight,
            Kraken,
            FireElf,
        }

        private static (double, double) CheckSpecialities(Card player1Card, Card player2Card, double player1Damage,
            double player2Damage)
        {
            if (Enum.GetNames(typeof(Specialities)).Any(name => player1Card.Name.Contains(name)))
            {
                // player1Card name matches a Specialities enum value
                if (player1Card.Name.Contains("Dragon") && player2Card.Name.Contains("Goblin"))
                {
                    player2Damage = 0;
                }
                else if (player1Card.Name.Contains("Wizzard") && player2Card.Name.Contains("Ork"))
                {
                    player2Damage = 0;
                }
                else if (player1Card.Name.Contains("Knight") && player2Card.Name.Contains("WaterSpell"))
                {
                    player1Damage = 0;
                }
                else if (player1Card.Name.Contains("Kraken") && player2Card.Type.Equals("Spell"))
                {
                    player2Damage = 0;
                }
                else if (player1Card.Name.Contains("FireElf") && player2Card.Name.Contains("Dragon"))
                {
                    player2Damage = 0;
                }
            }
            else if (Enum.GetNames(typeof(Specialities)).Any(name => player2Card.Name.Contains(name)))
            {
                // player2Card name matches a Specialities enum value
                if (player2Card.Name.Contains("Dragon") && player1Card.Name.Contains("Goblin"))
                {
                    player1Damage = 0;
                }
                else if (player2Card.Name.Contains("Wizzard") && player1Card.Name.Contains("Ork"))
                {
                    player1Damage = 0;
                }
                else if (player2Card.Name.Contains("Knight") && player1Card.Name.Contains("WaterSpell"))
                {
                    player2Damage = 0;
                }
                else if (player2Card.Name.Contains("Kraken") && player1Card.Type.Equals("Spell"))
                {
                    player1Damage = 0;
                }
                else if (player2Card.Name.Contains("FireElf") && player1Card.Name.Contains("Dragon"))
                {
                    player1Damage = 0;
                }
            }

            return (player1Damage, player2Damage);
        }

        private static (string, string) CheckElement(Card playerCard)
        {
            bool isSpell = false;
            if (playerCard.ToString().Contains("Spell"))
            {
                // Card is a Spell
                // Check element type
                if (playerCard.ToString().Contains("Fire"))
                {
                    // Card is a Fire Spell
                    // Apply element type bonus
                    return ("Fire", "Spell");
                }
                else if (playerCard.ToString().Contains("Water"))
                {
                    // Card is a Water Spell
                    // Apply element type bonus
                    return ("Water", "Spell");
                }
                else
                {
                    // Card is a Spell but has no element type
                    return ("Normal", "Spell");
                }
            }
            else if (playerCard.ToString().Contains("Fire"))
            {
                // Card is not a spell
                return ("Fire", "Monster");
            }
            else if (playerCard.ToString().Contains("Water"))
            {
                // Card is not a spell with the element water
                return ("Water", "Monster");
            }
            else
            {
                //Card is a normal monster
                return ("Normal", "Monster");
            }
        }
    }
}

namespace CardGameSWEN
{
    public class Battle
    {
        private UserFunctions _userFunctions = new UserFunctions();
        private Database _database = new Database();
        public (int, string) GetStats(string token)
        {
            int userId =  _userFunctions.GetUserIdFromToken(token);
            Stats stats = new Stats();

            if (userId == 0)
            {
                return (401, "Access token is missing or invalid");
            }

            string wl_ratioString = null;

            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                string selectStatsQuery =
                    @"SELECT u.username, us.elo, us.wins, us.losses, us.wl_ratio FROM public.user u JOIN user_stats us ON u.id = us.user_id WHERE u.id = @userId";

                using (NpgsqlCommand command = new NpgsqlCommand(selectStatsQuery, con))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            stats.Name = reader.GetString(0);
                            stats.Elo = reader.GetInt32(1);
                            stats.Wins = reader.GetInt32(2);
                            stats.Losses = reader.GetInt32(3);
                            if (reader.IsDBNull(4))
                            {
                                stats.WL_Ratio = null;
                            }
                            else
                            {
                                stats.WL_Ratio = (reader.GetDouble(4).ToString() + "%");
                            }
                        }
                    }
                }
            }


            return (200, JsonConvert.SerializeObject(stats));
        }

        public (int, string) GetScoreboard(string token)
        {
            int userId = _userFunctions.GetUserIdFromToken(token);
            if (userId == 0)
            {
                return (401, "Access token is missing or invalid");
            }

            string wl_ratioString = null;
            List<Scoreboard> scoreboard = new List<Scoreboard>();
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                string selectScoreboardQuery =
                    @"SELECT public.user.username, user_stats.elo, wins, losses, wl_ratio FROM user_stats JOIN public.user ON public.user.id = user_stats.user_id ORDER BY user_stats.elo DESC";
                using (NpgsqlCommand command = new NpgsqlCommand(selectScoreboardQuery, con))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wl_ratioString = "0";
                            string username = reader.GetString(0);
                            int elo = reader.GetInt32(1);
                            int wins = reader.GetInt32(2);
                            int losses = reader.GetInt32(3);
                            if (reader.IsDBNull(4))
                            {
                                string wl_ratio = "0";
                            }
                            else
                            {
                                double wl_ratio = reader.GetDouble(4);
                                wl_ratioString = wl_ratio.ToString() + "%";
                            }

                            scoreboard.Add(new Scoreboard
                            {
                                Username = username, Elo = elo, Wins = wins, Losses = losses, WL_Ratio = wl_ratioString
                            });
                        }
                    }
                }

                con.Close();
            }

            return (200, JsonConvert.SerializeObject(scoreboard));
        }

        public void UpdateElo(string? battleResultWinner, string? loser)
        {
            using (NpgsqlConnection con = _database.GetConnection())
            {
                // Open the connection
                con.Open();

                // Retrieve user_id for winner
                int winnerId = 0;
                string selectUserIDQuery = @"SELECT id FROM public.user WHERE username = @winnerName";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUserIDQuery, con))
                {
                    command.Parameters.AddWithValue("@winnerName", battleResultWinner);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            winnerId = reader.GetInt32(0);
                        }
                    }
                }

                // Retrieve user_id for loser
                int loserId = 0;
                string selectUserIDQuery2 = @"SELECT id FROM public.user WHERE username = @loserName";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUserIDQuery2, con))
                {
                    command.Parameters.AddWithValue("@loserName", loser);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            loserId = reader.GetInt32(0);
                        }
                    }
                }

                // Update Elo and Wins for winner
                string updateEloWinsQuery =
                    @"UPDATE public.user_stats SET elo = elo + 3, wins = wins + 1 WHERE user_id = @winnerId";
                using (NpgsqlCommand command = new NpgsqlCommand(updateEloWinsQuery, con))
                {
                    command.Parameters.AddWithValue("@winnerId", winnerId);
                    command.ExecuteNonQuery();
                }

                // Update Coins for winner
                string updateCoinsWinsQuery =
                    @"UPDATE public.user SET coins = coins + 5 WHERE id = @winnerId";
                using (NpgsqlCommand command = new NpgsqlCommand(updateCoinsWinsQuery, con))
                {
                    command.Parameters.AddWithValue("@winnerId", winnerId);
                    command.ExecuteNonQuery();
                }


                // Update Elo and Losses for loser
                string updateEloLossesQuery =
                    @"UPDATE public.user_stats SET elo = elo - 5, losses = losses + 1 WHERE user_id = @loserId";
                using (NpgsqlCommand command = new NpgsqlCommand(updateEloLossesQuery, con))
                {
                    command.Parameters.AddWithValue("@loserId", loserId);
                    command.ExecuteNonQuery();
                }

                //get wins and losses for W/L ratio winner
                float winsWinner = 0;
                float lossesWinner = 0;
                string selectWinsLossesW = @"SELECT wins, losses FROM public.user_stats WHERE user_id = @winnerId";
                using (NpgsqlCommand command = new NpgsqlCommand(selectWinsLossesW, con))
                {
                    command.Parameters.AddWithValue("@winnerId", winnerId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            winsWinner = reader.GetInt32(0);
                            lossesWinner = reader.GetInt32(1);
                        }
                    }
                }

                //get wins and losses for W/L ratio loser
                float winsLoser = 0;
                float lossesLoser = 0;
                string selectWinsLossesL = @"SELECT wins, losses FROM public.user_stats WHERE user_id = @loserId";
                using (NpgsqlCommand command = new NpgsqlCommand(selectWinsLossesL, con))
                {
                    command.Parameters.AddWithValue("@loserId", loserId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            winsLoser = reader.GetInt32(0);
                            lossesLoser = reader.GetInt32(1);
                        }
                    }
                }

                if ((winsWinner == 0 && lossesWinner == 0) || (winsLoser == 0 && lossesLoser == 0))
                {
                    return;
                }

                double WLWinner = Math.Round(((winsWinner / (winsWinner + lossesWinner)) * 100));
                double WLLoser = Math.Round(((winsLoser / (winsLoser + lossesLoser)) * 100));

                //update w/r winner
                string updateWRWinnerQuery =
                    @"UPDATE public.user_stats SET wl_ratio = @wlWinner WHERE user_id = @winnerId";
                using (NpgsqlCommand command = new NpgsqlCommand(updateWRWinnerQuery, con))
                {
                    command.Parameters.AddWithValue("@winnerId", winnerId);
                    command.Parameters.AddWithValue("@wlWinner", WLWinner);
                    command.ExecuteNonQuery();
                }

                //update w/r loser
                string updateWRLoserQuery =
                    @"UPDATE public.user_stats SET wl_ratio = @wlLoser WHERE user_id = @loserId";
                using (NpgsqlCommand command = new NpgsqlCommand(updateWRLoserQuery, con))
                {
                    command.Parameters.AddWithValue("@loserId", loserId);
                    command.Parameters.AddWithValue("@wlLoser", WLLoser);
                    command.ExecuteNonQuery();
                }

                con.Close();
            }
        }
    }
}