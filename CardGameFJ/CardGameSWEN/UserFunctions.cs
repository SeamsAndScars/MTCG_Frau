using System.Data;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Npgsql;

namespace CardGameSWEN
{
    public class UserFunctions
    {
        private Database _database = new Database();

        public bool CheckUser(string username, string password)
        {
            string hashedPW = EnryptPassword(password);
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                const string selectUserQuery =
                    @"SELECT COUNT(*) from public.user where username = @username and password = @password";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUserQuery, con))
                {
                    command.Parameters.AddWithValue("@username", username);
                    command.Parameters.AddWithValue("@password", hashedPW);
                    var count = (long)command.ExecuteScalar();
                    if (count > 0)
                    {
                        // user exists
                        con.Close();
                        return true;
                    }
                    else
                    {
                        // user does not exist
                        con.Close();
                        return false;
                    }
                }
            }
        }

        public (int, string) Login(string username, string password)
        {
            var exists = false;

            // Validate the username and password
            exists = CheckUser(username, password);

            if (exists == false)
            {
                //return if username or password is incorrect
                return (401, "Invalid username/password provided");
            }

            //set logged in user
            //Connection.logged_in_User = username;

            //check if user needs a new token
            var viable = CheckToken(username);

            //generate tokens
            if (viable == false)
            {
                var token = GetUserToken(username);

                //expiry date for tokens, to know if they are still viable
                var now = DateTime.Now;
                var expDate = now.Add(Connection.expirationDate);

                //insert token into database, along with username and expiry date
                using (NpgsqlConnection con = _database.GetConnection())
                {
                    con.Open();
                    const string insertTokenQuery =
                        @"INSERT INTO created_tokens (username, expiry_date, token) VALUES (@username, @expiry_date, @token)";
                    using (NpgsqlCommand command = new NpgsqlCommand(insertTokenQuery, con))
                    {
                        command.Parameters.AddWithValue("@username", username);
                        command.Parameters.AddWithValue("@expiry_date", expDate);
                        command.Parameters.AddWithValue("@token", token);
                        command.ExecuteNonQuery();
                    }

                    con.Close();
                }
            }

            return (200, "User login successful");
        }

        private bool CheckToken(string username)
        {
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                const string selectTokenQuery = @"SELECT expiry_date FROM created_tokens WHERE username = @username";
                using (NpgsqlCommand command = new NpgsqlCommand(selectTokenQuery, con))
                {
                    command.Parameters.AddWithValue("@username", username);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var expiryDate = reader.GetDateTime(0);
                            if (expiryDate > DateTime.Now)
                            {
                                //Token is expired
                                return true;
                            }
                        }
                    }
                }
            }

            //token is still viable
            return false;
        }

        //Tokens for special users
        public string GetUserToken(string username) => username switch
        {
            "kienboec" => "kienboec-mtcgToken",
            "altenhof" => "altenhof-mtcgToken",
            "admin" => "admin-mtcgToken",
            _ => GenerateToken()
        };


        public virtual string GenerateToken()
        {
            // Use a secure random number generator to generate a random token
            var rng = RandomNumberGenerator.Create();
            var tokenData = new byte[32];
            rng.GetBytes(tokenData);

            // Return the token as a hexadecimal string
            return BitConverter.ToString(tokenData).Replace("-", "").ToLower();
        }

        public (int, string) AddUser(string username, string password)
        {
            CurrUser currUser = new CurrUser();
            string hashedPW = EnryptPassword(password);
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                string checkUserQuery = @"SELECT COUNT(*) from public.user where username = @username";
                using (NpgsqlCommand command = new NpgsqlCommand(checkUserQuery, con))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var count = (long)command.ExecuteScalar();
                    if (count > 0)
                    {
                        return (409, "User with same username already registered");
                    }
                    else
                    {
                        string insertUserQuery =
                            @"INSERT INTO public.user (username, password, coins) VALUES (@username, @password, @coins)";
                        using (NpgsqlCommand insertCommand = new NpgsqlCommand(insertUserQuery, con))
                        {
                            insertCommand.Parameters.AddWithValue("@username", username);
                            insertCommand.Parameters.AddWithValue("@password", hashedPW);
                            insertCommand.Parameters.AddWithValue("@coins", 20);
                            insertCommand.ExecuteNonQuery();
                        }


                        var userId = GetUserId(username);

                        string insertUserStatsQuery =
                            @"INSERT INTO user_stats (user_id, elo, wins, losses) VALUES (@user_id, @elo, @wins, @losses)";
                        using (NpgsqlCommand insertCommand = new NpgsqlCommand(insertUserStatsQuery, con))
                        {
                            insertCommand.Parameters.AddWithValue("@user_id", userId);
                            insertCommand.Parameters.AddWithValue("@elo", 100);
                            insertCommand.Parameters.AddWithValue("@wins", 0);
                            insertCommand.Parameters.AddWithValue("@losses", 0);
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }

                con.Close();
            }

            currUser.Username = username;
            currUser.Password = hashedPW;
            return (201, JsonConvert.SerializeObject(currUser));
        }

        public int GetUserId(string username)
        {
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                const string selectUserId = @"SELECT id FROM public.user WHERE username = @username";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUserId, con))
                {
                    command.Parameters.AddWithValue("@username", username);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetInt32(0);
                        }
                    }
                }
            }

            return 0;
        }

        public static string EnryptPassword(string password)
        {
            //create bythe array
            var tmpSource = ASCIIEncoding.ASCII.GetBytes(password);
            //create hash
            var tmpHash = new MD5CryptoServiceProvider().ComputeHash(tmpSource);

            int i;
            StringBuilder sOutput = new StringBuilder(tmpHash.Length);
            for (i = 0; i < tmpHash.Length; i++)
            {
                sOutput.Append(tmpHash[i].ToString("X2"));
            }

            return sOutput.ToString();
        }

        public bool CheckTokenPermission(string token)
        {
            if (token == "admin-mtcgToken")
            {
                using (NpgsqlConnection con = _database.GetConnection())
                {
                    con.Open();
                    const string selectTokenQuery = @"SELECT token FROM created_tokens WHERE token = @token";
                    using (NpgsqlCommand command = new NpgsqlCommand(selectTokenQuery, con))
                    {
                        command.Parameters.AddWithValue("@token", token);
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                //token exists in the database
                                return true;
                            }
                        }
                    }
                }

                //token not found in the database
                return false;
            }

            return false;
        }

        public int GetUserIdFromToken(string token)
        {
            int userId = 0;
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                // First, retrieve the username associated with the token
                string selectUsernameQuery = @"SELECT username FROM created_tokens WHERE token = @token";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUsernameQuery, con))
                {
                    command.Parameters.AddWithValue("@token", token);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string username = reader.GetString(0);
                            reader.Close();
                            // Next, use the retrieved username to query the user table to get the user_id
                            string selectUserIdQuery = @"SELECT id FROM public.user WHERE username = @username";
                            using (NpgsqlCommand innerCommand = new NpgsqlCommand(selectUserIdQuery, con))
                            {
                                innerCommand.Parameters.AddWithValue("@username", username);
                                using (NpgsqlDataReader innerReader = innerCommand.ExecuteReader())
                                {
                                    if (innerReader.Read())
                                    {
                                        userId = innerReader.GetInt32("id");
                                    }
                                }
                            }
                        }
                    }
                }

                con.Close();
            }

            return userId;
        }

        public User GetUser(string token)
        {
            int userId = GetUserIdFromToken(token);
            User user = new User();
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                string selectUserQuery =
                    @"SELECT id, username FROM public.user WHERE id = @id";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUserQuery, con))
                {
                    command.Parameters.AddWithValue("@id", userId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user.Id = reader.GetInt32(0);
                            user.Username = reader.GetString(1);
                        }
                    }
                }

                con.Close();
            }

            return user;
        }

        public (int, string) GetUserUsername(string username, string token)
        {
            //if the username is not the same as saved in the token and not admin
            if (!CheckTokenPermission(token) && username != GetUser(token).Username)
            {
                return (401, "Access token is missing or invalid");
            }

            int userId = GetUserIdFromToken(token);
            ReturnUser user = new ReturnUser();
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                string selectUserQuery =
                    @"SELECT username, bio, image FROM public.user WHERE id = @id";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUserQuery, con))
                {
                    command.Parameters.AddWithValue("@id", userId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return (404, "User not found");
                        }

                        user.Name = reader.GetString(0);
                        if (reader.IsDBNull(1))
                        {
                            user.Bio = null;
                        }
                        else
                        {
                            user.Bio = reader.GetString(1);
                        }

                        if (reader.IsDBNull(2))
                        {
                            user.Image = null;
                        }
                        else
                        {
                            user.Image = reader.GetString(2);
                        }
                    }
                }

                con.Close();
            }

            return (200, JsonConvert.SerializeObject(user));
        }

        public (int, string) UpdateUser(string token, dynamic updates, string username)
        {
            // Get the user's id from the token
            int userId = GetUserIdFromToken(token);
            var currUser = GetUser(token);

            if (userId == 0 || currUser.Username != username)
            {
                return (401, "Access token is missing or invalid");
            }

            User user = new User();

            user.Name = updates.Name;
            user.Bio = updates.Bio;
            user.Image = updates.Image;

            // Open a connection to the database
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();

                //check if user exist
                bool userExists = ExistUser(userId);
                if (!userExists)
                {
                    return (404, "User not found");
                }

                // Create a new command to update the user's information
                using (NpgsqlCommand command = new NpgsqlCommand())
                {
                    command.Connection = con;
                    command.Parameters.AddWithValue("@id", userId);
                    command.CommandText =
                        "UPDATE public.user SET name = @name, bio = @bio, image = @image WHERE id = @id";

                    // Add the parameters and their values to the command

                    command.Parameters.AddWithValue("@name", user.Name);
                    command.Parameters.AddWithValue("@bio", user.Bio);
                    command.Parameters.AddWithValue("@image", user.Image);

                    // Execute the command
                    command.ExecuteNonQuery();
                }

                // Close the connection
                con.Close();
            }

            // return the updated user information
            return (200, "User successfully updated");
        }

        public bool ExistUser(int userId)
        {
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();
                string selectUserQuery =
                    @"SELECT username FROM public.user WHERE id = @id";
                using (NpgsqlCommand command = new NpgsqlCommand(selectUserQuery, con))
                {
                    command.Parameters.AddWithValue("@id", userId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return false;
                        }

                        //user exists
                        return true;
                    }
                }
            }
        }

        private enum PackageName
        {
            BattlesOfLegend,
            InvasionOfChaos,
            TheLostMillennium,
            GhostsFromThePast,
            MetalRaiders
        }

        public (int, string) AddPackageToUser(string token)
        {
            bool userHasCoins = false;
            int userId = GetUserIdFromToken(token);
            if (userId == 0)
            {
                return (401, "Access token is missing or invalid");
            }

            int packageId = 0;
            bool isPackageValid = false;

            //check if user has coins 
            userHasCoins = CheckUserCoins(userId);

            if (userHasCoins)
            {
                while (packageId == 0 && !isPackageValid)
                {
                    List<int> SmallestId = new List<int>();
                    using (NpgsqlConnection con = _database.GetConnection())
                    {
                        con.Open();

                        string selectPackageQuery = @"SELECT id FROM packages WHERE valid = true";
                        using (NpgsqlCommand command = new NpgsqlCommand(selectPackageQuery, con))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (!reader.Read())
                                {
                                    return (404, "No card package available for buying");
                                }

                                SmallestId.Add(reader.GetInt32(0));
                            }
                        }

                        packageId = SmallestId.Min();

                        // check if package is valid
                        //isPackageValid = IsPackageValid(packageId);

                        // insert the cards from the package into the user_cards table
                        string insertUserCardsQuery =
                            @"INSERT INTO user_cards (user_id, card_id) SELECT @userId, id FROM card WHERE package_id = @packageId";
                        using (NpgsqlCommand command = new NpgsqlCommand(insertUserCardsQuery, con))
                        {
                            command.Parameters.AddWithValue("@userId", userId);
                            command.Parameters.AddWithValue("@packageId", packageId);
                            command.ExecuteNonQuery();
                        }

                        con.Close();
                    }
                }


                using (NpgsqlConnection con = _database.GetConnection())
                {
                    con.Open();
                    string updatePackageQuery =
                        @"UPDATE packages SET user_id = @user_id, valid = false WHERE id = @package_id";
                    using (NpgsqlCommand command = new NpgsqlCommand(updatePackageQuery, con))
                    {
                        command.Parameters.AddWithValue("@user_id", userId);
                        command.Parameters.AddWithValue("@package_id", packageId);
                        command.ExecuteNonQuery();
                    }

                    con.Close();
                }


                using (NpgsqlConnection con = _database.GetConnection())
                {
                    con.Open();
                    string updateCoinsQuery =
                        @"UPDATE public.user SET coins = coins - 5 WHERE id = @user_id";
                    using (NpgsqlCommand command = new NpgsqlCommand(updateCoinsQuery, con))
                    {
                        command.Parameters.AddWithValue("@user_id", userId);
                        command.ExecuteNonQuery();
                    }

                    con.Close();
                }

                //return added package 

                return (200, JsonConvert.SerializeObject(GetPackage(packageId)));
            }

            return (403, "Not enough money for buying a card package");
        }

        private object? GetPackage(int packageId)
        {
            List<ReturnCard> cards = new List<ReturnCard>();
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();

                string selectPackageQuery = @"SELECT id, name, damage FROM public.card WHERE package_id = @package_id";
                using (NpgsqlCommand command = new NpgsqlCommand(selectPackageQuery, con))
                {
                    command.Parameters.AddWithValue("@package_id", packageId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ReturnCard card = new ReturnCard();
                            {
                                card.Id = reader.GetString(0);
                                card.Name = reader.GetString(1);
                                card.Damage = reader.GetDouble(2);
                            }
                            cards.Add(card);
                        }
                    }
                }
            }

            return cards;
        }

        public bool CheckUserCoins(int userId)
        {
            int coins = 0;
            //checks if user has enough coins to buy a package
            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();

                string selectPackageQuery = @"SELECT coins FROM public.user WHERE id = @userId";
                using (NpgsqlCommand command = new NpgsqlCommand(selectPackageQuery, con))
                {
                    command.Parameters.AddWithValue("@userId", userId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            coins = reader.GetInt32(0);
                        }
                    }
                }

                con.Close();
            }

            if (coins >= 5)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public (int, string) GetUserCards(string token)
        {
            var userId = GetUserIdFromToken(token);

            if (userId == 0)
            {
                return (401, "Access token is missing or invalid");
            }

            var cards = new List<ReturnCard>();

            using (NpgsqlConnection con = _database.GetConnection())
            {
                con.Open();

                string selectCardsQuery =
                    @"SELECT c.id, c.name, c.damage FROM user_cards uc JOIN card c ON uc.card_id = c.id WHERE uc.user_id = @user_id";
                using (NpgsqlCommand command = new NpgsqlCommand(selectCardsQuery, con))
                {
                    command.Parameters.AddWithValue("@user_id", userId);
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var card = new ReturnCard()
                            {
                                Id = reader.GetString(0),
                                Name = reader.GetString(1),
                                Damage = reader.GetDouble(2)
                            };
                            cards.Add(card);
                        }
                    }
                }

                con.Close();
            }

            //check if empty
            if (cards.Count == 0)
            {
                return (204, "The request was fine, but the user doesn't have any cards");
            }


            return (200, JsonConvert.SerializeObject(cards));
        }
    }
}