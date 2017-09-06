using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace WWAchvBot
{
    class Program
    {
        public static string token;
        public const string connectionstring = "Data Source=C:\\Olgabrezel\\AchvBot.sqlite;Version=3;";

        public const string gitPullFile = "C:\\Olgabrezel\\PullAchvGit.bat";
        public const string nugetRestoreFile = "C:\\Olgabrezel\\RestoreAchvNuget.bat";
        public const string buildFile = "C:\\Olgabrezel\\BuildAchvDevenv.bat";
        public const string sourceReleasePath = "C:\\Users\\Flom\\Desktop\\AchvBot\\WWAchvBot\\WWAchvBot\\bin\\Release";
        public const string destinationReleasePath = "C:\\Users\\Flom\\Desktop\\AchvBot\\Running\\";

        public static string NewExeToStart = null;

        public static User Bot;
        public static readonly int[] wwbots = new[] { 175844556, 198626752 };

        public const int ludwig = 295152997;
        public const int florian = 267376056;
        public static readonly int[] devs = { ludwig, florian };
        public static List<int> adminIds;

        public static long achvgroup;
        public static long testgroup;
        public static long logchannel;
        public static string achvgrouplink;


        public static List<long> allowedgroups;

        public static Dictionary<string, MethodInfo> devcommands = new Dictionary<string, MethodInfo>();
        public static Dictionary<string, MethodInfo> admincommands = new Dictionary<string, MethodInfo>();
        public static Dictionary<string, MethodInfo> commands = new Dictionary<string, MethodInfo>();
        public static Dictionary<string, MethodInfo> callbacks = new Dictionary<string, MethodInfo>();

        public static readonly Thread GameClearer = new Thread(Methods.ClearGames);

        public static string startuptxt = "<b>Starting up...</b>\n";
        public static Message startup;

        public static Message LatestBackup;

        public static ITelegramBotClient client;
        public static bool running = true;
        public static bool maintenance = true;
        public static bool errorsupression = false;
        public const string version = "1.2.6a";

        public static Dictionary<long, Message> Pinmessages = new Dictionary<long, Message>();

        static void Main(string[] args)
        {
            System.Threading.Thread.Sleep(1000); // give the old version a second to shut down

            if (System.IO.File.Exists("C:\\Olgabrezel\\AchvBot.sqlite.new"))
            {
                System.IO.File.Delete("C:\\Olgabrezel\\AchvBot.sqlite");
                System.IO.File.Copy("C:\\Olgabrezel\\AchvBot.sqlite.new", "C:\\Olgabrezel\\AchvBot.sqlite");
                System.IO.File.Delete("C:\\Olgabrezel\\AchvBot.sqlite.new");
                startuptxt += "\n<b>Found a new database file and used it!</b>\n\n";
            }

            token = Methods.SQL.ReadToken();
            adminIds = Methods.SQL.ReadAdmins();
            achvgroup = Methods.SQL.ReadAchvId();
            testgroup = Methods.SQL.ReadTestId();
            logchannel = Methods.SQL.ReadLogId();
            achvgrouplink = Methods.SQL.ReadAchvLink();
            allowedgroups = new List<long>() { testgroup, achvgroup, logchannel };

            client = new TelegramBotClient(token);

            startup = Methods.SendMessage(startuptxt, testgroup);


            AssignCommands();
            startuptxt += "The commands were read in!\n";
            Methods.EditMessage(startuptxt, startup);

            try
            {
                BotUser.users = Methods.SQL.ReadUsers();
                startuptxt += "The users were read in!\n";
            }
            catch (Exception e)
            {
                BotUser.users = new Dictionary<int, BotUser>();
                startuptxt += "The users were <b>not read in</b> so they were set to an empty dict.\n";
                Methods.SendMessage(e.Message, ludwig, parseMode: ParseMode.Default);
            }
            Methods.EditMessage(startuptxt, startup);

            try
            {
                Game.roleAliases = Methods.SQL.ReadAliases();
                startuptxt += "The aliases were read in!\n";
            }
            catch
            {
                Game.roleAliases = new Dictionary<string, Game.Roles>();
                startuptxt += "The aliases were <b>not read in</b> so they were set to an empty dict.\n";
            }
            Methods.EditMessage(startuptxt, startup);

            try
            {
                Game.rolestring = Methods.SQL.ReadRolestrings();
                startuptxt += "The rolestrings were read in!\n";
            }
            catch
            {
                Game.rolestring = new Dictionary<Game.Roles, string>();
                startuptxt += "The rolestrings were <b>not read in</b> so they were set to an empty dict.\n";
            }
            Methods.EditMessage(startuptxt, startup);

            if (!System.IO.File.Exists(gitPullFile)) startuptxt += "<b>No git pull file found!</b>\n";
            if (!System.IO.File.Exists(nugetRestoreFile)) startuptxt += "<b>No nuget restore file found!</b>\n";
            if (!System.IO.File.Exists(buildFile)) startuptxt += "<b>No devenv build file found!</b>\n";

            GameClearer.Start();
            startuptxt += "The Game Cleaner was started!\n";
            Methods.EditMessage(startuptxt, startup);


            var BotT = client.GetMeAsync();
            BotT.Wait();
            Bot = BotT.Result;

            Console.Write("Program started!\n\n");

            client.OnUpdate += OnUpdate;
            client.StartReceiving();
            startuptxt += $"Bot started receiving at <code>{DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss")} UTC</code>\n<b>Version {version}</b> running.\n\n<b>Startup complete.</b>\n";
            Methods.EditMessage(startuptxt, startup, InlineKeyboards.Startup);


            while (running)
            {
                Thread.Sleep(1000);
            }
            GameClearer.Abort();
            client.StopReceiving();
            if (!string.IsNullOrEmpty(NewExeToStart)) System.Diagnostics.Process.Start(NewExeToStart);
            return;
        }

        private static void OnUpdate(object sender, UpdateEventArgs e)
        {
            try
            {
                #region Messages
                if (e.Update.Type == UpdateType.MessageUpdate)
                {
                    #region Text Messages
                    if (!string.IsNullOrEmpty(e.Update.Message.Text))
                    {
                        #region Allowed chat?
                        if (e.Update.Message.Chat.Type != ChatType.Private && !allowedgroups.Contains(e.Update.Message.Chat.Id))
                        {
                            Methods.ReplyToMessage("<b>Hey there!</b>\n\nYou just sent me a message in your group. Sorry, but I am only to be used in my own group: @WWAchievement. If you want to use me, come there, we are glad about every single new member! :D\n\n<b>Good bye.</b>", e.Update);
                            client.LeaveChatAsync(e.Update.Message.Chat.Id).Wait();
                            Methods.SendMessage(e.Update.Message.From.FirstName + " (" + e.Update.Message.From.Id + ", @" + e.Update.Message.From.Username + ") just sent a message to me in the group " + e.Update.Message.Chat.Title + " (" + e.Update.Message.Chat.Id + ", @" + e.Update.Message.Chat.Username + "), which I left because it is not an allowed group.", testgroup);
                            return;
                        }
                        #endregion

                        #region Are we handling this message?
                        if (e.Update.Message.Chat.Type == ChatType.Channel) return;
                        if (maintenance && e.Update.Message.Chat.Id != testgroup && !adminIds.Contains(e.Update.Message.From.Id) && !Game.Games.ContainsKey(e.Update.Message.Chat.Id)) return;
                        if (e.Update.Message.Date.ToUniversalTime() < startup.Date.ToUniversalTime()) return;
                        #endregion

                        #region Game cleaner time update?
                        if (Game.Games.ContainsKey(e.Update.Message.Chat.Id))
                        {
                            Game.Games[e.Update.Message.Chat.Id].Updatetime = DateTime.UtcNow;
                            Game.Games[e.Update.Message.Chat.Id].Notified = false;
                        }
                        #endregion

                        var text = e.Update.Message.Text;

                        var args = text.Contains(" ") ? new[] { text.Split(' ')[0], text.Remove(0, text.IndexOf(' ') + 1) } : new[] { text, null };
                        var command = args[0].ToLower().Replace('!', '/').Replace('@' + Bot.Username, "").Replace("@werewolfbot", "");

                        #region Commands
                        if (commands.ContainsKey(command))
                        {
                            commands[command].Invoke(null, new object[] { e.Update, args });
                        }
                        else if (admincommands.ContainsKey(command))
                        {
                            if (adminIds.Contains(e.Update.Message.From.Id)) admincommands[command].Invoke(null, new object[] { e.Update, args });
                            else Methods.ReplyToMessage("This command is only for bot admins! You aren't a bot admin!", e.Update);
                        }
                        else if (devcommands.ContainsKey(command))
                        {
                            if (devs.Contains(e.Update.Message.From.Id)) devcommands[command].Invoke(null, new object[] { e.Update, args });
                            else Methods.ReplyToMessage("This command is only for Ludwig! You aren't Ludwig!", e.Update);
                        }
                        #endregion

                        #region Assign Roles
                        else if (Game.roleAliases.ContainsKey(args[0].ToLower()) && args[0].ToLower() != "unknown")
                        {
                            Commands.AssignRole(e.Update, args, false);
                        }
                        else if (args[0].ToLower() == "now" && !string.IsNullOrEmpty(args[1]) && Game.roleAliases.ContainsKey(args[1].Split(' ')[0].ToLower()) && args[1].Split(' ')[0].ToLower() != "unknown")
                        {
                            args[0] = args[1].Split(' ')[0];
                            args[1] = args[1].Contains(' ') ? args[1].Remove(0, args[1].IndexOf(' ') + 1) : null;
                            Commands.AssignRole(e.Update, args, true);
                        }
                        #endregion

                        #region Set playerlist by forwarding #players
                        else if (args[0] == "#players:" && e.Update.Message.Entities.Count >= 2 && e.Update.Message.ForwardFrom != null && e.Update.Message.ForwardFrom.Id == wwbots[0])
                        {
                            if (e.Update.Message.Chat.Type != ChatType.Supergroup) return;
                            if (Game.Games.ContainsKey(e.Update.Message.Chat.Id))
                            {
                                if (Game.Games[e.Update.Message.Chat.Id].Gamestate == Game.State.Joining)
                                {
                                    string failed = Game.Games[e.Update.Message.Chat.Id].SetPlayersByPlayerlist(e.Update.Message.Entities.Where(x => x.Type == MessageEntityType.TextMention).ToList());
                                    Methods.ReplyToMessage("Successfully set the players to:\n" + string.Join("", Game.Games[e.Update.Message.Chat.Id].players.Select(x => "\n" + x.Value.Name)) + (string.IsNullOrEmpty(failed) ? "" : "\n\n<b>Failed for:</b>\n" + failed), e.Update);
                                }
                                else Methods.ReplyToMessage("You can't set the players this way after the game started! You have to use /addplayer or /dead manually for corrections!", e.Update);
                            }
                            else Methods.ReplyToMessage("It seems there was no game started in here, you can start one using /startgame@werewolfbot!", e.Update);
                        }
                        #endregion

                        #region Achv Thumb Up
                        else if (e.Update.Message.ForwardFrom != null && wwbots.Contains(e.Update.Message.ForwardFrom.Id) && (text.ToLower().Contains("new unlock") || text.ToLower().Contains("achievement unlock") || text.ToLower().Contains("new achievement")))
                        {
                            Methods.ReplyToMessage("👍🏻", e.Update);
                        }
                        #endregion
                    }
                    #endregion

                    #region NewChatMember
                    else if (e.Update.Message.NewChatMembers != null)
                    {
                        foreach (var ncm in e.Update.Message.NewChatMembers)
                        {
                            if (ncm.Id == Bot.Id)
                            {
                                if (!allowedgroups.Contains(e.Update.Message.Chat.Id))
                                {
                                    Methods.ReplyToMessage("<b>Hey there!</b>\n\nYou just added me to your group. Sorry, but I am only to be used in my own group: @WWAchievement. If you want to use me, come there, we are glad about every single new member! :D\n\n<b>Good bye.</b>", e.Update);
                                    client.LeaveChatAsync(e.Update.Message.Chat.Id).Wait();
                                    Methods.SendMessage(e.Update.Message.From.FirstName + " (" + e.Update.Message.From.Id + ", @" + e.Update.Message.From.Username + ") just added me to the group " + e.Update.Message.Chat.Title + " (" + e.Update.Message.Chat.Id + ", @" + e.Update.Message.Chat.Username + "), which I left because it is not an allowed group.", testgroup);
                                }
                            }
                            else
                            {
                                var name = ncm.FirstName + (string.IsNullOrEmpty(ncm.LastName) ? "" : " " + ncm.LastName);
                                var username = string.IsNullOrEmpty(ncm.Username) ? "(no username)" : ncm.Username.ToLower();
                                var achv = JsonConvert.DeserializeObject<List<WWAchievements>>(Methods.GetAchievements(ncm.Id)).Select(x => x.Name).Select(x => Game.achv.First(y => y.Value == x).Key).ToList() ?? new List<Game.Achievements>();

                                if (!BotUser.users.ContainsKey(ncm.Id))
                                {
                                    var bu = new BotUser(ncm.Id, Methods.FormatHTML(name), username, 0, false, string.Join("|", achv.Select(x => Game.achv[x])));
                                    BotUser.users.Add(ncm.Id, bu);
                                    Methods.SQL.ModifyUser("add", bu);
                                }
                                else if (BotUser.users[ncm.Id].Name != name || BotUser.users[ncm.Id].Username.ToLower() != username)
                                {
                                    BotUser.users[ncm.Id].Name = Methods.FormatHTML(name);
                                    BotUser.users[ncm.Id].Username = username;
                                    BotUser.users[ncm.Id].Achievements = achv;
                                    Methods.SQL.ModifyUser("modify", BotUser.users[ncm.Id]);
                                }
                            }
                        }
                    }
                    #endregion
                }
                #endregion

                #region Callback queries
                else if (e.Update.Type == UpdateType.CallbackQueryUpdate)
                {
                    if (e.Update.CallbackQuery.Message.Date.ToUniversalTime() >= startup.Date.ToUniversalTime())
                    {
                        var text = e.Update.CallbackQuery.Data;

                        var args = new[] { text.Split('|')[0], text.Remove(0, text.IndexOf('|') + 1) };
                        if (maintenance && e.Update.CallbackQuery.Message.Chat.Id != testgroup && !adminIds.Contains(e.Update.CallbackQuery.From.Id)) return;

                        if (callbacks.ContainsKey(args[0])) callbacks[args[0]].Invoke(null, new object[] { e.Update, args });
                    }
                }
                #endregion

                #region Daily tasks
                if (LatestBackup == null || LatestBackup.Date.ToUniversalTime() < DateTime.UtcNow.AddDays(-1))
                {
                    #region Daily Backup
                    var temp = Methods.SendBackup();
                    if (temp != null && (temp.Caption == "#AchvBotBackup" || temp.Text == "*this would be a backup if I was running on release*"))
                    {
                        LatestBackup = temp;
                    }
                    #endregion

                    // other stuff will come
                }

                #endregion
            }
            catch (Exception ex)
            {
                long chatid;
                if (e.Update.Type == UpdateType.MessageUpdate) chatid = e.Update.Message.Chat.Id;
                else if (e.Update.Type == UpdateType.CallbackQueryUpdate) chatid = e.Update.CallbackQuery.Message.Chat.Id;
                else chatid = -1;
                Methods.SendError(ex, chatid, false);
            }
        }

#region Assign Commands
        private static void AssignCommands()
        {
            Type type = typeof(Commands);

            commands.Add("/start", type.GetMethod("Start"));
            commands.Add("/startgame", type.GetMethod("StartGame"));
            commands.Add("/startchaos", type.GetMethod("StartGame"));
            commands.Add("/ap", type.GetMethod("AddPlayer"));
            commands.Add("/addplayer", type.GetMethod("AddPlayer"));
            commands.Add("/stopgame", type.GetMethod("StopGame"));
            commands.Add("/flee", type.GetMethod("FleePlayer"));
            commands.Add("/dead", type.GetMethod("FleePlayer"));
            commands.Add("/la", type.GetMethod("ListAchievements"));
            commands.Add("/listachv", type.GetMethod("ListAchievements"));
            commands.Add("/love", type.GetMethod("ToggleLoveStatus"));
            commands.Add("/getpin", type.GetMethod("GetPin"));
            commands.Add("/lo", type.GetMethod("GetLynchorder"));
            commands.Add("/lynchorder", type.GetMethod("GetLynchorder"));
            commands.Add("/slo", type.GetMethod("SetLynchorder"));
            commands.Add("/setlynchorder", type.GetMethod("SetLynchorder"));
            commands.Add("/rslo", type.GetMethod("ResetLynchorder"));
            commands.Add("/resetlynchorder", type.GetMethod("ResetLynchorder"));
            commands.Add("#ping", type.GetMethod("SummonPinglist"));
            commands.Add("/ping", type.GetMethod("Ping"));
            commands.Add("/version", type.GetMethod("GetVersion"));
            commands.Add("/listalias", type.GetMethod("ListAlias"));
            commands.Add("/runinfo", type.GetMethod("RunInfo"));
            commands.Add("/listcommands", type.GetMethod("ListCommands"));
            commands.Add("/reportbug", type.GetMethod("ReportBug"));
            commands.Add("/help", type.GetMethod("HelpText"));
            //...


            admincommands.Add("/addalias", type.GetMethod("AddAlias"));
            admincommands.Add("/delalias", type.GetMethod("DelAlias"));
            admincommands.Add("/userinfo", type.GetMethod("UserInfo"));
            admincommands.Add("/mostactive", type.GetMethod("MostActive"));
            admincommands.Add("/supresserrors", type.GetMethod("SupressErrors"));
            admincommands.Add("/pin", type.GetMethod("SetDefaultPin"));
            //...

            devcommands.Add("/shutdown", type.GetMethod("ShutDown"));
            devcommands.Add("/send", type.GetMethod("Send"));
            devcommands.Add("/leave", type.GetMethod("Leave"));
            devcommands.Add("/sqlite", type.GetMethod("RunSQLCommand"));
            devcommands.Add("/maint", type.GetMethod("ToggleMaintenance"));
            devcommands.Add("/reboot", type.GetMethod("Restart"));
            devcommands.Add("/test", type.GetMethod("Test"));
            devcommands.Add("/backup", type.GetMethod("SendDBBackup"));
            devcommands.Add("/usebackup", type.GetMethod("UseDBBackup"));
            devcommands.Add("/unusebackup", type.GetMethod("UnuseDBBackup"));
            devcommands.Add("/upgrade", type.GetMethod("UpdateBot"));

            type = typeof(Callbacks);

            callbacks.Add("startgame", type.GetMethod("GameStart"));
            callbacks.Add("stopgame", type.GetMethod("GameEnd"));
            callbacks.Add("restart", type.GetMethod("RestartBot"));
            callbacks.Add("maint", type.GetMethod("Maintenance"));
            //...
        }
#endregion
    }
}