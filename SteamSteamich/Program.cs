using System;
using System.Collections.Generic;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.ReplyMarkups;
using Newtonsoft.Json.Linq;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Net.Http;

namespace Telegram_Game_Crypto_Bot
{
    class Program
    {
        private static string token { get; set; } = "1937281773:AAF418ycvSWzCbzbrVrnhkXZd-TewIFJA8U";
        private static string key { get; set; } = "066A516B7E67287A5985241BDF804C21";
        private static TelegramBotClient client;
        public string steamId { get; set; } = "";

        private static string PathJson { get; set; } = "./data.db";

        static void Main(string[] args)
        {
            client = new TelegramBotClient(token) { Timeout = TimeSpan.FromSeconds(10) };
            client.StartReceiving();
            client.OnMessage += OnMessageHandler;
            Console.ReadLine();
            client.StopReceiving();
        }

        private static void OnMessageHandler(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            if (msg.Text != null)
            {
                //Console.WriteLine($"Пришло сообщение с текстом: {msg.Text}");
                //await client.SendTextMessageAsync(msg.Chat.Id, msg.Text, replyMarkup: GetButtons());
                switch (msg.Text)
                {
                    case "/start":
                        StartMethod(sender, e);
                        break;

                    case "Edit ID":
                        EditIdMethod(sender, e);
                        break;

                    case "Check Online Friends":
                        CheckOnlineFriendsMethod(sender, e);
                        break;
                    case "Check Friends With Ban":
                        CheckFriendBanMethod(sender, e);
                        break;
                    default:
                        DefaultMethod(sender, e);
                        break;
                }
            }
        }

        public async static void StartMethod(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            await client.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Приветствую тебя! :)",
                            replyMarkup: GetButtons());
            await client.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Давай начнем...",
                            replyMarkup: GetButtons());
            await client.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Steam ID:",
                            replyMarkup: new ForceReplyMarkup { Selective = true });
        }

        public async static void EditIdMethod(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            await client.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Steam ID:",
                            replyMarkup: new ForceReplyMarkup { Selective = true });
        }

        public static void CheckFriendBanMethod(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            string steam_Id = GetSteamIdFromDB(msg.From.Id.ToString());
            string responseGetFriendList = new WebClient().DownloadString(
                $"http://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={key}&steamid={steam_Id}&relationship=friend");
            JObject jObject = JObject.Parse(responseGetFriendList);
            var friendsSteamId = jObject["friendslist"]["friends"].Children()
                .Select(x => x["steamid"].ToString())
                .ToList();

            var SteamIdJson = JsonSerializer.Serialize(friendsSteamId);
            int friendsWithBan = 0;
            string responseGetPlayerBans = new WebClient().DownloadString(
                $"http://api.steampowered.com/ISteamUser/GetPlayerBans/v1//?key={key}&steamids={SteamIdJson}&format=json");
            jObject = JObject.Parse(responseGetPlayerBans);
            var friendsBanInfo = jObject["players"].Children().Select(friend => {
                return new FriendsBanInfo
                {
                    SteamId = friend["SteamId"].ToString(),
                    CommunityBanned = friend["CommunityBanned"].ToObject<bool>(),
                    VACBanned = friend["VACBanned"].ToObject<bool>(),
                    NumberOfVACBans = friend["NumberOfVACBans"].ToObject<int>(),
                    DaysSinceLastBan = friend["DaysSinceLastBan"].ToObject<int>(),
                    NumberOfGameBans = friend["NumberOfGameBans"].ToObject<int>(),
                    EconomyBan = friend["EconomyBan"].ToString()
                };
            }).ToList();

            /*
             {"players":[{"SteamId":"76561198126997004","CommunityBanned":false,"VACBanned":false,"NumberOfVACBans":0,"DaysSinceLastBan":0,"NumberOfGameBans":0,"EconomyBan":"none"}]}
             */
            string text = "";
            if (friendsBanInfo.Count == 0)
            {
                text = $"No one has a VAC Ban";

                client.SendTextMessageAsync(
                    chatId: msg.Chat.Id,
                    text: text,
                    replyMarkup: GetButtons()).GetAwaiter().GetResult();
                return;
            }

            foreach (var friend in friendsBanInfo)
            {
                if ((friend.NumberOfVACBans != 0) || (friend.NumberOfGameBans != 0))
                {
                    friendsWithBan++;
                    string responseGetPlayerSummaries = new WebClient().DownloadString(
                        $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={key}&steamids={friend.SteamId}");
                    JObject friendInfoTemp = JObject.Parse(responseGetPlayerSummaries);
                    JToken friendInfo = friendInfoTemp["response"]["players"][0];

                    friend.PersonalName = friendInfo["personaname"].ToString();
                    friend.Url = friendInfo["profileurl"].ToString();
                    friend.PictureUrl = friendInfo["avatarfull"].ToString();

                    string isVacBanned = (friend.VACBanned == true) ? "✅" : "❌";
                    string isCommunityBanned = (friend.CommunityBanned == true) ? "✅" : "❌";
                    var httpClient = new HttpClient();
                    var streamPicture = httpClient.GetStreamAsync(friend.PictureUrl).Result;


                    text = $"{friend.PersonalName} ({friend.SteamId})\n" +
                        $"VAC Banned: {isVacBanned}\n" +
                        $"Days since last ban: {friend.DaysSinceLastBan}\n" +
                        $"Community banned: {isCommunityBanned}\n" +
                        $"Number of VAC bans: {friend.NumberOfVACBans} \n" +
                        $"Number of GAME bans: {friend.NumberOfGameBans}\n" +
                        $"Economy ban: {friend.EconomyBan}";
                    client.SendPhotoAsync(
                        chatId: msg.Chat.Id,
                        photo: streamPicture,
                        caption: text,
                        replyMarkup: GetInlineUrlButton(Convert.ToString(friend.Url))).GetAwaiter().GetResult();
                }
            }
            text = $"Friends with bans: {friendsWithBan}";

            client.SendTextMessageAsync(
                chatId: msg.Chat.Id,
                text: text,
                replyMarkup: GetButtons()).GetAwaiter().GetResult();
            return;
        }
        public static void CheckOnlineFriendsMethod(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            string steam_Id = GetSteamIdFromDB(msg.From.Id.ToString());
            try
            {
                int onlineFriendsCount = 0;
                string responseGetFriendList = new WebClient().DownloadString(
                $"http://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={key}&steamid={steam_Id}&relationship=friend");
                JObject jObject = JObject.Parse(responseGetFriendList);
                var friendsSteamId = jObject["friendslist"]["friends"].Children()
                    .Select(x => x["steamid"].ToString())
                    .ToList();
                var SteamIdJson = JsonSerializer.Serialize(friendsSteamId);
                string friendResponse = new WebClient().DownloadString(
                    $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={key}&steamids={SteamIdJson}&format=json");
                JObject friendInfoObject = JObject.Parse(friendResponse);

                var friendsInfo = friendInfoObject["response"]["players"].Children().Select(friend => {
                    return new FriendsInfo
                    {
                        SteamId = friend["steamid"].ToString(),
                        CommunityVisibilityState = friend["communityvisibilitystate"].ToObject<int>(),
                        ProfileState = friend["profilestate"].ToObject<int>(),
                        PersonaName = friend["personaname"].ToString(),
                        //CommentPermission = friend["commentpermission"].ToObject<int>(),
                        ProfileUrl = friend["profileurl"].ToString(),
                        Avatar = friend["avatar"].ToString(),
                        AvatarMedium = friend["avatarmedium"].ToString(),
                        AvatarFull = friend["avatarfull"].ToString(),
                        AvatarHash = friend["avatarhash"].ToString(),
                        LastLogOff = friend["lastlogoff"].ToObject<int>(),
                        PersonaState = friend["personastate"].ToObject<int>(),
                        //RealName = friend["realname"].ToString(),
                        //PrimaryClanId = friend["primaryclanid"].ToString(),
                        //TimeCreated = friend["timecreated"].ToObject<int>(),
                        PersonaStateFlags = friend["personastateflags"].ToObject<int>(),
                        //GameExtraInfo = friend["gameextrainfo"].ToString(),
                        //GameId = friend["gameid"].ToString(),
                        //LocCountryCode = friend["loccountrycode"].ToString()
                    };
                }).ToList();

                string text = "";
                if (friendsInfo.Count == 0)
                {
                    text = $"No friends online";

                    client.SendTextMessageAsync(
                        chatId: msg.Chat.Id,
                        text: text,
                        replyMarkup: GetButtons()).GetAwaiter().GetResult();
                    return;
                }

                foreach (var friend in friendsInfo)
                {
                    if (friend.PersonaState != 0)
                    {
                        onlineFriendsCount++;
                        string status = "";
                        switch (friend.PersonaState)
                        {
                            case 0:
                                status = "🔴OFFLINE🔴";
                                break;
                            case 1:
                                status = "🟢ONLINE🟢";
                                break;
                            case 2:
                                status = "BUSY";
                                break;
                            case 3:
                                status = "AWAY";
                                break;
                            case 4:
                                status = "SNOOZY";
                                break;
                            case 5:
                                status = "LOOKING TO TRADE";
                                break;
                            case 6:
                                status = "LOOKING TO PLAY";
                                break;
                        }

                        var httpClient = new HttpClient();
                        var streamPicture = httpClient.GetStreamAsync(friend.AvatarFull).Result;

                        text = $"Nickname: {friend.PersonaName} ({friend.SteamId})\n" +
                            $"Status: {status}\n" +
                            //$"Real name: {friend.RealName}\n" +
                            $"Last logoff: {friend.LastLogOff}\n";
                            //$"Time created: {friend.TimeCreated}\n" +
                            //$"Game extra info: {friend.GameExtraInfo}";
                        client.SendPhotoAsync(
                            chatId: msg.Chat.Id,
                            photo: streamPicture,
                            caption: text,
                            replyMarkup: GetInlineUrlButton(Convert.ToString(friend.ProfileUrl))).GetAwaiter().GetResult();
                    }
                }
                text = $"Friends online: {onlineFriendsCount}";

                client.SendTextMessageAsync(
                    chatId: msg.Chat.Id,
                    text: text,
                    replyMarkup: GetButtons()).GetAwaiter().GetResult();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION:" + ex);
                client.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Не получилось :(",
                            replyMarkup: GetButtons()).GetAwaiter().GetResult();
            }
        }

        private static string GetSteamIdFromDB(string telegramId)
        {
            using (var connection = new SqliteConnection($"Data Source={PathJson}"))
            {
                connection.Open();

                SqliteCommand command = new SqliteCommand();
                command.Connection = connection;
                command.CommandText = $"SELECT * FROM users_info WHERE telegram_id == {telegramId}";
                SqliteDataReader sqlReader = command.ExecuteReader();
                while (sqlReader.Read())
                {
                    return sqlReader[2].ToString();
                }
                sqlReader.Close();
                connection.Close();
            }
            return null;
        }

        public async static void DefaultMethod(object sender, MessageEventArgs e)
        {
            var msg = e.Message;
            if (msg.ReplyToMessage != null && msg.ReplyToMessage.Text.Contains("Steam ID:"))
            {
                using (var connection = new SqliteConnection($"Data Source={PathJson}"))
                {
                    connection.Open();
                    SqliteCommand command = new SqliteCommand();
                    command.Connection = connection;
                    command.CommandText = $"SELECT * FROM users_info WHERE telegram_id == {msg.From.Id}"; // AND steam_id == {msg.Text}
                    SqliteDataReader sqlReader = command.ExecuteReader();
                    if (sqlReader.HasRows == false)
                    {
                        command.CommandText = $"INSERT INTO users_info (telegram_id, steam_id) VALUES ('{msg.From.Id}', '{msg.Text}')";
                        int number = command.ExecuteNonQuery();
                        Console.WriteLine($"В таблицу users_info добавлена новая запись: telegram_id = {msg.From.Id}, steam_id = {msg.Text}");
                        await client.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Steam ID saved!",
                            replyMarkup: GetButtons());
                    }
                    else
                    {
                        sqlReader.Close();
                        command.CommandText = $"UPDATE users_info SET steam_id = {msg.Text} WHERE telegram_id == {msg.From.Id}";
                        int number = command.ExecuteNonQuery();
                        Console.WriteLine($"В таблице users_info обновлена запись: telegram_id = {msg.From.Id}, steam_id = {msg.Text}");
                        await client.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: "Steam ID updated!",
                            replyMarkup: GetButtons());
                    }
                    connection.Close();
                }
                return;
            }

            await client.SendTextMessageAsync(
                chatId: msg.Chat.Id,
                text: "Я тебя не понимать -_- ...",
                replyMarkup: GetButtons());
        }
        
        private static IReplyMarkup GetInlineUrlButton(string url)
        {
            return new InlineKeyboardMarkup(
            new InlineKeyboardButton[][]
            {
                new [] {
                    new InlineKeyboardButton(){
                    Url = url,
                    Text = "Profile",},
                },
            });
        }
        private static IReplyMarkup GetButtons()
        {
            return new ReplyKeyboardMarkup
            {

                Keyboard = new List<List<KeyboardButton>>
                {
                    new List<KeyboardButton> { new KeyboardButton { Text = "Edit ID" }, new KeyboardButton { Text = "Check Online Friends" } },
                    new List<KeyboardButton> { new KeyboardButton { Text = "Check Friends With Ban" } }
                },
                ResizeKeyboard = true
                
            };
        }
        
        public class FriendsInfo
        {
            public string SteamId { get; set; }
            public int CommunityVisibilityState { get; set; }
            public int ProfileState { get; set; }
            public string PersonaName { get; set; }
            public int CommentPermission { get; set; }
            public int LastLogOff { get; set; }
            public string ProfileUrl { get; set; }
            public string Avatar { get; set; }
            public string AvatarMedium { get; set; }
            public string AvatarFull { get; set; }
            public string AvatarHash { get; set; }
            public int PersonaState { get; set; }
            public string RealName { get; set; }
            public string PrimaryClanId { get; set; }
            public int TimeCreated { get; set; }
            public int PersonaStateFlags { get; set; }
            public string GameExtraInfo { get; set; }
            public string GameId { get; set; }
            public string LocCountryCode { get; set; }
            public FriendsInfo()
            {
                SteamId = "";
                CommunityVisibilityState = 0;
                ProfileState = 0;
                PersonaName = "";
                CommentPermission = 0;
                LastLogOff = 0;
                ProfileUrl = "";
                Avatar = "";
                AvatarMedium = "";
                AvatarFull = "";
                AvatarHash = "";
                PersonaState = 0;
                RealName = "";
                PrimaryClanId = "";
                TimeCreated = 0;
                PersonaStateFlags = 0;
                GameExtraInfo = "";
                GameId = "";
                LocCountryCode = "";
            }
        }
        public class FriendsBanInfo
        {
            public string SteamId { get; set; }
            public bool CommunityBanned { get; set; }
            public bool VACBanned { get; set; }
            public int NumberOfVACBans { get; set; }
            public int DaysSinceLastBan { get; set; }
            public int NumberOfGameBans { get; set; }
            public string EconomyBan { get; set; }
            public string Url { get; set; }
            public string PersonalName { get; set; }
            public string PictureUrl { get; set; }

            public FriendsBanInfo()
            {
                SteamId = "";
                CommunityBanned = false;
                VACBanned = false;
                NumberOfVACBans = 0;
                DaysSinceLastBan = 0;
                NumberOfGameBans = 0;
                EconomyBan = "";
                Url = "";
                PersonalName = "";
                PictureUrl = "";
            }
        }
    }
}
