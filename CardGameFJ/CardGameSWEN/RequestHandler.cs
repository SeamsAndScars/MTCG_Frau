using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using static CardGameSWEN.UserFunctions;
using static CardGameSWEN.Battle;
using static CardGameSWEN.CardFunctions;
using static CardGameSWEN.BattleMechanics;

namespace CardGameSWEN;

public class RequestHandler
{
    private UserFunctions _userFunctions = new UserFunctions();
    private CardFunctions _cardFunctions = new CardFunctions();
    private BattleMechanics _battleMechanics = new BattleMechanics();
    private Battle _battle = new Battle();


    internal void HandleRequest(object? client)
    {
        if (client is null)
        {
            throw new ArgumentNullException(nameof(client));
        }

        // Get the client's stream
        NetworkStream stream = ((TcpClient)client).GetStream();

        // Read the incoming data
        byte[] buffer = new byte[((TcpClient)client).ReceiveBufferSize];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string data_stream = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        string[] segments = data_stream.Split(" ");
        string firstSegment = segments[0];
        string method = "";
        if (firstSegment.Contains("curl"))
        {
            method = segments[1];
        }
        else
        {
            method = firstSegment;
        }

        // Handle the incoming data
        if (method == "GET")
        {
            HandleGet(data_stream, (TcpClient)client);
        }
        else if (method == "POST")
        {
            HandlePost(data_stream, (TcpClient)client);
        }
        else if (method == "PUT")
        {
            HandlePut(data_stream, (TcpClient)client);
        }
        else
        {
            // Return an error for unsupported methods
            string error = "Error: Unsupported method";
            byte[] errorBytes = Encoding.UTF8.GetBytes(error);
            stream.Write(errorBytes, 0, errorBytes.Length);
        }

        // Close the client's stream and the client
        stream.Close();
        ((TcpClient)client).Close();
    }

    private void HandleGet(string data_stream, TcpClient client)
    {
        var body = data_stream;
        // Read the request body
        //string body = new StreamReader(data_stream).ReadToEnd();

        // Extract the endpoint
        var lines = body.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        string endpoint = lines[0].Split(' ')[1].Trim('/');
        string endpointCopy = endpoint;

        // Extract the token after authorization (admin-mtcgToken)
        string token = lines.FirstOrDefault(x => x.StartsWith("Authorization: Basic"))?.Split(' ')[2] ?? "0";

        //check if endpoint contains /users -> user check
        string identifier = null;
        if (endpoint.Contains("users/"))
        {
            int indexOfSlash = endpoint.IndexOf('/');
            string endpointCut = endpoint.Substring(0, indexOfSlash); // "users"
            identifier = endpoint.Substring(indexOfSlash + 1); // "jakob (username)"
            endpoint = endpointCut + "/";
        }

        // Extract the JSON payload
        int payloadStartIndex = body.IndexOf("[");
        if (payloadStartIndex >= 0)
        {
            int payloadEndIndex = body.LastIndexOf("]") + 1;
            string jsonPayload = body.Substring(payloadStartIndex, payloadEndIndex - payloadStartIndex);
        }

        string textFormat = null;
        if (endpoint.Contains('?'))
        {
            string[] endpointValues = endpoint.Split('?');
            endpoint = endpointValues[0]; // this will contain "deck"
            textFormat = endpointValues[1].Substring(7); // this will contain 
        }


        bool token_expired = false;

        //For response
        object content = null;
        string description = null;
        int code = 0;
        string server = "CardGameSWEN";
        string contentType = null;


        //switch with endpoint
        switch (endpoint)
        {
            case "users/":
                if (identifier != null) (code, content) = _userFunctions.GetUserUsername(identifier, token);
                switch (code)
                {
                    case 200:
                        description = "Data successfully retrieved";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        break;
                    case 404:
                        description = "User not found.";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "cards":
                (code, content) = _userFunctions.GetUserCards(token);
                switch (code)
                {
                    case 200:
                        description = "The user has cards, the response contains these";
                        contentType = "application/json";
                        break;
                    case 204:
                        description = "The request was fine, but the user doesn't have any cards";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "deck":
                (code, content) = _cardFunctions.GetDeckRequest(token);
                switch (code)
                {
                    case 200:
                        description = "The deck has cards, the response contains these";
                        contentType = "application/json";
                        if (textFormat != null)
                        {
                            contentType = "text/plain";
                        }

                        break;
                    case 204:
                        description = "The request was fine, but the deck doesn't have any cards";
                        contentType = "application/json";
                        if (textFormat != null)
                        {
                            contentType = "text/plain";
                        }

                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        if (textFormat != null)
                        {
                            contentType = "text/plain";
                        }

                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "stats":
                (code, content) = _battle.GetStats(token);
                switch (code)
                {
                    case 200:
                        description = "The stats could be retrieved successfully.";
                        contentType = "application/json";

                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";

                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "score":
                (code, content) = _battle.GetScoreboard(token);
                switch (code)
                {
                    case 200:
                        description = "The stats could be retrieved successfully.";
                        contentType = "application/json";

                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";

                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            default:
                throw new InvalidDataException();
                break;
        }

        SendResponse(client, code, server, contentType, description, content);
    }


    private void HandlePost(string data_stream, TcpClient client)
    {
        var body = data_stream;
        string token = "";
        string jsonPayload = "";
        // Read the request body
        //string body = new StreamReader(data_stream).ReadToEnd();

        // Extract the endpoint
        var lines = body.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        string endpoint = lines[0].Split(' ')[1].Trim('/');

        // Extract the token after authorization (admin-mtcgToken)
        if (endpoint != "users" && endpoint != "sessions")
        {
            token = lines.First(x => x.StartsWith("Authorization: Basic")).Split(' ')[2];
        }

        if (endpoint != "users" && endpoint != "sessions" && body.Contains("["))
        {
            // Extract the JSON payload
            int payloadStartIndex = body.IndexOf("[");
            int payloadEndIndex = body.LastIndexOf("]") + 1;
            jsonPayload = body.Substring(payloadStartIndex, payloadEndIndex - payloadStartIndex);
        }
        else if (!body.Contains("[") && !body.Contains("{"))
        {
            jsonPayload = "empty";
        }
        else
        {
            // Extract the JSON payload
            int payloadStartIndex = body.IndexOf("{");
            int payloadEndIndex = body.LastIndexOf("}") + 1;
            jsonPayload = body.Substring(payloadStartIndex, payloadEndIndex - payloadStartIndex);
        }

        //jsonPayload.Trim('\n');

        // Parse the request body as JSON
        dynamic data;
        if (jsonPayload == "empty")
        {
            data = null;
        }
        else
        {
            data = JsonConvert.DeserializeObject(jsonPayload);
        }


        //For response
        object content = null;
        string description = null;
        int code = 0;
        string server = "CardGameSWEN";
        string contentType = null;


        //switch with endpoint
        switch (endpoint)
        {
            case "users":
                (code, content) = _userFunctions.AddUser((string)data.Username, (string)data.Password);
                switch (code)
                {
                    case 201:
                        description = "User successfully created";
                        contentType = "application/json";
                        break;
                    case 409:
                        description = "User with same username already registered";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "sessions":
                (code, content) = _userFunctions.Login((string)(data.Username), (string)data.Password);
                switch (code)
                {
                    case 200:
                        description = "User login successful";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Invalid username/password provided";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "packages":
                //(code, content) = AddCardPackage(data, token);
                (int, object) tuple = _cardFunctions.AddCardPackage(data, token);
                code = tuple.Item1;
                content = tuple.Item2;
                switch (code)
                {
                    case 201:
                        description = "Package and cards successfully created";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        break;
                    case 403:
                        description = "	Provided user is not admin";
                        contentType = "application/json";
                        break;
                    case 409:
                        description = "At least one card in the packages already exist";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "transactions/packages":
                (code, content) = _userFunctions.AddPackageToUser(token);
                switch (code)
                {
                    case 200:
                        description = "A package has been successfully bought";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        break;
                    case 403:
                        description = "Not enough money for buying a card package";
                        contentType = "application/json";
                        break;
                    case 404:
                        description = "No card package available for buying";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "battles":
                (code, content) = _battleMechanics.BattleString(token);
                switch (code)
                {
                    case 200:
                        description = "The battle has been carried out successfully";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        break;
                    case 202:
                        description = "Waiting for another player to join the lobby";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            default:
                throw new InvalidDataException();
                break;
        }

        SendResponse(client, code, server, contentType, description, content);
    }

    private void HandlePut(string data_stream, TcpClient client)
    {
        var body = data_stream;
        string jsonPayload;
        // Read the request body
        //string body = new StreamReader(data_stream).ReadToEnd();

        // Extract the endpoint
        string identifier = null;
        var lines = body.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        string endpoint = lines[0].Split(' ')[1].Trim('/');
        if (endpoint.Contains("users/"))
        {
            string[] endpointElements = endpoint.Split('/');
            endpoint = endpointElements[0] + "/";
            identifier = endpointElements[1];
        }

        // Extract the token after authorization (admin-mtcgToken)
        string token = lines.First(x => x.StartsWith("Authorization: Basic")).Split(' ')[2];


        if (endpoint != "users/" && endpoint != "sessions" && !body.Contains("[") || !body.Contains("{"))
        {
            // Extract the JSON payload
            int payloadStartIndex = body.IndexOf("[");
            int payloadEndIndex = body.LastIndexOf("]") + 1;
            jsonPayload = body.Substring(payloadStartIndex, payloadEndIndex - payloadStartIndex);
        }
        else if (!body.Contains("[") && !body.Contains("{"))
        {
            jsonPayload = "empty";
        }
        else
        {
            // Extract the JSON payload
            int payloadStartIndex = body.IndexOf("{");
            int payloadEndIndex = body.LastIndexOf("}") + 1;
            jsonPayload = body.Substring(payloadStartIndex, payloadEndIndex - payloadStartIndex);
        }


        // Parse the request body as JSON

        if (endpoint.Contains('?'))
        {
            string[] endpointValues = endpoint.Split('?');
            endpoint = endpointValues[0]; // this will contain "deck"
            Connection._TextFormat = endpointValues[1].Substring(7); // this will contain 
        }

        dynamic data = JsonConvert.DeserializeObject(jsonPayload);


        //For response
        object content = null;
        string description = null;
        int code = 0;
        string server = "CardGameSWEN";
        string contentType = null;


        //switch with endpoint
        switch (endpoint)
        {
            case "users/":
                (int, string) contentTMP = _userFunctions.UpdateUser(token, data, identifier);
                code = contentTMP.Item1;
                content = contentTMP.Item2;
                switch (code)
                {
                    case 200:
                        description = "User successfully created";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        break;
                    case 404:
                        description = "User not found.";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;
            case "deck":
                (int, string) contentTMP2 = _cardFunctions.AddCardsToDeck(data, token);
                code = contentTMP2.Item1;
                content = contentTMP2.Item2;
                switch (code)
                {
                    case 200:
                        description = "The deck has been successfully configured";
                        contentType = "application/json";
                        break;
                    case 400:
                        description = "The provided deck did not include the required amount of cards";
                        contentType = "application/json";
                        break;
                    case 401:
                        description = "Access token is missing or invalid";
                        contentType = "application/json";
                        break;
                    case 403:
                        description =
                            "At least one of the provided cards does not belong to the user or is not available.";
                        contentType = "application/json";
                        break;
                    default:
                        throw new ApplicationException();
                }

                break;

            default:
                throw new InvalidDataException();
                break;
        }

        SendResponse(client, code, server, contentType, description, content);
    }

    private static void SendResponse(TcpClient client, int code, string server, string contentType, string description,
        object content)
    {
        // Get the client's stream
        NetworkStream stream = client.GetStream();

        // Create the response message
        string response =
            $"HTTP/1.1 {code} {description}\r\nServer: {server}\r\nContent-Type: {contentType}\r\nContent-Length: {content.ToString().Length}\r\n\r\n{content.ToString()}";

        // Convert the response message to bytes
        byte[] responseBytes = Encoding.ASCII.GetBytes(response);

        // Send the response
        stream.Write(responseBytes, 0, responseBytes.Length);
    }
}