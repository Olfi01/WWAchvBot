using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Telegram.Bot.Types;
using static WWAchvBot.Commands;
using static WWAchvBot.Methods;
using static WWAchvBot.Program;

namespace WWAchvBot
{
    class Callbacks
    {
        /// <summary>
        /// Called on button press to start the game
        /// </summary>
        public static void GameStart(Update update, string[] args)
        {
            var chatid = long.Parse(args[1]);

            if (justCalledStartStop.Contains(update.CallbackQuery.From.Id))
            {
                justCalledStartStop.Remove(update.CallbackQuery.From.Id);

                if (Game.Games.ContainsKey(chatid) && Game.Games[chatid].Gamestate == Game.State.Joining)
                {
                    var from = BotUser.users[update.CallbackQuery.From.Id];
                    if (Game.Games[chatid].players.Count >= 5 || chatid == testgroup)
                    {
                        SendMessage($"{from.LinkedName} has considered the game started!", chatid);
                        client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "You successfully considered the game started!").Wait();
                        Game.Games[chatid].Start();

                        foreach (var p in Game.Games[chatid].players.Values)
                        {
                            BotUser.users[p.Id].Gamecount++;
                            SQL.ModifyUser("modify", BotUser.users[p.Id]);
                        }
                    }
                    else
                    {
                        SendMessage($"{from.LinkedName} tried to start the game but there are too less players yet!", chatid);
                        client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Too less players to start the game!").Wait();
                    }
                }
                else
                {
                    // either it is me debugging stuff, or 2 people hit the buttons the same time. Either way, do nothing except answering the query.
                    client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Failed to consider the game started.").Wait();
                }
            }
            else
            {
                justCalledStartStop.Add(update.CallbackQuery.From.Id);
                client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Press this button again to start the game.");
                Timer t = new Timer(new TimerCallback(delegate { try { justCalledStartStop.Remove(update.Message.From.Id); } catch { } }), null, 10000, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Called on button press to end the game
        /// </summary>
        public static void GameEnd(Update update, string[] args)
        {
            var chatid = long.Parse(args[1]);

            if (justCalledStartStop.Contains(update.CallbackQuery.From.Id))
            {
                justCalledStartStop.Remove(update.CallbackQuery.From.Id);

                if (Game.Games.ContainsKey(chatid))
                {
                    var from = BotUser.users[update.CallbackQuery.From.Id];
                    SendMessage($"{from.LinkedName} has considered the game stopped!", chatid);
                    client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "You successfully considered the game stopped!").Wait();
                    Game.Games[chatid].Stop();

                    foreach (var p in Game.Games[chatid].players.Values)
                    {
                        BotUser.users[p.Id].Achievements = JsonConvert.DeserializeObject<List<WWAchievements>>(GetAchievements(p.Id)).Select(x => Game.achv.First(y => y.Value == x.Name).Key).ToList();
                        SQL.ModifyUser("modify", BotUser.users[p.Id]);
                    }

                    Game.Games.Remove(chatid);
                }
                else
                {
                    // either it is me debugging stuff, or 2 people hit the buttons the same time. Either way, do nothing except answering the query.
                    client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Failed to consider the game stopped.").Wait();
                }
            }
            else
            {
                justCalledStartStop.Add(update.CallbackQuery.From.Id);
                client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "Press this button again to stop the game.");
                Timer t = new Timer(new TimerCallback(delegate { try { justCalledStartStop.Remove(update.Message.From.Id); } catch { } }), null, 10000, Timeout.Infinite);
            }
        }

        public static void RestartBot(Update update, string[] args)
        {
            if (devs.Contains(update.CallbackQuery.From.Id))
            {
                switch (args[1])
                {
                    case "update":
                        client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "You chose to update the bot").Wait();
                        EditMessage(update.CallbackQuery.Message.Text + "\n\n" + update.CallbackQuery.From.FirstName + " chose to update the bot.", update.CallbackQuery.Message);
                        startuptxt = "<b>Updating...</b>\n";
                        startup = SendMessage(startuptxt, testgroup);

                        DateTime endtime1 = DateTime.UtcNow;
                        startuptxt += $"Bot stopped at \n<code>{endtime1.ToString("dd.MM.yyyy HH:mm:ss")} UTC</code>\n\n<b>Shutdown complete.</b>";
                        EditMessage(startuptxt, startup);
                        System.Diagnostics.Process.Start(updateFile);
                        running = false;
                        return;

                    case "noupdate":
                        client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "You chose to restart the bot").Wait();
                        EditMessage(update.CallbackQuery.Message.Text + "\n\n" + update.CallbackQuery.From.FirstName + " chose to restart the bot.", update.CallbackQuery.Message);
                        startuptxt = "<b>Restarting...</b>\n";
                        startup = SendMessage(startuptxt, testgroup);

                        DateTime endtime2 = DateTime.UtcNow;
                        startuptxt += $"Bot stopped at \n<code>{endtime2.ToString("dd.MM.yyyy HH:mm:ss")} UTC</code>\n\n<b>Shutdown complete.</b>";
                        EditMessage(startuptxt, startup);
                        System.Diagnostics.Process.Start(restartFile);
                        running = false;
                        return;

                    case "abort":
                        client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "You chose to do nothing").Wait();
                        EditMessage(update.CallbackQuery.Message.Text + "\n\n" + update.CallbackQuery.From.FirstName + " chose to do nothing.", update.CallbackQuery.Message);
                        return;
                }
            }
            else client.AnswerCallbackQueryAsync(update.CallbackQuery.Id, "You are not a bot dev!").Wait();
        }

        public static void Maintenance(Update update, string[] args)
        {
            if (adminIds.Contains(update.CallbackQuery.From.Id))
            {
                switch (args[1])
                {
                    case "disable":
                        maintenance = false;
                        EditMessage(startuptxt + "\n\n" + update.CallbackQuery.From.FirstName + " disabled maintenance.", update.CallbackQuery.Message);
                        return;
                }
            }
        }
    }
}
