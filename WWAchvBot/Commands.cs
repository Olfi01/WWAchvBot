using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static WWAchvBot.Program;
using static WWAchvBot.Methods;
using System.IO;

namespace WWAchvBot
{
    class Commands
    {
        public static List<int> justCalledStartStop = new List<int>();
        public static DateTime lastping = DateTime.MinValue;


        /// <summary>
        /// Called on commands: /start
        /// </summary>
        public static void Start(Update update, string[] args)
        {
            if (update.Message.Chat.Type != ChatType.Private) return;

            var name = update.Message.From.FirstName + (string.IsNullOrEmpty(update.Message.From.LastName) ? "" : " " + update.Message.From.LastName);
            var username = string.IsNullOrEmpty(update.Message.From.Username) ? "(no username)" : update.Message.From.Username.ToLower();
            var achv = string.Join("|", Newtonsoft.Json.JsonConvert.DeserializeObject<List<WWAchievements>>(GetAchievements(update.Message.From.Id)).Select(x => x.Name)).Replace("'", "''");
            if (!BotUser.users.ContainsKey(update.Message.From.Id))
            {
                var u = update.Message.From;
                var bu = new BotUser(u.Id, FormatHTML(name), username, 0, false, achv);
                BotUser.users.Add(u.Id, bu);
                SQL.ModifyUser("add", bu);
            }
            else
            {
                BotUser.users[update.Message.From.Id].Name = FormatHTML(name);
                BotUser.users[update.Message.From.Id].Username = username;
                BotUser.users[update.Message.From.Id].Achievements = achv.Split('|').Select(x => Game.achv.FirstOrDefault(y => y.Value == x).Key).ToList();
                SQL.ModifyUser("modify", BotUser.users[update.Message.From.Id]);
            }

            List<string> deeplinkstarts = new List<string>() { "subscribe", "unsubscribe" };

            if (string.IsNullOrEmpty(args[1]) || !deeplinkstarts.Contains(args[1])) // normal bot start
            {
                ReplyToMessage("Welcome! This is the manager bot of the @wwachievement group. It's a group where everyone helps each other farming achievements with @werewolfbot. Feel free to join us! :)", update);
            }
            else // started via deep link, e.g. to subscribe to the pinglist
            {
                switch (args[1])
                {
                    case "subscribe":
                        if (!BotUser.users[update.Message.From.Id].Subscribing)
                        {
                            BotUser.users[update.Message.From.Id].Subscribing = true;
                            SQL.ModifyUser("modify", BotUser.users[update.Message.From.Id]);
                            ReplyToMessage("You successfully subscribed to the pinglist! I'll inform you whenever someone triggers the pinglist by \"#ping\"", update);
                            LogToChannel($"{BotUser.users[update.Message.From.Id].Name} (@{BotUser.users[update.Message.From.Id].Username}, <code>{update.Message.From.Id}</code>) just subscribed the #ping list!\n\nNumber of users: {BotUser.users.Count}\nNumber of subs: {BotUser.users.Count(x => x.Value.Subscribing)}\nPercentage of subs: {(BotUser.users.Count(x => x.Value.Subscribing) / BotUser.users.Count) * 100}%");
                        }
                        else ReplyToMessage("You were already subscribing to the pinglist!", update);
                        break;

                    case "unsubscribe":
                        if (BotUser.users[update.Message.From.Id].Subscribing)
                        {
                            BotUser.users[update.Message.From.Id].Subscribing = false;
                            SQL.ModifyUser("modify", BotUser.users[update.Message.From.Id]);
                            ReplyToMessage("You successfully stopped subscribing from the pinglist!", update);
                            LogToChannel($"{BotUser.users[update.Message.From.Id].Name} (@{BotUser.users[update.Message.From.Id].Username}, <code>{update.Message.From.Id}</code>) just unsubscribed the #ping list!\n\nNumber of users: {BotUser.users.Count}\nNumber of subs: {BotUser.users.Count(x => x.Value.Subscribing)}\nPercentage of subs: {(BotUser.users.Count(x => x.Value.Subscribing) / BotUser.users.Count) * 100}%");
                        }
                        else ReplyToMessage("You weren't even subscribing to the pinglist!", update);
                        break;
                }
            }
        }


        /// <summary>
        /// Called on commands: /startgame, /startchaos
        /// </summary>
        public static void StartGame(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (!Game.Games.ContainsKey(chatid))
                {
                    string gamestarttext = "Initializing game . . .";

                    Message msg;

                    if (pinmessages.ContainsKey(chatid))
                    {
                        try
                        {
                            msg = EditMessage(gamestarttext, pinmessages[chatid]);
                            ReplyToMessage($"The new game starts in the pin message! If there is none, please ask an admin for help.", update);
                        }
                        catch
                        {
                            msg = ReplyToMessage(gamestarttext, update);
                            pinmessages.Remove(chatid);
                            SendMessage($"The pinmessage of group {chatid} was removed because it seems it was deleted.", testgroup);
                        }
                    }
                    else msg = ReplyToMessage(gamestarttext, update);

                    if (msg == null) return;
                    Game.Games.Add(chatid, new Game(msg));
                }
                else if (Game.Games[chatid].Gamestate == Game.State.Joining) ReplyToMessage("There was already a game started in here! Join that one!", update);
                else ReplyToMessage("There is already a game running in here, wait for its end before starting a new one!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /addplayer
        /// </summary>
        public static void AddPlayer(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (Game.Games.ContainsKey(chatid))
                {
                    var name = update.Message.From.FirstName + (string.IsNullOrEmpty(update.Message.From.LastName) ? "" : " " + update.Message.From.LastName);
                    var username = string.IsNullOrEmpty(update.Message.From.Username) ? "(no username)" : update.Message.From.Username.ToLower();
                    if (!BotUser.users.ContainsKey(update.Message.From.Id))
                    {
                        var u = update.Message.From;
                        var bu = new BotUser(u.Id, FormatHTML(name), username, 0, false);
                        BotUser.users.Add(u.Id, bu);
                        SQL.ModifyUser("add", bu);
                    }
                    else if (BotUser.users[update.Message.From.Id].Name != name || BotUser.users[update.Message.From.Id].Username.ToLower() != username)
                    {
                        BotUser.users[update.Message.From.Id].Name = FormatHTML(name);
                        BotUser.users[update.Message.From.Id].Username = username;
                        SQL.ModifyUser("modify", BotUser.users[update.Message.From.Id]);
                    }
                    if (update.Message.ReplyToMessage != null && !new[] { Bot.Id, update.Message.From.Id }.Contains(update.Message.ReplyToMessage.From.Id))
                    {
                        name = update.Message.ReplyToMessage.From.FirstName + (string.IsNullOrEmpty(update.Message.ReplyToMessage.From.LastName) ? "" : " " + update.Message.ReplyToMessage.From.LastName);
                        username = string.IsNullOrEmpty(update.Message.ReplyToMessage.From.Username) ? "(no username)" : update.Message.ReplyToMessage.From.Username.ToLower();

                        if (!BotUser.users.ContainsKey(update.Message.ReplyToMessage.From.Id) && update.Message.ReplyToMessage.From.Id != Bot.Id)
                        {
                            var u = update.Message.ReplyToMessage.From;
                            var bu = new BotUser(u.Id, FormatHTML(name), username, 0, false);
                            BotUser.users.Add(u.Id, bu);
                            SQL.ModifyUser("add", bu);
                        }
                        else if (BotUser.users[update.Message.ReplyToMessage.From.Id].Name != name || BotUser.users[update.Message.ReplyToMessage.From.Id].Username.ToLower() != username)
                        {
                            BotUser.users[update.Message.ReplyToMessage.From.Id].Name = FormatHTML(name);
                            BotUser.users[update.Message.ReplyToMessage.From.Id].Username = username;
                            SQL.ModifyUser("modify", BotUser.users[update.Message.ReplyToMessage.From.Id]);
                        }
                    }
                    if (!string.IsNullOrEmpty(args[1]))
                    {
                        if (int.TryParse(args[1].Split(' ')[0], out int id))
                        {
                            if (!wwbots.Contains(id) && Bot.Id != id)
                            {
                                try
                                {
                                    var u = ResolveUser(id, update.Message.Chat.Id);
                                    name = u.FirstName + (string.IsNullOrEmpty(u.LastName) ? "" : " " + u.LastName);
                                    username = string.IsNullOrEmpty(u.Username) ? "(no username)" : u.Username.ToLower();

                                    if (BotUser.users.ContainsKey(id))
                                    {
                                        BotUser.users[id].Name = FormatHTML(name);
                                        BotUser.users[id].Username = username;

                                        SQL.ModifyUser("modify", BotUser.users[id]);
                                    }
                                    else
                                    {
                                        var bu = new BotUser(u.Id, FormatHTML(name), username, 0, false);
                                        BotUser.users.Add(u.Id, bu);
                                        SQL.ModifyUser("add", bu);
                                    }
                                }
                                catch { }
                            }
                        }
                        else if (args[1].Split(' ')[0].StartsWith("@") && BotUser.users.Any(x => x.Value.Username == args[1].Split(' ')[0].Remove(0, 1).ToLower()))
                        {
                            try
                            {
                                var u = ResolveUser(id, update.Message.Chat.Id);
                                name = u.FirstName + (string.IsNullOrEmpty(u.LastName) ? "" : " " + u.LastName);
                                username = string.IsNullOrEmpty(u.Username) ? "(no username)" : u.Username.ToLower();
                                BotUser.users[id].Name = FormatHTML(name);
                                BotUser.users[id].Username = username;
                                SQL.ModifyUser("modify", BotUser.users[id]);
                            }
                            catch { }
                        }
                    }

                    BotUser newplayer = GetUser(update, args);
                    if (!Game.Games[chatid].players.Any(x => x.Value.Id == newplayer.Id))
                    {
                        if (Game.Games[chatid].AddPlayer(newplayer.Id)) ReplyToMessage($"{newplayer.LinkedName} was successfully added to the game!", update);
                        else ReplyToMessage($"Failed to add {newplayer.LinkedName} to the players.", update);
                    }
                    else ReplyToMessage($"It seems {newplayer.LinkedName} had already joined the game!", update);
                }
                else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /stopgame
        /// </summary>
        public static void StopGame(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;
                if (Game.Games.ContainsKey(chatid))
                {
                    if (justCalledStartStop.Contains(update.Message.From.Id))
                    {
                        justCalledStartStop.Remove(update.Message.From.Id);
                        Game.Games[chatid].Stop();
                        Game.Games.Remove(chatid);
                        ReplyToMessage($"<b>{update.Message.From.FirstName}</b> has considered the game stopped!", update);
                    }
                    else
                    {
                        justCalledStartStop.Add(update.Message.From.Id);
                        ReplyToMessage($"Use this command again if you want to stop the game.", update);
                        Timer t = new Timer(new TimerCallback(delegate { try { justCalledStartStop.Remove(update.Message.From.Id); } catch { } }), null, 10000, Timeout.Infinite);
                    }
                }
                else ReplyToMessage("It seems there isn't even a game running in here!", update);
            }
        }


        /// <summary>
        /// Called on commands: /flee, /dead
        /// </summary>
        public static void FleePlayer(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (Game.Games.ContainsKey(chatid))
                {
                    BotUser oldplayer = GetUser(update, args);

                    if (Game.Games[chatid].players.ContainsKey(oldplayer.Id))
                    {
                        if (Game.Games[chatid].Gamestate == Game.State.Joining)
                        {
                            if (Game.Games[chatid].RemovePlayer(oldplayer.Id)) ReplyToMessage($"{oldplayer.LinkedName} was successfully removed from the game!", update);
                            else ReplyToMessage($"Failed to remove {oldplayer.LinkedName} from the game.", update);
                        }
                        else if (Game.Games[chatid].players[oldplayer.Id].Alive)
                        {
                            Game.Games[chatid].players[oldplayer.Id].Alive = false;
                            Game.Games[chatid].UpdatePlayerlist();
                            ReplyToMessage($"{oldplayer.LinkedName} was marked as dead.", update);
                        }
                        else ReplyToMessage($"It seems {oldplayer.LinkedName} was already dead!", update);
                    }
                    else ReplyToMessage($"It seems {oldplayer.LinkedName} hadn't even joined the game!", update);
                }
                else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /listachv
        /// </summary>
        public static void ListAchievements(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (Game.Games.ContainsKey(chatid))
                {
                    if (Game.Games[chatid].Gamestate == Game.State.Running)
                    {
                        Game g = Game.Games[chatid];

                        List<string> Reply = new List<string>();
                        string possible = "<b>POSSIBLE ACHIEVEMENTS:</b>\n";
                        g.CalculateAchvInfo();

                        foreach (var player in g.players.Values.Where(x => x.Alive))
                        {
                            possible += "\n" + player.Name + "\n";

                            foreach (var achv in Game.achv.Keys)
                            {
                                possible += Game.Games[chatid].IsAchievable(achv, player)
                                    ? " - " + Game.achv[achv] + "\n"
                                    : "";
                            }

                            if (possible.Length >= 1500)
                            {
                                Reply.Add(possible);
                                possible = "";
                            }
                        }
                        if (possible.Length >= 1) Reply.Add(possible);
                        foreach (var r in Reply) ReplyToMessage(r, update);
                    }
                    else ReplyToMessage("This command can only be used after the game was started!", update);
                }
                else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /love
        /// </summary>
        public static void ToggleLoveStatus(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (Game.Games.ContainsKey(chatid))
                {
                    if (Game.Games[chatid].Gamestate == Game.State.Running)
                    {
                        BotUser lover = GetUser(update, args);

                        Game.Games[chatid].players[lover.Id].Love = !Game.Games[chatid].players[lover.Id].Love;
                        ReplyToMessage($"The love status of {lover.LinkedName} was updated.", update);
                        Game.Games[chatid].UpdatePlayerlist();
                    }
                    else ReplyToMessage("This command can only be used after the game started!", update);
                }
                else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /getpin
        /// </summary>
        public static void GetPin(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                if (pinmessages.ContainsKey(update.Message.Chat.Id))
                {
                    ReplyToMessage("Here is the pin message.", update.Message.Chat.Id, pinmessages[update.Message.Chat.Id].MessageId);
                }
                else if (Game.Games.ContainsKey(update.Message.Chat.Id))
                {
                    ReplyToMessage("Here is the game message.", update.Message.Chat.Id, Game.Games[update.Message.Chat.Id].Pinmessage.MessageId);
                }
                else ReplyToMessage("There is no pin message!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /lynchorder
        /// </summary>
        public static void GetLynchorder(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;
                string order = "";

                if (Game.Games.ContainsKey(chatid))
                {
                    if (Game.Games[chatid].Gamestate == Game.State.Running)
                    {
                        var defaultorder = string.Join("\n", Game.Games[chatid].players.Values.Where(x => x.Alive).Select(x => x.Name)) + "\n" + Game.Games[chatid].players.Values.Where(x => x.Alive).Select(x => x.Name).First();

                        if (!string.IsNullOrEmpty(Game.Games[chatid].Lynchorder))
                        {
                            order = FormatHTML(Game.Games[chatid].Lynchorder.Replace("<-->", "↔️").Replace("<->", "↔️").Replace("<>", "↔️").Replace("-->", "➡️").Replace("->", "➡️").Replace(">", "➡️"));
                            order = order.Replace("$lynchorder", defaultorder);
                        }
                        else order = defaultorder;

                        order = "<b>Lynchorder</b>\n" + order;

                        ReplyToMessage(order, update);
                    }
                    else ReplyToMessage("There is only a lynch order when the game is running!", update);
                }
                else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /setlynchorder
        /// </summary>
        public static void SetLynchorder(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (Game.Games.ContainsKey(chatid))
                {
                    if (Game.Games[chatid].Gamestate == Game.State.Running)
                    {
                        if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.Type == MessageType.TextMessage)
                        {
                            if (update.Message.ReplyToMessage.From.Id == Bot.Id)
                            {
                                Game.Games[chatid].Lynchorder = "";
                                ReplyToMessage($"The lynchorder was reset by <b>{update.Message.From.FirstName}</b>", update);
                                return;
                            }
                            else
                            {
                                Game.Games[chatid].Lynchorder = update.Message.ReplyToMessage.Text ?? "";
                            }
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(args[1]))
                            {
                                Game.Games[chatid].Lynchorder = "";
                                ReplyToMessage($"The lynchorder was reset by <b>{update.Message.From.FirstName}</b>", update);
                                return;
                            }
                            else
                            {
                                Game.Games[chatid].Lynchorder = args[1];
                            }
                        }
                        ReplyToMessage($"The lynchorder was set by <b>{update.Message.From.FirstName}</b>. Get it with the /lynchorder command!", update);
                    }
                    else ReplyToMessage("There is only a lynch order when the game is running!", update);
                }
                else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /resetlynchorder
        /// </summary>
        public static void ResetLynchorder(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (Game.Games.ContainsKey(chatid))
                {
                    if (Game.Games[chatid].Gamestate == Game.State.Running)
                    {
                        Game.Games[chatid].Lynchorder = "";
                        ReplyToMessage($"The lynchorder was reset by <b>{update.Message.From.FirstName}</b>", update);
                    }
                    else ReplyToMessage("There is only a lynch order when the game is running!", update);
                }
                else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: #ping
        /// </summary>
        public static void SummonPinglist(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;
                var difference = DateTime.UtcNow - lastping;

                if (difference.TotalMinutes >= 10)
                {
                    if (Game.Games.ContainsKey(chatid))
                    {
                        if (Game.Games[chatid].Gamestate == Game.State.Joining)
                        {
                            string group = update.Message.Chat.Id == achvgroup ? $"<a href=\"{achvgrouplink}\">{update.Message.Chat.Title}</a>" : $"<b>{update.Message.Chat.Title}</b>";

                            foreach (var u in BotUser.users.Values.Where(x => x.Subscribing && !Game.Games[chatid].players.Values.Select(y => y.Id).Contains(x.Id)))
                            {
                                try
                                {

                                    SendMessage($"<b>🔔 Ping! 🔔</b>\nAchievement hunters are called in {group}!", u.Id);
                                }
                                catch (Exception e)
                                {
                                    if (e is AggregateException && ((AggregateException)e).InnerExceptions.Any(x => x.Message.ToLower().Contains("forbidden: bot was blocked by the user") || x.Message.ToLower().Contains("forbidden: bot can't initiate conversation with a user")))
                                    {
                                        BotUser.users[u.Id].Subscribing = false;
                                        SQL.ModifyUser("modify", BotUser.users[u.Id]);
                                        LogToChannel($"{u.Name} (@{u.Username}, <code>{u.Id}</code>) was automatically unsubscribed from the #ping list as they have blocked / not started the bot.\n\nNumber of users: {BotUser.users.Count}\nNumber of subs: {BotUser.users.Count(x => x.Value.Subscribing)}");
                                    }
                                    else SendError(e, update.Message.Chat.Id, false);
                                }
                            }

                            ReplyToMessage("<b>🔔 Ping! 🔔</b>\n\nAchievement hunters are called!\n\nIf you want to be notified by this command, use the subscribe button below! To no longer be notified, use the unsubscribe button. You will be sent to our private chat, where you need to start me, and we are done :D\n\n<b>Have fun hunting achievements!</b>", update, InlineKeyboards.Subscribe);
                            lastping = DateTime.UtcNow;
                        }
                        else ReplyToMessage("<b>You can only ping people while a game is in joining phase!</b>\n\nIf you want to be notified by this command, use the subscribe button below! To no longer be notified, use the unsubscribe button. You will be sent to our private chat, where you need to start me, and we are done :D\n\n<b>Have fun hunting achievements!</b>", update, InlineKeyboards.Subscribe);
                    }
                    else ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!\n\nIf you want to be notified by this command, use the subscribe button below! To no longer be notified, use the unsubscribe button. You will be sent to our private chat, where you need to start me, and we are done :D\n\n<b>Have fun hunting achievements!</b>", update, InlineKeyboards.Subscribe);
                }
                else ReplyToMessage($"You can only ping once per 10 minutes! You need to wait {string.Format("{0:mm\\:ss}", (TimeSpan.FromMinutes(10) - difference))} more minutes to use the ping list again!\n\nIf you want to be notified by this command, use the subscribe button below! To no longer be notified, use the unsubscribe button. You will be sent to our private chat, where you need to start me, and we are done :D\n\n<b>Have fun hunting achievements!</b>", update, InlineKeyboards.Subscribe);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on commands: /ping
        /// </summary>
        public static void Ping(Update update, string[] args)
        {
            /*
            var recievetime = DateTime.UtcNow - update.Message.Date;
            var text = $"<b>Time to receive</b>: {recievetime.ToString("mm:ss")}";
            var msg = ReplyToMessage(text, update);
            var sendtime = DateTime.Now - msg.Date;
            EditMessage(text + $"\n<b>Time to send</b>: {sendtime.ToString("mm:ss")}", msg);
            */
            ReplyToMessage("<b>I heard a sound, such like a \"ping\"!</b>", update);
        }


        /// <summary>
        /// Called on commands: /version
        /// </summary>
        public static void GetVersion(Update update, string[] args)
        {
            ReplyToMessage($"Running version: {version}", update);
        }


        /// <summary>
        /// Called on commands: /listalias
        /// </summary>
        public static void ListAlias(Update update, string[] args)
        {
            string list = "<b>ALL ALIASES OF ALL ROLES:</b>\n\n";

            foreach (var role in Game.rolestring.Keys.Where(x => x != Game.Roles.Unknown))
            {
                list += $"<b>{Game.rolestring[role]}</b>\n" + string.Join("\n", Game.roleAliases.Where(x => x.Value == role).Select(e => e.Key)) + "\n\n";
            }
            ReplyToMessage(list, update);
        }


        /// <summary>
        /// Called on commands: /runinfo
        /// </summary>
        public static void RunInfo(Update update, string[] args)
        {
            string infomessage = "<b>RUNTIME INFO:</b>\n";
            infomessage += "Running for: <b>" + (DateTime.UtcNow - starttime).ToString().Remove((DateTime.UtcNow - starttime).ToString().LastIndexOf('.') + 2) + "</b>\n";
            infomessage += "Running games: <b>" + Game.Games.Count + "</b>\n";
            ReplyToMessage(infomessage, update);
        }


        /// <summary>
        /// Called on commands: /listcommands
        /// </summary>
        public static void ListCommands(Update update, string[] args)
        {
            if (devs.Contains(update.Message.From.Id))
            {
                List<string> commands = Program.commands.Keys.Select(x => x.Replace("/", "")).ToList();
                List<string> admincommands = Program.admincommands.Keys.Select(x => x.Replace("/", "")).ToList();
                List<string> devcommands = Program.devcommands.Keys.Select(x => x.Replace("/", "")).ToList();

                ReplyToMessage("<b>User commands:</b>\n" + string.Join("\n", commands) + "\n\n\n<b>Admin commands:</b>\n" + string.Join("\n", admincommands) + "\n\n\n<b>Dev commands:</b>\n" + string.Join("\n", devcommands), update);
            }
            if (adminIds.Contains(update.Message.From.Id))
            {
                List<string> commands = Program.commands.Keys.Select(x => x.Replace("/", "")).ToList();
                List<string> admincommands = Program.admincommands.Keys.Select(x => x.Replace("/", "")).ToList();

                ReplyToMessage("<b>User commands:</b>\n" + string.Join("\n", commands) + "\n\n\n<b>Admin commands:</b>\n" + string.Join("\n", admincommands), update);
            }
            else
            {
                List<string> commands = Program.commands.Keys.Select(x => x.Replace("/", "")).ToList();
                ReplyToMessage("<b>User commands:</b>\n" + string.Join("\n", commands), update);
            }
        }


        /// <summary>
        /// Called on commands: /reportbug
        /// </summary>
        public static void ReportBug(Update update, string[] args)
        {
            if (string.IsNullOrEmpty(args[1]))
            {
                ReplyToMessage("Please add the bug you want to report after the command.", update);
                return;
            }

            try
            {
                SQL.AddReport(args[1], update.Message.From.Id);
            }
            catch
            {
                ReplyToMessage("Failed reporting. Did you already report this bug? If you did not, and this error keeps happening, inform @Olgabrezel.", update);
                return;
            }
            var id = SQL.GetLatestReportId();
            ReplyToMessage("Your bug was reported to Ludwig. Bug ID: #b" + id, update);
            SendMessage($"<b>New</b> #AchvBug<b>!</b>\n\n<b>Reported by:</b> {FormatHTML(update.Message.From.FirstName)} ({update.Message.From.Id}, @{update.Message.From.Username})\n\n<b>Bug ID:</b> #b{id}\n\n<b>Message:</b>\n {args[1]}", testgroup);
        }

        /// <summary>
        /// Called on assigning roles.
        /// </summary>
        public static void AssignRole(Update update, string[] args, bool forcechange = false)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                var chatid = update.Message.Chat.Id;

                if (Game.Games.ContainsKey(chatid))
                {
                    if (Game.Games[chatid].Gamestate == Game.State.Running)
                    {
                        var u = GetUser(update, args);
                        if (!Game.Games[chatid].players.Keys.Contains(u.Id) || (Game.Games[chatid].players[u.Id].Role != Game.Roles.Unknown && !forcechange)) return;

                        var role = Game.roleAliases[args[0].ToLower()];

                        if (!Game.Games[chatid].players.Any(x => x.Value.Id == u.Id)) return;

                        Game.Games[chatid].players[u.Id].Role = role;
                        Game.Games[chatid].players[u.Id].Alive = true;
                        Game.Games[chatid].CalculateAchvInfo();
                        Game.Games[chatid].UpdatePlayerlist();
                        ReplyToMessage($"{u.LinkedName}'s role was successfully set to <b>{Game.rolestring[role]}</b>.", update);
                    }
                }
            }
        }


        /// <summary>
        /// Called on admin commands: /maint
        /// </summary>
        public static void ToggleMaintenance(Update update, string[] args)
        {
            maintenance = !maintenance;

            if (maintenance)
            {
                if (!Game.Games.Any(x => x.Value.Pinmessage.Chat.Id == achvgroup)) SendMessage("<b>The bot is now going down for maintenance!</b>", achvgroup);
                List<long> ids = new List<long>();
                foreach (var g in Game.Games.Values.Where(x => x.Gamestate == Game.State.Joining))
                {
                    Game.Games[g.Pinmessage.Chat.Id].Stop();
                    ids.Add(g.Pinmessage.Chat.Id);
                    SendMessage("<b>This game was aborted as the bot is going down for maintenance!</b>", g.Pinmessage.Chat.Id);
                }
                foreach (var id in ids)
                {
                    Game.Games.Remove(id);
                }
                foreach (var g in Game.Games.Values.Where(x => x.Gamestate == Game.State.Running))
                {
                    SendMessage("<b>After this game, the bot is going down for maintenance!</b>", g.Pinmessage.Chat.Id);
                }
            }
            else
            {
                SendMessage("<b>The bot is no longer under maintenance! You can now play games!</b>", achvgroup);
            }
            if (maintenance)
            {
                if (Game.Games.Count == 0)
                {
                    ReplyToMessage("The maintenance mode was <b>enabled</b>, there are no more games running. What would you like to do?", update, InlineKeyboards.Update);
                }
                else
                {
                    ReplyToMessage("The maintenance mode was <b>enabled</b>. There are <b>" + Game.Games.Count + "</b> games still running.", update);
                }
            }
            else
            {
                ReplyToMessage("The maintenance mode was <b>disabled</b>!", update);
            }
        }


        /// <summary>
        /// Called on admin commands: /genpin
        /// </summary>
        public static void GenPin(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                if (pinmessages.ContainsKey(update.Message.Chat.Id)) pinmessages.Remove(update.Message.Chat.Id);
                pinmessages.Add(update.Message.Chat.Id, SendMessage($"This is a pinmessage generated by <b>{update.Message.From.FirstName}</b>", update.Message.Chat.Id));
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on admin commands: /setpin
        /// </summary>
        public static void SetPin(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                if (update.Message.ReplyToMessage != null && update.Message.ReplyToMessage.From.Id == Bot.Id)
                {
                    if (pinmessages.ContainsKey(update.Message.Chat.Id)) pinmessages.Remove(update.Message.Chat.Id);
                    pinmessages.Add(update.Message.Chat.Id, update.Message.ReplyToMessage);
                    EditMessage($"This is a pinmessage generated by <b>{update.Message.From.FirstName}</b>", update.Message.ReplyToMessage);
                    ReplyToMessage("That message was successfully set as pin message!", update);
                }
                else ReplyToMessage("You need to reply to a message of mine!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on admin commands: /delpin
        /// </summary>
        public static void DelPin(Update update, string[] args)
        {
            if (update.Message.Chat.Type == ChatType.Supergroup)
            {
                if (pinmessages.ContainsKey(update.Message.Chat.Id))
                {
                    pinmessages.Remove(update.Message.Chat.Id);
                    ReplyToMessage("The pinmessage was successfully deleted!", update);
                }
                else ReplyToMessage("There isn't even a pinmessage in here!", update);
            }
            else ReplyToMessage("This command can only be used in groups.", update);
        }


        /// <summary>
        /// Called on admin commands: /send
        /// </summary>
        public static void Send(Update update, string[] args)
        {
            if (!string.IsNullOrEmpty(args[1]) && args[1].Count(x => x == ' ') >= 1)
            {
                string idS = args[1].Split(' ')[0];
                string text = args[1].Remove(0, args[1].IndexOf(' '));

                if (long.TryParse(idS, out long id))
                {
                    switch (id)
                    {
                        case 0:
                            id = testgroup;
                            break;

                        case 1:
                            id = achvgroup;
                            break;
                    }

                    try
                    {
                        SendMessage(text, id);
                        ReplyToMessage("Successfully sent!", update);
                    }
                    catch (Exception e)
                    {
                        ReplyToMessage("Send unsuccessful!", update);
                        SendError(e, id, false);
                    }
                }
                else if (BotUser.users.Values.Any(x => '@' + x.Username == idS.ToLower()))
                {
                    int uid = BotUser.users.Values.First(x => '@' + x.Username == idS.ToLower()).Id;

                    try
                    {
                        SendMessage(text, uid);
                        ReplyToMessage("Successfully sent!", update);
                    }
                    catch (Exception e)
                    {
                        ReplyToMessage("Send unsuccessful!", update);
                        SendError(e, uid, false);
                    }
                }
                else ReplyToMessage("Syntax error!\n\nEither you didn't specify ID or username, or I don't know any user with that username.\n\nSyntax: /send [id] [text]\n\nwhere id can be:\n - 0 - Testgroup\n - 1 - Achv Group\n - Any group's/user's ID\n - Any user's username which I know", update);
            }
            else ReplyToMessage("Syntax error!\n\nEither you didn't specify ID or username, or I don't know any user with that username.\n\nSyntax: /send [id] [text]\n\nwhere id can be:\n - 0 - Testgroup\n - 1 - Achv Group\n - Any group's/user's ID\n - Any user's username which I know", update);
        }


        /// <summary>
        /// Called on admin commands: /addalias
        /// </summary>
        public static void AddAlias(Update update, string[] args)
        {
            if (!string.IsNullOrEmpty(args[1]) && args[1].Count(x => x == ' ') == 1 && Game.roleAliases.ContainsKey(args[1].Split(' ')[1]))
            {
                string currentalias = args[1].Split(' ')[1];
                string newalias = args[1].Split(' ')[0];
                Game.Roles newrole = Game.roleAliases[currentalias];

                if (Game.roleAliases.ContainsKey(newalias))
                {
                    Game.roleAliases[newalias] = newrole;
                    SQL.ModifyAliases("modify", newalias, Game.defaultAliases.First(x => x.Value == newrole).Key);
                    ReplyToMessage($"Alias <b>{newalias}</b> was successfully updated to role <i>{Game.rolestring[newrole]}</i>.", update);
                }
                else
                {
                    Game.roleAliases.Add(newalias, newrole);
                    SQL.ModifyAliases("add", newalias, Game.defaultAliases.First(x => x.Value == newrole).Key);
                    ReplyToMessage($"Alias <b>{newalias}</b> was successfully added for role <i>{Game.rolestring[newrole]}</i>.", update);
                }
            }
            else ReplyToMessage("Syntax error!\n\nEither you didn't specify new alias and role, or you misspelled the role!\n\nSyntax: /addalias [new alias] [already existing alias]", update);
        }


        /// <summary>
        /// Called on admin commands: /delalias
        /// </summary>
        public static void DelAlias(Update update, string[] args)
        {
            if (!string.IsNullOrEmpty(args[1]) && Game.roleAliases.ContainsKey(args[1]))
            {
                string oldrole = Game.rolestring[Game.roleAliases[args[1]]];
                Game.roleAliases.Remove(args[1]);
                SQL.ModifyAliases("delete", args[1], null);
                ReplyToMessage($"Alias <b>{args[1]}</b> was successfully deleted from role {oldrole}!", update);
            }
        }


        /// <summary>
        /// Called on admin commands: /userinfo
        /// </summary>
        public static void UserInfo(Update update, string[] args)
        {
            var user = GetUser(update, args);
            string status = "Not in Group";
            if (adminIds.Contains(user.Id)) status = "Bot Admin";
            else if (IsGroupAdmin(user.Id, achvgroup)) status = "Group Admin";
            else if (IsGroupMember(user.Id, achvgroup)) status = "Group Member";

            ReplyToMessage($"{user.LinkedName}\n - @{user.Username}\n - {user.Id}\n - Subscribing: " + (user.Subscribing ? "✅" : "❌") + $"\n - Games played: {user.Gamecount}\n - Status: {status}", update);
        }


        /// <summary>
        /// Called on admin commands: /mostactive
        /// </summary>
        public static void MostActive(Update update, string[] args)
        {
            int number = 10;
            if (!string.IsNullOrEmpty(args[1])) int.TryParse(args[1], out number);

            var active = BotUser.users.Values.OrderByDescending(x => x.Gamecount).ToList().GetRange(0, number);

            string activity = $"<b>Top {number} active players:</b>\n\n";
            foreach (var a in active) activity += a.Gamecount + ": " + a.LinkedName + "\n";
            ReplyToMessage(activity, update);
        }


        /// <summary>
        /// Called on admin commands: /leave
        /// </summary>
        public static void Leave(Update update, string[] args)
        {
            long chatID;
            if (!string.IsNullOrEmpty(args[1]) && long.TryParse(args[1], out long chatid))
            {
                chatID = chatid;
            }
            else chatID = update.Message.Chat.Id;

            ReplyToMessage($"Leaving chat {chatID}", update);
            client.LeaveChatAsync(chatID).Wait();
        }


        /// <summary>
        /// Called on admin commands: /shutdown
        /// </summary>
        public static void ShutDown(Update update, string[] args)
        {
            if (!maintenance || Game.Games.Count != 0)
            {
                ReplyToMessage("You can only shut down if maintenance is enabled and no games are running anymore.", update);
                return;
            }

            startuptxt = "<b>Shutting down...</b>\n";
            startup = SendMessage(startuptxt, testgroup);

            DateTime endtime = DateTime.UtcNow;
            startuptxt += $"Bot stopped at \n<code>{endtime.ToString("dd.MM.yyyy HH:mm:ss")} UTC</code>\n\n<b>Shutdown complete.</b>";
            EditMessage(startuptxt, startup);
            running = false;
        }


        /// <summary>
        /// Called on admin commands: /sqlite
        /// </summary>
        public static void RunSQLCommand(Update update, string[] args)
        {
            if (string.IsNullOrEmpty(args[1]))
            {
                ReplyToMessage("You need to enter a query...", update);
                return;
            }
            else if (update.Message.From.Id != ludwig && !args[1].ToLower().StartsWith("select") && !args[1].ToLower().StartsWith("pragma"))
            {
                ReplyToMessage("You are not allowed to run queries other than <b>SELECT</b> and <b>PRAGMA</b>", update);
                return;
            }

            try
            {
                var conn = new SQLiteConnection(connectionstring);

                string raw = "";

                var queries = args[1].Split(';');
                var reply = "";
                foreach (var sql in queries)
                {
                    conn.Open();

                    using (var comm = conn.CreateCommand())
                    {
                        comm.CommandText = sql;
                        var reader = comm.ExecuteReader();
                        var result = "";
                        if (reader.HasRows)
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                                raw += reader.GetName(i) + (i == reader.FieldCount - 1 ? "" : " - ");
                            result += raw + Environment.NewLine;
                            raw = "";
                            while (reader.Read())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                    raw += (reader.IsDBNull(i) ? "<i>NULL</i>" : reader[i]) + (i == reader.FieldCount - 1 ? "" : " - ");
                                result += raw + Environment.NewLine;
                                raw = "";
                            }
                        }
                        if (reader.RecordsAffected > 0) result += $"\n<i>{reader.RecordsAffected} record(s) affected.</i>";
                        else if (string.IsNullOrEmpty(result)) result = sql.ToLower().StartsWith("select") || sql.ToLower().StartsWith("update") || sql.ToLower().StartsWith("pragma") ? "<i>Nothing found.</i>" : "<i>Done.</i>";
                        reply += result + "\n\n";
                        conn.Close();
                    }
                }
                ReplyToMessage(reply, update);

                if (new[] { "delete from users", "update users", "insert into users" }.Any(x => args[1].ToLower().StartsWith(x))) BotUser.users = SQL.ReadUsers();
                if (new[] { "delete from aliases", "update aliases", "insert into aliases" }.Any(x => args[1].ToLower().StartsWith(x))) Game.roleAliases = SQL.ReadAliases();
                if (new[] { "delete from admins", "update admins", "insert into admins" }.Any(x => args[1].ToLower().StartsWith(x))) Program.adminIds = SQL.ReadAdmins();
                if (new[] { "delete from roles", "update roles", "insert into roles" }.Any(x => args[1].ToLower().StartsWith(x))) Game.rolestring = SQL.ReadRolestrings();
                if (args[1].ToLower().StartsWith("update bot set achvlink")) achvgrouplink = SQL.ReadAchvLink();
                if (args[1].ToLower().StartsWith("update bot set achvid")) achvgroup = SQL.ReadAchvId();
                if (args[1].ToLower().StartsWith("update bot set testid")) testgroup = SQL.ReadTestId();
                if (args[1].ToLower().StartsWith("update bot set logchannel")) logchannel = SQL.ReadLogId();
                if (args[1].ToLower().StartsWith("update bot set token")) ReplyToMessage("The token was just changed in the database! If you restart the program, the new token, and as such the new bot, will be used :)", update);
            }
            catch (SQLiteException sqle)
            {
                Exception e = sqle;
                while (e.InnerException != null) e = e.InnerException;

                ReplyToMessage("<b>SQLite Error!</b>\n\n" + e.Message, update);
            }
            catch (Exception e)
            {
                SendError(e, update.Message.Chat.Id, false);
            }
        }


        /// <summary>
        /// Called on admin commands: /reboot
        /// </summary>
        public static void Restart(Update update, string[] args)
        {
            if (!maintenance || Game.Games.Count != 0)
            {
                ReplyToMessage("To restart the bot, maintenance must be enabled and there may no games be running anymore!", update);
                return;
            }

            ReplyToMessage("You used the reboot command. What would you like to do?", update, InlineKeyboards.Update);
        }


        /// <summary>
        /// Called on admin commands: /supresserrors
        /// </summary>
        public static void SupressErrors(Update update, string[] args)
        {
            errorsupression = !errorsupression;
            ReplyToMessage("Error supression: <code>" + errorsupression + "</code>", update);
        }

        /// <summary>
        /// Called on admin commands: /test
        /// </summary>
        public static void Test(Update update, string[] args)
        {
#if RELEASE
            ReplyToMessage("Nothing to test on release!", update);
            return;
#endif
        }

        /// <summary>
        /// Called on admin commands: /backup
        /// </summary>
        public static void SendDBBackup(Update update, string[] args)
        {
            SendBackup(update.Message.Chat.Id);
            ReplyToMessage("Here is a current backup!", update);
        }

        /// <summary>
        /// Called on admin commands: /usebackup
        /// </summary>
        public static void UseDBBackup(Update update, string[] args)
        {
            if (update.Message.ReplyToMessage == null || update.Message.ReplyToMessage.Type != MessageType.DocumentMessage || update.Message.ReplyToMessage.Document.FileName != "AchvBot.sqlite")
            {
                ReplyToMessage("You need to reply to a database backup of mine!", update);
                return;
            }

            var fs = System.IO.File.Create("C:\\Olgabrezel\\AchvBot.sqlite.new");
            var t = client.GetFileAsync(update.Message.ReplyToMessage.Document.FileId);
            t.Wait();
            t.Result.FileStream.CopyTo(fs);
            fs.Close();
            ReplyToMessage("The backup was copied to the destination folder. It will be used on the next /reboot.", update);
        }

        /// <summary>
        /// Called on admin commands: /unusebackup
        /// </summary>
        public static void UnuseDBBackup(Update update, string[] args)
        {
            if (!System.IO.File.Exists("C:\\Olgabrezel\\AchvBot.sqlite.new"))
            {
                ReplyToMessage("There wasn't even a backup to be used!", update);
                return;
            }

            System.IO.File.Delete("C:\\Olgabrezel\\AchvBot.sqlite.new");
            ReplyToMessage("The backup file won't be used on the next reboot anymore.", update);
        }
    }
}
