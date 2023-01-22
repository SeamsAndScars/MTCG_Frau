using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using static CardGameSWEN.UserFunctions;
using static CardGameSWEN.CardFunctions;
using Moq;
using Newtonsoft.Json;
using Npgsql;

namespace CardGameSWEN.Tests;

public class Tests
{
    private UserFunctions _userFunctions = new UserFunctions();
    private CardFunctions _cardFunctions = new CardFunctions();


    [Test]
    [TestCase("admin", "admin-mtcgToken")]
    [TestCase("kienboec", "kienboec-mtcgToken")]
    [TestCase("altenhof", "altenhof-mtcgToken")]
    [TestCase("Jakob", "Katze123")]
    public void GetUserTokenTest(string username, string expected)
    {
        //Arrange 
        //callbase = true, 
        var userFunctionsMock = new Mock<UserFunctions>() { CallBase = true };
        userFunctionsMock
            .Setup(_ => _.GenerateToken())
            .Returns("Katze123");
        //Act
        var actual = userFunctionsMock.Object.GetUserToken(username);
        //Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void AddUserTestReturns201()
    {
        // Arrange
        string testUsername = "testuser";
        string testPassword = "testpassword";

        // Create a mock object of the UserFunctions class
        var userFunctionsMock = new Mock<UserFunctions>() { CallBase = true };

        // Act
        var (statusCode, content) = userFunctionsMock.Object.AddUser(testUsername, testPassword);

        // Assert
        Assert.AreEqual(201, statusCode);
        Assert.IsNotNull(content);

        var user = JsonConvert.DeserializeObject<CurrUser>(content);
        Assert.AreEqual(testUsername, user.Username);
        Assert.AreEqual(EnryptPassword(testPassword), user.Password);
    }

    [Test]
    public void AddUserTestReturns409()
    {
        // Arrange
        string testUsername = "kienboec";
        string testPassword = "daniel";

        // Create a mock object of the UserFunctions class
        var userFunctionsMock = new Mock<UserFunctions>() { CallBase = true };

        // Act
        var (statusCode, content) = userFunctionsMock.Object.AddUser(testUsername, testPassword);

        // Assert
        Assert.AreEqual(409, statusCode);
        Assert.IsNotNull(content);


        Assert.AreEqual(content, "User with same username already registered");
    }

    [Test]
    public void EnryptPasswordTest()
    {
        // Arrange
        string testPassword = "testpassword";
        string expectedEncryptedPassword =
            "E16B2AB8D12314BF4EFBD6203906EA6C"; // this is the expected output when encrypting the test password

        // Act
        string actualEncryptedPassword = EnryptPassword(testPassword);

        // Assert
        Assert.AreEqual(expectedEncryptedPassword, actualEncryptedPassword);
    }

    [Test]
    [TestCase("admin-mtcgToken", true)]
    [TestCase("invalid-token", false)]
    [TestCase("kieboec-mtcgToken", false)]
    public void CheckTokenPermissionTest(string token, bool expected)
    {
        // Arrange (if needed)
        // ...

        // Act
        var actual = _userFunctions.CheckTokenPermission(token);
        var databaseMock = new Mock<Database>();
        // When the GetConnection() method is called, return a new NpgsqlConnection object

        // Assert
        Assert.AreEqual(expected, actual);
    }


    [Test]
    public void GetDeckTest()
    {
        // Arrange
        string testToken = "kienboec-mtcgToken";
        var expectedCards = new List<Card>
        {
            new Card { Id = "845f0dc7-37d0-426e-994e-43fc3ac83c08", Name = "WaterGoblin", Damage = 10.0 },
            new Card { Id = "99f8f8dc-e25e-4a95-aa2c-782823f36e2a", Name = "Dragon", Damage = 50.0 },
            new Card { Id = "e85e3976-7c86-4d06-9a80-641c2019a79f", Name = "WaterSpell", Damage = 20.0 },
            new Card { Id = "171f6076-4eb5-4a7d-b3f2-2d650cc3d237", Name = "RegularSpell", Damage = 28.0 },
        };

        // Act
        var actualCards = _cardFunctions.GetDeck(testToken);

        // Assert
        CollectionAssert.AreEqual(JsonConvert.SerializeObject(expectedCards), JsonConvert.SerializeObject(actualCards));
    }


    [Test]
    public void CardExistsTest()
    {
        // Arrange
        string testToken = "kienboec-mtcgToken";
        var expectedCards = new List<Card>
        {
            new Card { Id = "845f0dc7-37d0-426e-994e-43fc3ac83c08", Name = "WaterGoblin", Damage = 10.0 },
            new Card { Id = "99f8f8dc-e25e-4a95-aa2c-782823f36e2a", Name = "Dragon", Damage = 50.0 },
            new Card { Id = "e85e3976-7c86-4d06-9a80-641c2019a79f", Name = "WaterSpell", Damage = 20.0 },
            new Card { Id = "171f6076-4eb5-4a7d-b3f2-2d650cc3d237", Name = "RegularSpell", Damage = 28.0 },
        };

        // Act
        var actualCards = _cardFunctions.GetDeck(testToken);

        // Assert
        CollectionAssert.AreEqual(JsonConvert.SerializeObject(expectedCards), JsonConvert.SerializeObject(actualCards));
    }


    [Test]
    [TestCase("kienboec", "daniel", 200, "User login successful")]
    [TestCase("testuser", "incorrectpassword", 401, "Invalid username/password provided")]
    [TestCase("nonexistentuser", "testpassword", 401, "Invalid username/password provided")]
    public void LoginTest(string username, string password, int expectedStatusCode,
        string expectedContent)
    {
        // Arrange
        UserFunctions userFunctions = new UserFunctions();

        // Act
        var (statusCode, content) = userFunctions.Login(username, password);

        // Assert
        Assert.AreEqual(expectedStatusCode, statusCode);
        Assert.AreEqual(expectedContent, content);
    }

    [Test]
    [TestCase("kienboec", "daniel", true)]
    [TestCase("invaliduser", "invalidpassword", false)]
    public void CheckUserExistsTest(string username, string password, bool expected)
    {
        // Act
        var actual = _userFunctions.CheckUser(username, password);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void ExistUser_ValidInput()
    {
        // Arrange
        int testUserId = _userFunctions.GetUserId("kienboec");
        UserFunctions userFunctions = new UserFunctions();

        // Act
        bool result = _userFunctions.ExistUser(testUserId);

        // Assert
        Assert.IsTrue(result);
    }

    [Test]
    public void ExistUser_InvalidInput()
    {
        // Arrange
        int testUserId = -1;
        UserFunctions userFunctions = new UserFunctions();

        // Act
        bool result = userFunctions.ExistUser(testUserId);

        // Assert
        Assert.IsFalse(result);
    }

    [Test]
    public void CheckUserCoins_NotEnoughCoins()
    {
        // Arrange
        int userId = _userFunctions.GetUserId("kienboec");
        int enoughCoins = 5;

        // Act
        bool result = _userFunctions.CheckUserCoins(userId);

        // Assert
        Assert.IsFalse(result);
    }
}