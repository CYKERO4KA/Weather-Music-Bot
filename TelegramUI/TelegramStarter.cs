using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Configuration;
using System.Text.Json.Serialization;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Data.SqlTypes;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace TelegramUI;

class TelegramStarter
{
    private static double _latitude { get; set; }
    private static double _longitude { get; set; }
    private static string _player { get; set; }
    private static string _step { get; set; }
    private static readonly HttpClient HttpClient = new HttpClient();
    private static SqlConnection connection;

    public static void Start()
    {
        TelegramConstant constant = new TelegramConstant();
        Console.OutputEncoding = Encoding.Unicode;
        var client = new TelegramBotClient(constant.connectionString);
        client.StartReceiving(Update, Error);
        
        Console.ReadLine();
    }

    static async Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
    {

        var message = update.Message;
        

        if (message?.Type == MessageType.Text && message.Text != null)
        {
            Console.WriteLine($"[{message.Chat.Username} ({message.Chat.FirstName}) - {message.Text}]");
            if (message.Text!.ToLower().Contains("/start"))
            {
                await Authorization(botClient, message, token);
            }
            else if (message.Text!.ToLower().Contains("authentication"))
            {
                await SendSqlRequest(message);
                await SendMusicOptionRequest(botClient, message, token);
                await GetLocationSql(message);
            }
            else if (message.Text!.ToLower().Contains("spotify"))
            {
                _player = message.Text;
                await SendSearchMethod(botClient, message, token);
            }
            else if (message.Text!.ToLower().Contains("youtube"))
            {
                _player = message.Text;
                await SendSearchMethod(botClient, message, token);
            }
            else if (message.Text!.ToLower().Contains("deezer"))
            {
                _player = message.Text;
                await SendSearchMethod(botClient, message, token);
            }
            else if (message.Text!.ToLower().Contains("change location"))
            { 
                await SendLocationRequest(botClient, message, token);
            }
            else if (message.Text!.ToLower().Contains("search playlist by weather"))
            {
               await SendMusicPlaylistRecommendation(botClient, message, token, null);
            }
            else if (message.Text!.ToLower().Contains("search playlist by name"))
            {
                _step = message.Text.ToLower();
                message.Text = null;
                await botClient.SendTextMessageAsync(message.Chat.Id, "Write the name of the playlist:", cancellationToken:token);
                return;
            }
            else if (message.Text!.ToLower().Contains("search track by name"))
            {
                _step = message.Text.ToLower();
                message.Text = null;
                await botClient.SendTextMessageAsync(message.Chat.Id, "Write the name of the track:",
                    cancellationToken: token);
                return;
            }
            else if (message.Text!.ToLower().Contains("back"))
            {
                await SendMusicOptionRequest(botClient, message, token);
            }
            
            if (_step == "search playlist by name" && message.Text.ToLower() != "back" )
            {
                await SendMusicPlaylistRecommendation(botClient, message, token, message.Text);
                _step = null;
            }
            
            if (_step == "search track by name" && message.Text.ToLower() != "back"  )
            {
                await SendMusicRecommendation(botClient, message, token, message.Text);
                _step = null;
            }
        }

        if (message?.Type == MessageType.Location)
        {
            _latitude = message.Location!.Latitude;
            _longitude = message.Location.Longitude;
            await UpdateUser(message);
            await SendMusicOptionRequest(botClient, message, token);
        }
    }

    //------------------------------------------Buttons--------------------------------------

    static async Task SendSearchMethod(ITelegramBotClient botClient, Message message, CancellationToken token)
    {
        ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
        {
            new KeyboardButton[] { "Search playlist by weather" },
            new KeyboardButton[] { "Search playlist by name" },
            new KeyboardButton[] { "Search track by name" },
            new KeyboardButton[] { "Back" }
        })
        {
            ResizeKeyboard = true
        };

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "Select your search method:",
            replyMarkup: replyKeyboardMarkup,
            cancellationToken: token);
    }
    static async Task SendMusicOptionRequest(ITelegramBotClient botClient, Message message, CancellationToken token)
    {
        ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
        {
            new KeyboardButton[] { "Spotify" },
            new KeyboardButton[] { "YouTube" },
            new KeyboardButton[] { "Deezer" },
            new KeyboardButton[] { "Change location" },
        })
        {
            ResizeKeyboard = true
        };

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "Choose your player: ",
            replyMarkup: replyKeyboardMarkup,
            cancellationToken: token);
    }
    
    static async Task SendUserAuthentication(ITelegramBotClient botClient, Message message, CancellationToken token)
    {
        ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
        {
            new KeyboardButton[] { "Authentication" },
        })
        {
            ResizeKeyboard = true
        };

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "Confirm",
            replyMarkup: replyKeyboardMarkup,
            cancellationToken: token);
    }

    static async Task SendLocationRequest(ITelegramBotClient botClient, Message message, CancellationToken token)
    {
        ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
        {
            KeyboardButton.WithRequestLocation("Share Location"),
        });

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "Confirm",
            replyMarkup: replyKeyboardMarkup,
            cancellationToken: token);
    }
    
    //------------------------------------------SQL--------------------------------------
    static async Task Authorization(ITelegramBotClient botClient, Message message, CancellationToken token)
    {
        var user = new User
        {
            ChatId = message.Chat.Id,
            Username = message.Chat.Username ?? "--",
            Firstname = message.Chat.FirstName ?? "--",
            Latitude = _latitude,
            Longitude = _longitude
        };

        var json = JsonConvert.SerializeObject(user);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        var url = "https://weathermusicapi.azurewebsites.net/Db/CheckUser"; 
        using var client = new HttpClient();

        var response = await client.PostAsync(url, data);

        if (response.IsSuccessStatusCode)
        {
            bool exists = JsonConvert.DeserializeObject<bool>(await response.Content.ReadAsStringAsync());
            if (exists)
            {
                await SendMusicOptionRequest(botClient, message, token);
                await GetLocationSql(message);
            }
            else
            {
                await SendUserAuthentication(botClient, message, token);
            }
        }
        else
        {
            //Console.WriteLine($"Error: {response.StatusCode}");
        }
    }

    static async Task UpdateUser(Message message)
    {
        var user = new User
        {
            ChatId = message.Chat.Id,
            Username = message.Chat.Username ?? "--",
            Firstname = message.Chat.FirstName ?? "--",
            Latitude = _latitude, 
            Longitude = _longitude 
        };

        var json = JsonConvert.SerializeObject(user);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = new HttpClient();

        var url = "https://weathermusicapi.azurewebsites.net/Db/UpdateUser"; 
        var response = await client.PutAsync(url, data);

        if (response.IsSuccessStatusCode)
        {
            //Console.WriteLine("User updated successfully.");
        }
        else
        {
            //Console.WriteLine($"Error: {response.StatusCode}");
        }
    }

    static async Task SendSqlRequest(Message message)
    {
        var user = new User
        {
            ChatId = message.Chat.Id,
            Username = message.Chat.Username ?? "--",
            Firstname = message.Chat.FirstName ?? "--",
            Latitude = _latitude,
            Longitude = _longitude
        };
        
        var json = JsonConvert.SerializeObject(user);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = new HttpClient();
        
        var sqlPostUrl =
            $"https://weathermusicapi.azurewebsites.net/Db?";
        var sqlPost = await client.PostAsync(sqlPostUrl, data);
        
        if (sqlPost.IsSuccessStatusCode)
        {
            string result = await sqlPost.Content.ReadAsStringAsync();
            //Console.WriteLine(result);
        }
        else
        {
           // Console.WriteLine($"Error: {sqlPost.StatusCode}");
        }
    }

    //------------------------------------------Music-Players--------------------------------------

    static async Task SendMusicPlaylistRecommendation(ITelegramBotClient botClient, Message message, CancellationToken token, string userInput)
    {
        var weatherResponse = await GetWeatherInfo();
        var musicRequestUrl = " ";

        if (userInput == null)
        {
            musicRequestUrl = $"https://weathermusicapi.azurewebsites.net/{_player}/searchPlaylist/{weatherResponse}%20music";
        }
        else
        {
            musicRequestUrl = $"https://weathermusicapi.azurewebsites.net/{_player}/searchPlaylist/{userInput}";
        }
        var musicResponse = await HttpClient.GetStringAsync(musicRequestUrl);

        //Console.WriteLine($"{message.Chat.Id}, {message.Chat.Username}, {message.Chat.FirstName}, {message.Chat.LastName}, {_latitude}, {_longitude}");

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: musicResponse,
            cancellationToken: token);
    }

    static async Task SendMusicRecommendation(ITelegramBotClient botClient, Message message, CancellationToken token, string userInput)
    {
        var musicRequestUrl = " ";
        
        musicRequestUrl = $"https://weathermusicapi.azurewebsites.net/{_player}/searchTrack/{userInput}";
        
        var musicResponse = await HttpClient.GetStringAsync(musicRequestUrl);

        //Console.WriteLine($"{message.Chat.Id}, {message.Chat.Username}, {message.Chat.FirstName}, {message.Chat.LastName}, {_latitude}, {_longitude}");

        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: musicResponse,
            cancellationToken: token);
    }

    //------------------------------------------Weather--------------------------------------
    static async Task<string> GetWeatherInfo()
    {
        var weatherRequestUrl = $"https://weathermusicapi.azurewebsites.net/Weather/{_latitude.ToString("F6", CultureInfo.InvariantCulture)}/{_longitude.ToString("F6", CultureInfo.InvariantCulture)}";
        return await HttpClient.GetStringAsync(weatherRequestUrl);
    }

    static async Task GetLocationSql(Message message)
    {
        long chatId = message.Chat.Id; 

        using var client = new HttpClient();

        var url = $"https://weathermusicapi.azurewebsites.net/Db/GetUserLocation/{chatId}"; 
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var location = JsonConvert.DeserializeObject<(double Latitude, double Longitude)>(await response.Content.ReadAsStringAsync());
            _latitude = location.Latitude;
            _longitude = location.Longitude;
            //Console.WriteLine($"User's Latitude: {location.Latitude}, Longitude: {location.Longitude}");
        }
        else
        {
            //Console.WriteLine($"Error: {response.StatusCode}");
        }
    }

    static Task Error(ITelegramBotClient arg1, Exception arg2, CancellationToken arg3)
    {
        throw new NotImplementedException();
    }
}

class User
{
    public long ChatId { get; set; }
    public string Username { get; set; } 
    public string Firstname { get; set; } 
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}