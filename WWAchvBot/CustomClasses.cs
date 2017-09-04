using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InlineKeyboardButtons;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using static WWAchvBot.Program;

namespace WWAchvBot
{
    class BotUser
    {
        public static Dictionary<int, BotUser> users = new Dictionary<int, BotUser>();


        public string Name { get; set; }
        public int Id { get; }
        public string Username { get; set; }
        public bool Subscribing { get; set; }
        public int Gamecount { get; set; }
        public List<Game.Achievements> Achievements { get; set; }
        public string LinkedName
        {
            get
            {
                return Username == "(no username)"
                    ? "<a href=\"tg://user?id=" + Id + "\">" + Name + "</a>"
                    : "<a href=\"https://t.me/" + Username + "\">" + Name + "</a>";
            }
        }

        public BotUser(int Id, string Name, string Username = null, int Gamecount = 0, bool Subscribing = false, string Achievements = "")
        {
            this.Name = Name;
            this.Id = Id;
            this.Username = Username;
            this.Subscribing = Subscribing;
            this.Gamecount = Gamecount;
            this.Achievements = string.IsNullOrEmpty(Achievements) ? new List<Game.Achievements>() : Achievements.Split('|').Select(x => Game.achv.First(y => y.Value == x).Key).ToList();
        }
    }

    class Player
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public Game.Roles Role { get; set; }
        public bool Love { get; set; }
        public bool Alive { get; set; }

        public Player(int Id, string Name)
        {
            this.Id = Id;
            this.Name = Name;
            this.Role = Game.Roles.Unknown;
            this.Love = false;
            this.Alive = true;

            string Achievements = Methods.GetAchievements(Id);
            BotUser.users[Id].Achievements = JsonConvert.DeserializeObject<List<WWAchievements>>(Achievements).Select(x => x.Name).Select(x => Game.achv.First(y => y.Value == x).Key).ToList();
            Methods.SQL.ModifyUser("modify", BotUser.users[Id]);
        }
    }

    public class WWAchievements
    {
        public string Name { get; set; }
        public string Desc { get; set; }
    }

    class Game
    {
        public static Dictionary<long, Game> Games = new Dictionary<long, Game>();
        public static Dictionary<string, Roles> roleAliases = new Dictionary<string, Roles>();

        public static readonly Dictionary<string, Roles> defaultAliases = new Dictionary<string, Roles>()
        {
            { "AlphaWolf", Roles.AlphaWolf },
            { "ApprenticeSeer", Roles.ApprenticeSeer },
            { "Beholder", Roles.Beholder },
            { "Blacksmith", Roles.Blacksmith },
            { "ClumsyGuy", Roles.ClumsyGuy },
            { "Cultist", Roles.Cultist },
            { "CultistHunter", Roles.CultistHunter },
            { "Cupid", Roles.Cupid },
            { "Cursed", Roles.Cursed },
            { "Detective", Roles.Detective },
            { "Doppelgänger", Roles.Doppelgänger },
            { "Drunk", Roles.Drunk },
            { "Fool", Roles.Fool },
            { "GuardianAngel", Roles.GuardianAngel },
            { "Gunner", Roles.Gunner },
            { "Harlot", Roles.Harlot },
            { "Hunter", Roles.Hunter },
            { "Mason", Roles.Mason },
            { "Mayor", Roles.Mayor },
            { "Prince", Roles.Prince },
            { "Seer", Roles.Seer },
            { "SerialKiller", Roles.SerialKiller },
            { "Sorcerer", Roles.Sorcerer },
            { "Tanner", Roles.Tanner },
            { "Traitor", Roles.Traitor },
            { "Villager", Roles.Villager },
            { "Werewolf", Roles.Werewolf },
            { "WildChild", Roles.WildChild },
            { "WolfCub", Roles.WolfCub },
            { "SeerFool", Roles.SeerFool },
        };

        public Dictionary<int, Player> players = new Dictionary<int, Player>();

        public Message Pinmessage { get; set; }
        public State Gamestate { get; set; }
        public DateTime Updatetime { get; set; }
        public bool Notified { get; set; }
        public Message DefaultPinMessage { get; set; }

        public int Spawnablewolves { get; set; }
        public int Visitcount { get; set; }
        public List<KeyValuePair<int, Player>> AlivePlayers { get; set; }

        public string Lynchorder = "";

        public Game(Message pin, Message defaultPin)
        {
            DefaultPinMessage = defaultPin;
            Pinmessage = pin;
            if (!Methods.PinMessage(pin)) Methods.SendMessage("Can't pin a message! Please inform this group's creator to make me admin!", pin.Chat.Id);
            UpdatePlayerlist();
            Updatetime = DateTime.UtcNow;
            Notified = false;
        }

        public bool AddPlayer(int newplayer)
        {
            if (!players.ContainsKey(newplayer))
            {
                try
                {
                    var u = BotUser.users[newplayer];
                    var clearname = u.Name.Length > 15 ? u.Name.Substring(0, 12) + "..." : u.Name;
                    var name = u.Username == "(no username)"
                        ? $"<a href=\"tg://user?id={newplayer}\">{clearname}</a>"
                        : $"<a href=\"https://t.me/{u.Username}\">{clearname}</a>";

                    players.Add(newplayer, new Player(newplayer, name));
                    UpdatePlayerlist();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public bool RemovePlayer(int oldplayer)
        {
            if (players.ContainsKey(oldplayer))
            {
                try
                {
                    players.Remove(oldplayer);
                    UpdatePlayerlist();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a list of names it failed to add
        /// </summary>
        public string SetPlayersByPlayerlist(List<MessageEntity> entities)
        {
            var failed = "";
            players.Clear();

            foreach (var entity in entities)
            {
                try
                {
                    if (entity.Type != MessageEntityType.TextMention) continue;

                    var name = entity.User.FirstName + (string.IsNullOrEmpty(entity.User.LastName) ? "" : " " + entity.User.LastName);
                    var username = string.IsNullOrEmpty(entity.User.Username) ? "(no username)" : entity.User.Username.ToLower();
                    if (!BotUser.users.ContainsKey(entity.User.Id))
                    {
                        var user = entity.User;
                        var bu = new BotUser(user.Id, Methods.FormatHTML(name), username, 0, false);
                        BotUser.users.Add(user.Id, bu);
                        Methods.SQL.ModifyUser("add", bu);
                    }
                    else
                    {
                        BotUser.users[entity.User.Id].Name = Methods.FormatHTML(name);
                        BotUser.users[entity.User.Id].Username = username;
                        Methods.SQL.ModifyUser("modify", BotUser.users[entity.User.Id]);
                    }

                    var u = BotUser.users[entity.User.Id];
                    var clearname = u.Name.Length > 15 ? u.Name.Substring(0, 12) + "..." : u.Name;
                    var pname = u.Username == "(no username)"
                        ? $"<a href=\"tg://user?id={entity.User.Id}\">{clearname}</a>"
                        : $"<a href=\"https://t.me/{u.Username}\">{clearname}</a>";

                    players.Add(entity.User.Id, new Player(entity.User.Id, pname));
                }
                catch
                {
                    failed += entity.User.FirstName + "\n";
                }
            }
            UpdatePlayerlist();
            return failed;
        }

        public void Start()
        {
            Gamestate = State.Running;
            UpdatePlayerlist();
        }

        public void Stop()
        {
            var askupdate = Gamestate == State.Running;
            Gamestate = State.Stopped;
            UpdatePlayerlist();
            if (DefaultPinMessage != null) Methods.PinMessage(DefaultPinMessage);
            else Methods.UnpinMessage(Pinmessage.Chat.Id);

            if (maintenance)
            {
                if (Games.Count == 1 && askupdate)
                {
                    Methods.SendMessage($"A game was just stopped, no games are running anymore! What would you like to do?", testgroup, InlineKeyboards.Update);
                }
                else
                {
                    Methods.SendMessage($"A game was just stopped! <b>{Games.Count - 1}</b> more running.", testgroup);
                }
            }
        }

        public void UpdatePlayerlist()
        {
            string playerlist = Gamestate == State.Running
                ? $"<b>LYNCHORDER ({players.Count(x => x.Value.Alive)} of {players.Count}):</b>\n"
                : $"<b>Players ({players.Count}):</b>\n";

            foreach (var p in players.Values.Where(x => x.Alive))
            {
                if (Gamestate == State.Joining) playerlist += p.Name + "\n";
                else if (Gamestate == State.Running)
                {
                    if (p.Role != Roles.Unknown) playerlist += p.Name + ": " + rolestring[p.Role];
                    else playerlist += p.Name + ": " + rolestring[Roles.Unknown];

                    if (p.Love) playerlist += " ❤️";
                    playerlist += "\n";
                }
            }

            if (Gamestate == State.Running)
            {
                playerlist += "\n\n<b>DEAD PLAYERS 💀:</b>";

                foreach (var p in players.Values.Where(x => !x.Alive))
                {
                    playerlist += "\n" + p.Name + " (" + rolestring[p.Role] + ")";
                }
            }

            switch (Gamestate)
            {

                case State.Joining:
                    Methods.EditMessage($"<b>Join this game!</b>\n\nJoin using the button and remember to use /addplayer after joining. Click the start button below as soon as the roles are assigned and the game begins. <b>DON'T PRESS START BEFORE THE ROLES ARE ASSIGNED!</b>\n\n{playerlist}", Pinmessage, InlineKeyboards.JoiningGamePin(Pinmessage.Chat.Id));
                    break;

                case State.Running:
                    Methods.EditMessage($"<b>Game running!</b>\n\nPress stop <b>ONCE THE GAME STOPPED!</b>\n\n{playerlist}", Pinmessage, InlineKeyboards.RunningGamePin(Pinmessage.Chat.Id));
                    break;

                case State.Stopped:
                    Methods.EditMessage("<b>This game is finished!</b>", Pinmessage);
                    break;

                default: // fucked
                    return;
            }
        }

        public void CalculateAchvInfo()
        {
            AlivePlayers = players.Where(x => x.Value.Alive).ToList();

            Spawnablewolves = AlivePlayers.Count(x => new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub }.Contains(x.Value.Role));
            Spawnablewolves += AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.WildChild);
            Spawnablewolves += Spawnablewolves > 0 ? AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.Cursed) : 0;
            Spawnablewolves += Spawnablewolves > 0 ? AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.Doppelgänger) : 0;
            Spawnablewolves += AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.Traitor);
            if (AlivePlayers.Select(e => e.Value.Role).Contains(Roles.AlphaWolf)) Spawnablewolves = (AlivePlayers.Count() / 2 - 2); // Round about... lol

            Visitcount += Spawnablewolves >= 1 ? 1 : 0;
            Visitcount += AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cultist) ? 1 : 0;
            Visitcount += AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.SerialKiller);
            Visitcount += AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.CultistHunter);
            Visitcount += AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.Harlot);
            Visitcount += AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.GuardianAngel);
        }

        public enum State
        {
            Joining,
            Running,
            Stopped
        }

        public enum Achievements
        {
            //These achievements are attainable:
            WelcomeToHell,
            WelcomeToTheAsylum,
            AlzheimersPatient,
            OHAIDER,
            SpyVsSpy,
            IHaveNoIdeaWhatImDoing,
            Enochlophobia,
            Introvert,
            Naughty,
            Dedicated,
            Obsessed,
            Masochist,
            WobbleWobble,
            Inconspicuous,
            Survivalist,
            Promiscuous,
            MasonBrother,
            DoubleShifter,
            HeyManNiceShot,
            ThatsWhyYouDontStayHome,
            DoubleKill,
            ShouldHaveKnown,
            ISeeALackOfTrust,
            SundayBloodySunday,
            ChangeSidesWorks,
            ForbiddenLove,
            TheFirstStone,
            SmartGunner,
            SpeedDating,
            EvenAStoppedClockIsRightTwiceADay,
            SoClose,
            CultistConvention,
            SelfLoving,
            ShouldveSaidSomething,
            TannerOverkill,
            CultistFodder,
            LoneWolf,
            PackHunter,
            SavedByTheBullet,
            InForTheLongHaul,
            OHSHI,
            Veteran,
            DoubleVision,
            Streetwise,
            SerialSamaritan,

            //Following achievements are unattainable:
            HeresJohnny,
            IveGotYourBack,
            BlackSheep,
            Explorer,
            Linguist,
            Developer,


            // NEW ACHIEVEMENTS
            NoSorcery,
            CultistTracker,
            ImNotDrunBurppp,
            WuffieCult,
            DidYouGuardYourself,
            SpoiledRichBrat,
            ThreeLittleWolvesAndABigBadPig,
            President,
            IHelped,
            ItWasABusyNight,



        }

        public bool IsAchievable(Achievements achv, Player player)
        {
            if (BotUser.users[player.Id].Achievements.Contains(achv)) return false;

            switch (achv)
            {
                case Achievements.ChangeSidesWorks:
                    return player.Role == Roles.Doppelgänger || player.Role == Roles.WildChild || player.Role == Roles.Traitor || (AlivePlayers.Select(e => e.Value.Role).Contains(Roles.AlphaWolf) && !new[] { Roles.Werewolf, Roles.AlphaWolf, Roles.WolfCub }.Contains(player.Role)) || player.Role == Roles.ApprenticeSeer || (player.Role == Roles.Cursed && Spawnablewolves >= 1);

                case Achievements.CultistConvention:
                    return !new[] { Roles.AlphaWolf, Roles.WolfCub, Roles.Werewolf, Roles.SerialKiller, Roles.CultistHunter }.Contains(player.Role) && AlivePlayers.Select(e => e.Value.Role).Count(x => x != Roles.AlphaWolf && x != Roles.WolfCub && x != Roles.Werewolf && x != Roles.SerialKiller && x != Roles.CultistHunter) >= 10 && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cultist);

                case Achievements.CultistFodder:
                    return !new[] { Roles.Werewolf, Roles.WolfCub, Roles.AlphaWolf, Roles.SerialKiller, Roles.CultistHunter }.Contains(player.Role) && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.CultistHunter) && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cultist);

                case Achievements.DoubleKill:
                    return (player.Role == Roles.SerialKiller && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Hunter)) || (player.Role == Roles.Hunter && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.SerialKiller)) || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Hunter) && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.SerialKiller));

                case Achievements.DoubleShifter:
                    return false; // TOO HARD YET, GOTTA BE FIXED!

                case Achievements.DoubleVision:
                    return (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.ApprenticeSeer)) || (player.Role == Roles.ApprenticeSeer && players.Select(e => e.Value.Role).Contains(Roles.Doppelgänger));

                case Achievements.EvenAStoppedClockIsRightTwiceADay:
                    return player.Role == Roles.Fool || player.Role == Roles.SeerFool || (player.Role == Roles.Doppelgänger && (AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Fool) || AlivePlayers.Select(e => e.Value.Role).Contains(Roles.SeerFool)));

                case Achievements.ForbiddenLove:
                    return players.Select(e => e.Value.Role).Contains(Roles.Cupid) && ((player.Role == Roles.Villager && Spawnablewolves >= 1) || (new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub, Roles.WildChild, Roles.Traitor }.Contains(player.Role) && players.Select(e => e.Value.Role).Contains(Roles.Villager)) || (new[] { Roles.Doppelgänger, Roles.Cursed }.Contains(player.Role) && Spawnablewolves >= 1 && players.Select(e => e.Value.Role).Contains(Roles.Villager))) && (players.Count(x => x.Value.Love) > 2 || player.Love);

                case Achievements.HeyManNiceShot:
                    return player.Role == Roles.Hunter || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Hunter));

                case Achievements.Inconspicuous:
                    return players.Count >= 20;

                case Achievements.ISeeALackOfTrust:
                    return (new[] { Roles.Seer, Roles.ApprenticeSeer, Roles.SeerFool }.Contains(player.Role)) || (player.Role == Roles.Doppelgänger && (players.Select(e => e.Value.Role).Contains(Roles.Seer) || players.Select(e => e.Value.Role).Contains(Roles.SeerFool) || players.Select(e => e.Value.Role).Contains(Roles.ApprenticeSeer)));

                case Achievements.LoneWolf:
                    return new[] { Roles.Werewolf, Roles.AlphaWolf, Roles.WolfCub }.Contains(player.Role) && players.Count(x => new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub }.Contains(x.Value.Role)) == 1 && players.Count >= 10;

                case Achievements.Masochist:
                    return player.Role == Roles.Tanner || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Tanner));

                case Achievements.MasonBrother:
                    return AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.Mason) >= 2 && new[] { Roles.Mason, Roles.Doppelgänger }.Contains(player.Role);

                case Achievements.OHSHI:
                    return new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub, Roles.SerialKiller }.Contains(player.Role) && players.Select(e => e.Value.Role).Contains(Roles.Cupid) && (players.Count(x => x.Value.Love) > 2 || player.Love);

                case Achievements.PackHunter:
                    return AlivePlayers.Count >= 15 && Spawnablewolves >= 7 && (AlivePlayers.Select(e => e.Value.Role).Contains(Roles.AlphaWolf) || new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub, Roles.Traitor, Roles.WildChild, Roles.Cursed, Roles.Doppelgänger }.Contains(player.Role));

                case Achievements.Promiscuous:
                    return (player.Role == Roles.Harlot && AlivePlayers.Select(e => e.Value.Role).Count(x => x != Roles.Werewolf && x != Roles.WolfCub && x != Roles.AlphaWolf && x != Roles.SerialKiller && x != Roles.Harlot) >= 5) || (player.Role == Roles.Doppelgänger && AlivePlayers.Any(x => x.Value.Role == Roles.Harlot) && AlivePlayers.Select(e => e.Value.Role).Count(x => x != Roles.Werewolf && x != Roles.WolfCub && x != Roles.AlphaWolf && x != Roles.SerialKiller && x != Roles.Harlot && x != Roles.Doppelgänger) >= 5);

                case Achievements.SavedByTheBullet:
                    return AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Gunner) && Spawnablewolves >= 1 && !new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub }.Contains(player.Role);

                case Achievements.SelfLoving:
                    return player.Role == Roles.Cupid && (players.Count(x => x.Value.Love) < 2 || player.Love);

                case Achievements.SerialSamaritan:
                    return (player.Role == Roles.SerialKiller || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.SerialKiller))) && Spawnablewolves >= 3;

                case Achievements.ShouldHaveKnown:
                    return new[] { Roles.Seer, Roles.SeerFool, Roles.ApprenticeSeer }.Contains(player.Role) && AlivePlayers.Any(x => x.Value.Role == Roles.Beholder);

                case Achievements.ShouldveSaidSomething:
                    return players.Select(e => e.Value.Role).Contains(Roles.Cupid) && (new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub, Roles.WildChild, Roles.Traitor }.Contains(player.Role) || (new[] { Roles.Doppelgänger, Roles.Cursed }.Contains(player.Role) && Spawnablewolves >= 1)) && (players.Count(x => x.Value.Love) > 2 || player.Love);

                case Achievements.SmartGunner:
                    return (player.Role == Roles.Gunner || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Gunner))) && (Spawnablewolves >= 2 || (Spawnablewolves == 1 && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.SerialKiller)) || AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cultist));

                case Achievements.SoClose:
                    return player.Role == Roles.Tanner || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Tanner));

                case Achievements.SpeedDating:
                    return AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cupid) && (players.Count(x => x.Value.Love) < 2 || player.Love);

                case Achievements.Streetwise:
                    return (player.Role == Roles.Detective || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Detective))) && ((Spawnablewolves + AlivePlayers.Select(e => e.Value.Role).Count(x => x == Roles.SerialKiller) >= 4) || AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cultist));

                case Achievements.SundayBloodySunday:
                    return false; //THIS NEEDS WORK

                case Achievements.TannerOverkill:
                    return player.Role == Roles.Tanner || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Tanner));

                case Achievements.ThatsWhyYouDontStayHome:
                    return AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Harlot) && (new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub, Roles.Cultist, Roles.WildChild, Roles.Traitor }.Contains(player.Role) || (new[] { Roles.Doppelgänger, Roles.Cursed }.Contains(player.Role) && Spawnablewolves >= 1) || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cultist)));

                case Achievements.WobbleWobble:
                    return players.Count >= 10 && (player.Role == Roles.Drunk || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Drunk)));


                // NEW ACHIEVEMENTS
                case Achievements.NoSorcery:
                    return AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Sorcerer) && (new[] { Roles.AlphaWolf, Roles.Werewolf, Roles.WolfCub, Roles.WildChild, Roles.Traitor }.Contains(player.Role) || (new[] { Roles.Cursed, Roles.Doppelgänger }.Contains(player.Role) && Spawnablewolves >= 1));

                case Achievements.CultistTracker:
                    return (player.Role == Roles.CultistHunter || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.CultistHunter))) && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Cultist);

                case Achievements.ImNotDrunBurppp:
                    return player.Role == Roles.ClumsyGuy || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.ClumsyGuy));

                case Achievements.WuffieCult:
                    return (player.Role == Roles.AlphaWolf && players.Count >= 8 && AlivePlayers.Count / 2 > AlivePlayers.Count(x => new[] { Roles.AlphaWolf, Roles.WolfCub, Roles.Werewolf }.Contains(x.Value.Role))) || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.AlphaWolf) && players.Count >= 9 && AlivePlayers.Count / 2 > AlivePlayers.Count(x => new[] { Roles.AlphaWolf, Roles.WolfCub, Roles.Werewolf }.Contains(x.Value.Role)));

                case Achievements.DidYouGuardYourself:
                    return (player.Role == Roles.GuardianAngel || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.GuardianAngel))) && Spawnablewolves >= 1;

                case Achievements.SpoiledRichBrat:
                    return player.Role == Roles.Prince || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Prince));

                case Achievements.ThreeLittleWolvesAndABigBadPig:
                    return Spawnablewolves >= 3 && (player.Role == Roles.Sorcerer || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Sorcerer)));

                case Achievements.President:
                    return player.Role == Roles.Mayor || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.Mayor));

                case Achievements.IHelped:
                    return Spawnablewolves >= 2 && (player.Role == Roles.WolfCub || (player.Role == Roles.Doppelgänger && AlivePlayers.Select(e => e.Value.Role).Contains(Roles.WolfCub)));

                case Achievements.ItWasABusyNight:
                    return false; // THIS NEEDS WORK!

                case Achievements.TheFirstStone:
                case Achievements.InForTheLongHaul:
                    return true;




                default:
                    // UNATTAINABLE ONES AND ONES BOT CAN'T KNOW:
                    // AlzheimersPatient
                    // BlackSheep
                    // Dedicated
                    // Developer
                    // Enochlophobia
                    // Explorer
                    // HeresJohnny
                    // IHaveNoIdeaWhatImDoing
                    // Introvert
                    // IveGotYourBack
                    // Linguist
                    // Naughty
                    // Obsessed
                    // OHAIDER
                    // SpyVsSpy
                    // Survivalist
                    // Veteran
                    // WelcomeToHell
                    // WelcomeToTheAsylum
                    return false;
            }
        }

        public enum Roles
        {
            Villager,
            Werewolf,
            Drunk,
            Seer,
            Cursed,
            Harlot,
            Beholder,
            Gunner,
            Traitor,
            GuardianAngel,
            Detective,
            ApprenticeSeer,
            Cultist,
            CultistHunter,
            WildChild,
            Fool,
            Mason,
            Doppelgänger,
            Cupid,
            Hunter,
            SerialKiller,
            Tanner,
            Mayor,
            Prince,
            Sorcerer,
            ClumsyGuy,
            Blacksmith,
            AlphaWolf,
            WolfCub,
            SeerFool, // Used if not sure whether seer or fool
            Unknown
        }

        public static Dictionary<Roles, string> rolestring;

        public static Dictionary<Achievements, string> achv = new Dictionary<Achievements, string>()
        {
            { Achievements.AlzheimersPatient, "Alzheimer's Patient" },
            { Achievements.BlackSheep, "Black Sheep" },
            { Achievements.ChangeSidesWorks, "Change Sides Works" },
            { Achievements.CultistConvention, "Cultist Convention" },
            { Achievements.CultistFodder, "Cultist Fodder" },
            { Achievements.CultistTracker, "Cultist Tracker" },
            { Achievements.Dedicated, "Dedicated" },
            { Achievements.Developer, "Developer" },
            { Achievements.DidYouGuardYourself, "Did you guard yourself?" },
            { Achievements.DoubleKill, "Double Kill" },
            { Achievements.DoubleShifter, "Double Shifter" },
            { Achievements.DoubleVision, "Double Vision" },
            { Achievements.Enochlophobia, "Enochlophobia" },
            { Achievements.EvenAStoppedClockIsRightTwiceADay, "Even a Stopped Clock is Right Twice a Day" },
            { Achievements.Explorer, "Explorer" },
            { Achievements.ForbiddenLove, "Forbidden Love" },
            { Achievements.HeresJohnny, "Here's Johnny!" },
            { Achievements.HeyManNiceShot, "Hey Man, Nice Shot" },
            { Achievements.IHaveNoIdeaWhatImDoing, "I Have No Idea What I'm Doing" },
            { Achievements.IHelped, "I Helped!" },
            { Achievements.ImNotDrunBurppp, "I'M NOT DRUN-- *BURPPP*" },
            { Achievements.Inconspicuous, "Inconspicuous" },
            { Achievements.InForTheLongHaul, "In for the Long Haul" },
            { Achievements.Introvert, "Introvert" },
            { Achievements.ISeeALackOfTrust, "I See a Lack of Trust" },
            { Achievements.ItWasABusyNight, "It Was a Busy Night!" },
            { Achievements.IveGotYourBack, "I've Got Your Back" },
            { Achievements.Linguist, "Linguist" },
            { Achievements.LoneWolf, "Lone Wolf" },
            { Achievements.Masochist, "Masochist" },
            { Achievements.MasonBrother, "Mason Brother" },
            { Achievements.Naughty, "Naughty!" },
            { Achievements.NoSorcery, "No Sorcery!" },
            { Achievements.Obsessed, "Obsessed" },
            { Achievements.OHAIDER, "O HAI DER!" },
            { Achievements.OHSHI, "OH SHI-" },
            { Achievements.PackHunter, "Pack Hunter" },
            { Achievements.President, "President" },
            { Achievements.Promiscuous, "Promiscuous" },
            { Achievements.SavedByTheBullet, "Saved by the Bull(et)" },
            { Achievements.SelfLoving, "Self Loving" },
            { Achievements.SerialSamaritan, "Serial Samaritan" },
            { Achievements.ShouldHaveKnown, "Should Have Known" },
            { Achievements.ShouldveSaidSomething, "Should've Said Something" },
            { Achievements.SmartGunner, "Smart Gunner" },
            { Achievements.SoClose, "So Close!" },
            { Achievements.SpeedDating, "Speed Dating" },
            { Achievements.SpoiledRichBrat, "Spoiled Rich Brat" },
            { Achievements.SpyVsSpy, "Spy vs Spy" },
            { Achievements.Streetwise, "Streetwise" },
            { Achievements.SundayBloodySunday, "Sunday Bloody Sunday" },
            { Achievements.Survivalist, "Survivalist" },
            { Achievements.TannerOverkill, "Tanner Overkill" },
            { Achievements.ThatsWhyYouDontStayHome, "That's Why You Don't Stay Home" },
            { Achievements.TheFirstStone, "The First Stone" },
            { Achievements.ThreeLittleWolvesAndABigBadPig, "Three Little Wolves and a Big Bad Pig" },
            { Achievements.Veteran, "Veteran" },
            { Achievements.WelcomeToHell, "Welcome to Hell" },
            { Achievements.WelcomeToTheAsylum, "Welcome to the Asylum" },
            { Achievements.WobbleWobble, "Wobble Wobble" },
            { Achievements.WuffieCult, "Wuffie-Cult" },
        };
    }

    class InlineKeyboards
    {
        public static IReplyMarkup Subscribe
        {
            get
            {
                return new InlineKeyboardMarkup(

                    new InlineKeyboardButton[]
                    {
                        new InlineKeyboardUrlButton("Subscribe", $"https://t.me/{Bot.Username}?start=subscribe"),
                        new InlineKeyboardUrlButton("Unsubscribe", $"https://t.me/{Bot.Username}?start=unsubscribe")
                    }
                );
            }
        }

        public static IReplyMarkup JoiningGamePin(long chatid)
        {
            return new InlineKeyboardMarkup(
                
                new InlineKeyboardButton[]
                {
                    new InlineKeyboardCallbackButton("Start", "startgame|" + chatid.ToString()),
                    new InlineKeyboardCallbackButton("Abort", "stopgame|" + chatid.ToString()),
                }
            );
        }

        public static IReplyMarkup RunningGamePin(long chatid)
        {
            return new InlineKeyboardMarkup(

                new InlineKeyboardButton[]
                {
                    new InlineKeyboardCallbackButton("Stop", "stopgame|" + chatid.ToString()),
                }
            );
        }

        public static IReplyMarkup Update
        {
            get
            {
                return new InlineKeyboardMarkup(

                    new InlineKeyboardButton[]
                    {
                        new InlineKeyboardCallbackButton("Update", "restart|update"),
                        new InlineKeyboardCallbackButton("Restart", "restart|noupdate"),
                        new InlineKeyboardCallbackButton("Nothing", "restart|abort"),
                    }
                    
                );
            }
        }
    }
}
