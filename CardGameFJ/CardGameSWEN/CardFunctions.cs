using System.ComponentModel;
using Newtonsoft.Json;
using Npgsql;
using static CardGameSWEN.UserFunctions;
using static CardGameSWEN.Database;

namespace CardGameSWEN;

public class CardFunctions
{
    private new Database _database = new Database();
    private UserFunctions _userFunctions = new UserFunctions();

    public List<Card> GetDeck(string token)
    {
        // Get the user's id from the token
        int userId = _userFunctions.GetUserIdFromToken(token);

        //check if token exist
        if (userId == 0)
        {
            throw new AccessViolationException();
        }

        var cards = new List<Card>();
        //get deck with userid
        using (NpgsqlConnection con = _database.GetConnection())
        {
            con.Open();
            string selectCardsInDeckQuery =
                @"SELECT c.id, c.name, c.damage FROM decks d JOIN card c ON d.card1_id = c.id OR d.card2_id = c.id OR d.card3_id = c.id OR d.card4_id = c.id WHERE d.user_id = @user_id";
            using (NpgsqlCommand command = new NpgsqlCommand(selectCardsInDeckQuery, con))
            {
                command.Parameters.AddWithValue("@user_id", userId);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var cardId = reader.GetString(0);
                        var cardName = reader.GetString(1);
                        var cardDamage = reader.GetDouble(2);
                        cards.Add(new Card() { Id = cardId, Name = cardName, Damage = cardDamage });
                    }
                }
            }
        }

        //return deck
        return cards;
    }

    public (int, string) GetDeckRequest(string token)
    {
        // Get the user's id from the token
        int userId = _userFunctions.GetUserIdFromToken(token);

        //check if token exist
        if (userId == 0)
        {
            return (401, "Access token is missing or invalid");
        }

        var cards = new List<ReturnCard>();
        using (NpgsqlConnection con = _database.GetConnection())
        {
            con.Open();
            string selectCardsInDeckQuery =
                @"SELECT c.id, c.name, c.damage FROM decks d JOIN card c ON d.card1_id = c.id OR d.card2_id = c.id OR d.card3_id = c.id OR d.card4_id = c.id WHERE d.user_id = @user_id";
            using (NpgsqlCommand command = new NpgsqlCommand(selectCardsInDeckQuery, con))
            {
                command.Parameters.AddWithValue("@user_id", userId);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var cardId = reader.GetString(0);
                        var cardName = reader.GetString(1);
                        var cardDamage = reader.GetDouble(2);
                        cards.Add(new ReturnCard() { Id = cardId, Name = cardName, Damage = cardDamage });
                    }
                }
            }
        }

        //check if cards in deck
        if (cards.Count == 0)
        {
            return (204, "The request was fine, but the deck doesn't have any cards");
        }

        return (200, JsonConvert.SerializeObject(cards));
    }

    public (int, string) AddCardsToDeck(dynamic data, string token)
    {
        // Get the user's id from the token
        int userId = _userFunctions.GetUserIdFromToken(token);
        if (userId == 0)
        {
            return (401, "Access token is missing or invalid");
        }

        var cardIds = new List<string>();

        foreach (var item in data)
        {
            cardIds.Add(item.ToString());
        }

        //check if there are less then 4 cards in the deck provided
        if (cardIds.Count < 4)
        {
            return (400, "The provided deck did not include the required amount of cards");
        }

        var currUserCards = GetDeck(token);
        var currUserCardIdString = new List<string>();


        foreach (var currUserCardId in currUserCards)
        {
            currUserCardIdString.Add(currUserCardId.Id);
        }

        //check if the same cards are already in the deck
        if (cardIds.Equals(currUserCardIdString))
        {
            return (403, "At least one of the provided cards does not belong to the user or is not available.");
        }

        bool cardsAreValid = true;
        using (NpgsqlConnection con = _database.GetConnection())
        {
            con.Open();
            string checkCardOwnershipQuery = @"SELECT user_id FROM user_cards WHERE card_id = @card_id";
            using (NpgsqlCommand command = new NpgsqlCommand(checkCardOwnershipQuery, con))
            {
                command.Parameters.AddWithValue("@card_id", cardIds[0]);
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int ownerId = reader.GetInt32(0);
                        if (ownerId != userId)
                        {
                            cardsAreValid = false;
                        }
                    }
                }
            }
        }

        if (!cardsAreValid)
        {
            // return error message or throw exception
            return (403, "At least one of the provided cards does not belong to the user or is not available.");
        }

        //check if a deck already exists, if not INSERT
        if (currUserCards.Count == 0)
        {
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                const string insertDeckQuery =
                    @"INSERT INTO decks (user_id, card1_id, card2_id, card3_id, card4_id) VALUES (@user_id, @card1, @card2, @card3, @card4)";
                using (NpgsqlCommand command = new NpgsqlCommand(insertDeckQuery, con))
                {
                    command.Parameters.AddWithValue("@user_id", userId);
                    command.Parameters.AddWithValue("@card1", cardIds[0]);
                    command.Parameters.AddWithValue("@card2", cardIds[1]);
                    command.Parameters.AddWithValue("@card3", cardIds[2]);
                    command.Parameters.AddWithValue("@card4", cardIds[3]);
                    command.ExecuteNonQuery();
                }

                con.Close();
            }
        }
        else
        {
            // Use the user's id and the list of card ids to update the deck table
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                const string updateDeckQuery =
                    @"UPDATE decks SET card1_id = @card1, card2_id = @card2, card3_id = @card3, card4_id = @card4 WHERE user_id = @user_id";
                using (NpgsqlCommand command = new NpgsqlCommand(updateDeckQuery, con))
                {
                    command.Parameters.AddWithValue("@user_id", userId);
                    command.Parameters.AddWithValue("@card1", cardIds[0]);
                    command.Parameters.AddWithValue("@card2", cardIds[1]);
                    command.Parameters.AddWithValue("@card3", cardIds[2]);
                    command.Parameters.AddWithValue("@card4", cardIds[3]);
                    command.ExecuteNonQuery();
                }

                con.Close();
            }
        }

        var currUserCards2 = GetDeck(token);
        var currUserCardIdString2 = new List<string>();


        foreach (var currUserCardId2 in currUserCards2)
        {
            currUserCardIdString2.Add(currUserCardId2.Id);
        }

        return (200, JsonConvert.SerializeObject(currUserCardIdString2));
    }

    private enum PackageName
    {
        BattlesOfLegend,
        InvasionOfChaos,
        TheLostMillennium,
        GhostsFromThePast,
        MetalRaiders
    }

    public (int, object) AddCardPackage(dynamic data, string token)
    {
        var userID = _userFunctions.GetUserIdFromToken(token);
        if (userID == 0)
        {
            return (401, "Access token is missing or invalid");
        }

        string dataString = data.ToString();
        if (_userFunctions.CheckTokenPermission(token))
        {
            var cards = new List<ReturnCard>();

            foreach (var cardData in data)
            {
                var card = new ReturnCard()
                {
                    Id = (string)cardData.Id,
                    Name = (string)cardData.Name,
                    Damage = (double)cardData.Damage,
                };
                cards.Add(card);
                if (CardExist(card))
                {
                    return (409, "At least one card in the packages already exists");
                }
            }

            int packageId = 0;
            string[] packageNames = Enum.GetNames(typeof(PackageName));
            Random rand = new Random();
            int index = rand.Next(packageNames.Length);
            string packageName = packageNames[index];

            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                string insertPackageQuery = @"INSERT INTO packages (name) VALUES (@name) RETURNING id";
                using (NpgsqlCommand command = new NpgsqlCommand(insertPackageQuery, con))
                {
                    command.Parameters.AddWithValue("@name", packageName);
                    packageId = (int)command.ExecuteScalar();
                }

                con.Close();
            }


            var cardsTMP = new List<Card>();
            foreach (var cardDataTMP in data)
            {
                var cardTMP = new Card()
                {
                    Id = (string)cardDataTMP.Id,
                    Name = (string)cardDataTMP.Name,
                    Damage = (double)cardDataTMP.Damage,
                    package_id = packageId
                };
                using (NpgsqlConnection con = _database.GetConnection())
                {
                    con.Open();


                    cardsTMP.Add(cardTMP);

                    string insertCardQuery =
                        @"INSERT INTO public.card (id, name, damage, package_id) VALUES (@id, @name, @damage, @package_id)";
                    using (NpgsqlCommand command = new NpgsqlCommand(insertCardQuery, con))
                    {
                        command.Parameters.AddWithValue("@id", cardTMP.Id);
                        command.Parameters.AddWithValue("@name", cardTMP.Name);
                        command.Parameters.AddWithValue("@damage", cardTMP.Damage);
                        command.Parameters.AddWithValue("@package_id", cardTMP.package_id);
                        command.ExecuteNonQuery();
                    }
                }
            }

            return (201, JsonConvert.SerializeObject(cards));
        }
        else
        {
            return (403, "Provided user is not admin");
        }
    }

    public bool CardExist(ReturnCard card)
    {
        var cardId = card.Id;
        using (NpgsqlConnection con = _database.GetConnection())
        {
            con.Open();
            string selectCardQuery = @"SELECT COUNT(*) FROM card WHERE id = @id";
            using (NpgsqlCommand command = new NpgsqlCommand(selectCardQuery, con))
            {
                command.Parameters.AddWithValue("@id", cardId);
                long count = (long)command.ExecuteScalar();
                if (count > 0)
                {
                    return true;
                }
            }

            con.Close();
        }

        return false;
    }
}