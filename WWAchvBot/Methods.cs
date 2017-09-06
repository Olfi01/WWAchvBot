using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static WWAchvBot.Program;

namespace WWAchvBot
{
    class Methods
    {
        public static void LogToChannel(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                SendMessage(text, logchannel);
            }
        }

        public static BotUser GetUser(Update update, string[] args)
        {
            if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.From.Id != Bot.Id) return BotUser.users.Values.First(x => x.Id == update.Message.ReplyToMessage.From.Id);
            if (!string.IsNullOrEmpty(args[1]) && int.TryParse(args[1].Split(' ')[0], out int userid) && BotUser.users.Any(x => x.Key == userid)) return BotUser.users.Values.First(x => x.Id == userid);
            if (!string.IsNullOrEmpty(args[1]) && args[1].Split(' ')[0].StartsWith("@") && BotUser.users.Any(x => x.Value.Username.ToLower() == args[1].Split(' ')[0].ToLower().Remove(0, 1))) return BotUser.users.Values.First(x => x.Username.ToLower() == args[1].Split(' ')[0].ToLower().Remove(0, 1));
            if (!string.IsNullOrEmpty(args[1]) && update.Message.Entities.Any(x => x.Type == MessageEntityType.TextMention) && update.Message.Entities.First(x => x.Type == MessageEntityType.TextMention).Offset == update.Message.Text.IndexOf(' ') + 1 && BotUser.users.Any(x => x.Value.Id == update.Message.Entities.First(e => e.Type == MessageEntityType.TextMention).User.Id)) return BotUser.users.Values.First(y => y.Id == update.Message.Entities.First(x => x.Type == MessageEntityType.TextMention).User.Id);
            return BotUser.users.Values.First(x => x.Id == update.Message.From.Id);
        }

        public static string FormatHTML(string str)
        {
            return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        public static void ClearGames()
        {
            while (true)
            {
                List<Tuple<long, DateTime>> list = new List<Tuple<long, DateTime>>();
                foreach (var g in Game.Games) list.Add(new Tuple<long, DateTime>(g.Key, g.Value.Updatetime));

                DateTime now = DateTime.UtcNow;

                foreach (var l in list)
                {
                    if (l.Item2.AddMinutes(20) < now)
                    {
                        ReplyToMessage("<b>Your game was aborted as it was inactive for 20 minutes!</b>", l.Item1, Game.Games[l.Item1].Pinmessage.MessageId);
                        Game.Games[l.Item1].Stop();
                        Game.Games[l.Item1] = null;
                        Game.Games.Remove(l.Item1);
                    }
                    else if (l.Item2.AddMinutes(10) < now && !Game.Games[l.Item1].Notified)
                    {
                        if (Game.Games[l.Item1].Gamestate == Game.State.Joining)
                        {
                            ReplyToMessage("<b>Your game was aborted as it didn't start and was inactive for 10 minutes!</b>", l.Item1, Game.Games[l.Item1].Pinmessage.MessageId);
                            Game.Games[l.Item1].Stop();
                            Game.Games[l.Item1] = null;
                            Game.Games.Remove(l.Item1);
                        }
                        else
                        {
                            ReplyToMessage("<b>Warning!</b>\n\nYour game has been inactive for 10 minutes! It will soon be removed if noone sends a message! (If it isn't running anymore, someone should just double tap the 'Stop' button)", l.Item1, Game.Games[l.Item1].Pinmessage.MessageId);
                            Game.Games[l.Item1].Notified = true;
                        }
                    }
                }

                System.Threading.Thread.Sleep(120000);
            }
        }

        public static List<string> SplitString(string text, int split)
        {
            List<string> texts = new List<string>();
            while (text.Length > split)
            {
                texts.Add(text.Substring(0, split));
                text = text.Remove(0, split);
            }
            texts.Add(text);
            return texts;
        }

        public static string GetAchievements(int userid)
        {
            var url = "http://tgwerewolf.com/stats/playerachievements/?pid=" + userid + "&json=true";

            WebClient wc = new WebClient();
            byte[] raw = wc.DownloadData(url);
            string res = Encoding.UTF8.GetString(raw);
            return res;
        }

        #region Messages
        public static Message SendMessage(string text, long chatid, IReplyMarkup replyMarkup = null, bool disableWebPagePreview = true, ParseMode parseMode = ParseMode.Html, bool throwonerror = false)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    var task = client.SendTextMessageAsync(chatid, text, disableWebPagePreview: disableWebPagePreview, parseMode: parseMode, replyMarkup: replyMarkup);
                    task.Wait();
                    var msg = task.Result;
                    return msg;
                }
                catch (Exception e)
                {
                    if (!throwonerror && e is AggregateException && ((AggregateException)e).InnerExceptions.Any(x => x.Message.ToLower().Contains("can't parse entities in message text")))
                    {
                        return SendMessage(text + "\n\nThis message has a markdown fail, please inform @Olgabrezel", chatid, replyMarkup, disableWebPagePreview, ParseMode.Default, true);
                    }
                    SendError(e, chatid);
                }
            }
            return null;
        }

        public static Message ReplyToMessage(string text, Update update, IReplyMarkup replyMarkup = null, bool disableWebPagePreview = true, ParseMode parseMode = ParseMode.Html)
        {
            if (update.Message != null) return ReplyToMessage(text, update.Message.Chat.Id, update.Message.MessageId, replyMarkup, disableWebPagePreview, parseMode);
            return null;
        }

        public static Message ReplyToMessage(string text, long chatid, int messageid, IReplyMarkup replyMarkup = null, bool disableWebPagePreview = true, ParseMode parseMode = ParseMode.Html, bool throwonerror = false)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    var task = client.SendTextMessageAsync(chatid, text, replyToMessageId: messageid, parseMode: parseMode, replyMarkup: replyMarkup, disableWebPagePreview: disableWebPagePreview);
                    task.Wait();
                    var msg = task.Result;
                    return msg;
                }
                catch (Exception e)
                {
                    if (!throwonerror && e is AggregateException && ((AggregateException)e).InnerExceptions.Any(x => x.Message.ToLower().Contains("can't parse entities in message text")))
                    {
                        return ReplyToMessage(text + "\n\nThis message has a markdown fail, please inform @Olgabrezel", chatid, messageid, replyMarkup, disableWebPagePreview, ParseMode.Default, true);
                    }
                    else
                    {
                        SendError(e, chatid);
                        return null;
                    }
                }
            }
            return null;
        }

        public static Message EditMessage(string text, Message message, IReplyMarkup replyMarkup = null, bool disableWebPagePreview = true, ParseMode parseMode = ParseMode.Html, bool throwonerror = false)
        {
            if (!string.IsNullOrEmpty(text) && message != null)
            {
                var chatid = message.Chat.Id;
                var messageid = message.MessageId;

                try
                {
                    var task = client.EditMessageTextAsync(chatid, messageid, text, parseMode, disableWebPagePreview, replyMarkup);
                    task.Wait();
                    var msg = task.Result;
                    return msg;
                }
                catch (AggregateException e)
                {
                    if (e.InnerExceptions.Any(x => x.Message.Contains("message is not modified"))) return message;
                    else if (!throwonerror && e.InnerExceptions.Any(x => x.Message.ToLower().Contains("can't parse entities in message text")))
                    {
                        return EditMessage(text + "\n\nThis message has a markdown fail, please inform @Olgabrezel", message, replyMarkup, disableWebPagePreview, ParseMode.Default, true);
                    }
                    else SendError(e, chatid);
                }
                catch (Exception e)
                {
                    SendError(e, chatid);
                }
            }
            return null;
        }

        public static bool PinMessage(Message msg, bool disableNotification = true)
        {
            try
            {
                var t = client.PinChatMessageAsync(msg.Chat.Id, msg.MessageId, disableNotification);
                t.Wait();
                return t.Result;
            }
            catch
            {
                return false;
            }
        }

        public static bool UnpinMessage(long chatid)
        {
            try
            {
                var t = client.UnpinChatMessageAsync(chatid);
                t.Wait();
                return t.Result;
            }
            catch
            {
                return false;
            }
        }

        public static Message GetPinnedMessage(long chatid)
        {
            try
            {
                var t = client.GetChatAsync(chatid);
                t.Wait();
                return null; // this will return "t.Result.PinMessage" as soon as telegram.bot is up-to-date
            }
            catch
            {
                return null;
            }
        }

        public static void SendError(Exception e, long chatid, bool informGroup = true)
        {
            var error = e.Message + "\n\n";
            while (e.InnerException != null)
            {
                e = e.InnerException;
                error += e.Message + "\n\n";
            }
            error += e.StackTrace;
            var errorchat = errorsupression ? adminIds[0] : testgroup;
            client.SendTextMessageAsync(errorchat, error).Wait();
            if (informGroup) client.SendTextMessageAsync(chatid, "Tried to send, edit, etc. something in this chat but failed! The dev was informed! Sorry!").Wait();
        }

        public static bool DeleteMessage(Message msg)
        {
            try
            {
                var t = client.DeleteMessageAsync(msg.Chat.Id, msg.MessageId);
                t.Wait();
                return t.Result;
            }
            catch
            {
                return false;
            }
        }

        public static Message SendBackup(long chatid = 0)
        {
            if (chatid == 0) chatid = testgroup;

#if RELEASE
            var fs = new FileStream("C:\\Olgabrezel\\AchvBot.sqlite", FileMode.Open);
            var t = client.SendDocumentAsync(chatid, new FileToSend("AchvBot.sqlite", fs), "#AchvBotBackup");
            t.Wait();
            fs.Close();
            return t.Result;
#else
            return SendMessage("*this would be a backup if I was running on release*", chatid);
#endif
        }
        #endregion

        #region Other API Methods
        public static bool IsGroupAdmin(Update update)
        {
            return IsGroupAdmin(update.Message.From.Id, update.Message.Chat.Id);
        }

        public static bool IsGroupAdmin(int userid, long chatid)
        {
            var t = client.GetChatMemberAsync(chatid, userid);
            t.Wait();
            return new[] { ChatMemberStatus.Creator, ChatMemberStatus.Administrator }.Contains(t.Result.Status);
        }

        public static bool IsGroupMember(int userid, long chatid)
        {
            var t = client.GetChatMemberAsync(chatid, userid);
            t.Wait();
            return new[] { ChatMemberStatus.Creator, ChatMemberStatus.Administrator, ChatMemberStatus.Member }.Contains(t.Result.Status);
        }

        public static List<int> GetGroupAdmins(long chatid)
        {
            var t = client.GetChatAdministratorsAsync(chatid);
            t.Wait();
            return t.Result.Select(e => e.User.Id).ToList();
        }

        public static User ResolveUser(int userid, long chatid)
        {
            var t = client.GetChatMemberAsync(chatid, userid);
            t.Wait();
            return t.Result.User;
        }
#endregion

        public static class BotUpdate
        {
            public static void Run(object obj)
            {
                var updateMessage = (Message)obj;
                updateMessage = EditMessage(updateMessage.Text + "\n\n<b>Pulling git...</b>", updateMessage);
                System.Diagnostics.Process.Start(gitPullFile).WaitForExit();
                updateMessage = EditMessage(updateMessage.Text + "\nGit pulled.\n\n<b>Restoring nuget packages...</b>", updateMessage);
                System.Diagnostics.Process.Start(nugetRestoreFile).WaitForExit();
                updateMessage = EditMessage(updateMessage.Text + "\nPackages restored.\n\n<b>Building release...</b>", updateMessage);
                System.Diagnostics.Process.Start(buildFile).WaitForExit();
                updateMessage = EditMessage(updateMessage.Text + "\nRelease built.\n\n<b>Copying release to bot...</b>", updateMessage);
                var path = destinationReleasePath + "Build_" + DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss");
                System.IO.Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start("xcopy.exe", $"/E {sourceReleasePath} {path}").WaitForExit();
                updateMessage = EditMessage(updateMessage.Text + "\nRelease copied to bot. Path:\n\n" + path + "\\WWAchvBot.exe\n\n<b>Operation complete.</b>", updateMessage);
            }
        }

        public static class SQL
        {
#region Bot Table
            public static string ReadToken()
            {
                var query = "select token from bot";

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                reader.Read();

                var t = (string)reader[0];

                conn.Close();

                return t;
            }

            public static long ReadAchvId()
            {
                var query = "select achvid from bot";

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                reader.Read();

                var t = (long)reader[0];

                conn.Close();

                return t;
            }

            public static string ReadAchvLink()
            {
                var query = "select achvlink from bot";

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                reader.Read();

                var t = (string)reader[0];

                conn.Close();

                return t;
            }

            public static long ReadLogId()
            {
                var query = "select logchannel from bot";

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                reader.Read();

                var t = (long)reader[0];

                conn.Close();

                return t;
            }

            public static long ReadTestId()
            {
                var query = "select testid from bot";

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                reader.Read();

                var t = (long)reader[0];

                conn.Close();

                return t;
            }
#endregion

#region Users
            public static Dictionary<int, BotUser> ReadUsers()
            {
                var query = "select * from users";

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                var users = new Dictionary<int, BotUser>();

                while (reader.Read())
                {
                    var achv = reader[5] is DBNull ? "" : (string)reader[5];
                    users.Add((int)reader[0], new BotUser((int)reader[0], ((string)reader[1]).Replace("''", "'"), (string)reader[2], (int)reader[3], (bool)reader[4], achv));
                }
                conn.Close();

                return users;
            }

            public static void ModifyUser(string action, BotUser user)
            {
                var query = "";
                var subsc = user.Subscribing ? 1 : 0;
                string achv = string.Join("|", user.Achievements.Select(x => Game.achv[x]).ToList()).Replace("'", "''");


                switch (action)
                {
                    case "add":
                        query = $"insert into users values ({user.Id}, '{user.Name.Replace("'", "''")}', '{user.Username.ToLower()}', {user.Gamecount}, {subsc}, '{achv}')";
                        break;

                    case "modify":
                        query = $"update users set name = '{user.Name.Replace("'", "''")}', username = '{user.Username.ToLower()}', gamecount = {user.Gamecount}, subscribing = {subsc}, achievements = '{achv}' where id = {user.Id}";
                        break;

                    case "delete":
                        query = $"delete from users where id = {user.Id}";
                        break;

                    default:
                        return;
                }

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                comm.ExecuteNonQuery();
                conn.Close();
            }
#endregion

#region Aliases
            public static Dictionary<string, Game.Roles> ReadAliases()
            {
                var query = "select * from rolealiases";

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                var aliases = new Dictionary<string, Game.Roles>();

                foreach (var da in Game.defaultAliases)
                {
                    aliases.Add(da.Key.ToLower(), da.Value);
                }

                while (reader.Read())
                {
                    try
                    {
                        aliases.Add(((string)reader[0]).Replace("''", "'"), Game.defaultAliases[(string)reader[1]]);
                    }
                    catch (Exception e)
                    {
                        SendMessage("Failed adding alias " + (string)reader[0] + " (" + (string)reader[1] + "). Exception:\n\n" + e.Message, ludwig);
                    }
                }
                conn.Close();
                return aliases;
            }

            public static void ModifyAliases(string action, string alias, string id)
            {
                var query = "";
                alias = alias.ToLower();

                switch (action)
                {
                    case "add":
                        if (Game.defaultAliases.Select(x => x.Key.ToLower()).Contains(alias)) return;
                        query = $"insert into rolealiases values ('{alias.Replace("'", "''")}', '{id}')";
                        break;

                    case "modify":
                        query = $"update rolealiases set id = '{id}' where alias like '{alias.Replace("'", "''")}'";
                        break;

                    case "delete":
                        query = $"delete from rolealiases where alias like '{alias.Replace("'", "''")}'";
                        break;

                    default:
                        return;
                }

                var conn = new SQLiteConnection(connectionstring);
                conn.Open();

                var comm = new SQLiteCommand(query, conn);
                comm.ExecuteNonQuery();
                conn.Close();
            }
#endregion

#region Admins
            public static List<int> ReadAdmins()
            {
                var query = "select id from admins";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();
                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();
                var admins = new List<int>();

                while (reader.Read())
                {
                    admins.Add((int)reader[0]);
                }
                conn.Close();

                return admins;
            }

#endregion

#region Rolestrings
            public static Dictionary<Game.Roles, string> ReadRolestrings()
            {
                var query = "select * from roles";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();
                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();
                var rolestring = new Dictionary<Game.Roles, string>();

                while (reader.Read())
                {
                    try
                    {
                        if ((string)reader[0] == "Unknown") rolestring.Add(Game.Roles.Unknown, "Unknown");
                        else rolestring.Add(Game.defaultAliases[(string)reader[0]], ((string)reader[1]).Replace("''", "'"));
                    }
                    catch
                    {
                        SendMessage($"Failed to add rolestring {(string)reader[1]} ({(string)reader[0]})", ludwig);
                    }
                }

                conn.Close();
                return rolestring;
            }
#endregion

#region Bugreports
            public static void AddReport(string bug, int reporter)
            {
                var query = $"insert into bugreports (reportedby, message) values ({reporter}, '{bug.Replace("'", "''")}')";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();
                var comm = new SQLiteCommand(query, conn);
                comm.ExecuteNonQuery();
                conn.Close();
            }

            public static long GetLatestReportId()
            {
                var query = "select seq from sqlite_sequence where name = 'bugreports'";
                var conn = new SQLiteConnection(connectionstring);
                conn.Open();
                var comm = new SQLiteCommand(query, conn);
                var reader = comm.ExecuteReader();

                reader.Read();
                var i = (long)reader[0];
                conn.Close();
                return i;
            }
#endregion
        }
    }
}
