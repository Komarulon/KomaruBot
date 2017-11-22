using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Models.Client;


namespace KomaruBot
{
    class Program
    {
        // TODO: CONFIGURABLE RewardClasses CONFIGURED IN Configuration.cs
        // TODO: PUT ALL STRINGS IN CONFIG FILE AND LOAD THEM UP BY REFERENCE

        // Looks like this is a way twitch prevents spam?:
		// TODO: Send Whispers doesn't work if the user never sent a whisper first
		
        // TODO: WHEN !guess xxxx GAME HASNT STARTED, WHISPER TO USER THAT ITS ALL OVER 


        public static bool whisperMode = true;

        public static string currencySingular = "";
        public static string currencyPlural = "";

        public static string user = "";
        public static string oauth = "";
        public static string streamElementsAPIKey = "";
        public static string streamElementsAccountID = "";
        public static string channel = "";

        public const string database_fn = "History.sqlite";
        public const string config_fn = "bot.txt";

        public static bool round_started = false;
        private static long round_id = 0;
        private static long round_started_time = 0;
        private static long round_awarded = 0; // how many awards this round
        public static TwitchClient cl;

        // So there's the time allowed to guess. 
        // After this time, there's a hidden range we'll allow more times to guess
        // and still accept them
        private static int secondsToGuess = 45;

        // This is the "extra" time where guesses are allowed but it is not shown as such
        private static int secondsToGuessSecretExtra = 5;


        private static string configExample =
        @"
BOT USERNAME
TWITCH API KEY
STREAMELEMENTS JWT TOKEN
STREAMELEMENTS ACCOUNT ID
channel name
CURRENCY SINGULAR
CURRENCY PLURAL
";


        private static PointsManager.IPointsManager pointsManager;

        public static System.Data.SQLite.SQLiteConnection con;
        static void Main(string[] args)
        {
            try
            {
                Logging.Initialize();

                if (File.Exists(config_fn))
                {
                    using (StreamReader file = new StreamReader(config_fn))
                    {
                        user = file.ReadLine();
                        oauth = file.ReadLine();
                        streamElementsAPIKey = file.ReadLine();
                        streamElementsAccountID = file.ReadLine();
                        channel = file.ReadLine();
                        currencySingular = file.ReadLine();
                        currencyPlural = file.ReadLine();
                    }
                }
                else
                {
                    Logging.LogMessage($"No {config_fn} file could be found! It should look like this: " + configExample);
                    while (Console.Read() != 13) ;
                    return;
                }

                pointsManager = new PointsManager.StreamElementsPointsManager(streamElementsAPIKey, currencyPlural, currencySingular, streamElementsAccountID);

                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Logging.LogException);

                Thread mainThread = new Thread(Program.MainThread);
                Logging.LogMessage($"{user} starting up!");

                /* prep database */
                bool first = false;
                if (!File.Exists(database_fn))
                {
                    System.Data.SQLite.SQLiteConnection.CreateFile(database_fn);
                    first = true;
                    Logging.LogMessage("Initializing empty statistics database!");
                }

                con = new System.Data.SQLite.SQLiteConnection("data source=" + database_fn);

                if (first)
                {
                    /* create the table */
                    using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
                    {
                        con.Open();
                        com.CommandText = @"CREATE TABLE IF NOT EXISTS [channels] (
                                            [ID] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                            [channel_name] TEXT UNIQUE NOT NULL
                                        )";
                        com.ExecuteNonQuery();
                        com.CommandText = @"CREATE TABLE IF NOT EXISTS [rounds] (
                                            [ID] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                            [chan_id] INTEGER NOT NULL,
                                            [began] TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                            [time] INTEGER DEFAULT 0,
                                            FOREIGN KEY (chan_id) REFERENCES channels(ID)
                                        )";
                        com.ExecuteNonQuery();
                        com.CommandText = @"CREATE TABLE IF NOT EXISTS [players] (
                                            [ID] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                            [chan_id] INTEGER NOT NULL,
                                            [nickname] TEXT NOT NULL,
                                            [points] INTEGER DEFAULT 0,
                                            FOREIGN KEY (chan_id) REFERENCES channels(ID),
                                            UNIQUE (chan_id, nickname) ON CONFLICT REPLACE
                                        )";
                        com.ExecuteNonQuery();
                        com.CommandText = @"CREATE TABLE IF NOT EXISTS [guesses] (
                                            [ID] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                            [round_id] INTEGER NOT NULL,
                                            [user_id] TEXT NOT NULL,
                                            [chan_id] INTEGER NOT NULL,
                                            [t] TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                                            [time] INTEGER NOT NULL,
                                            FOREIGN KEY (user_id) REFERENCES players(ID),
                                            FOREIGN KEY (round_id) REFERENCES rounds(ID),
                                            FOREIGN KEY (chan_id) REFERENCES channels(ID),
                                            UNIQUE (round_id, user_id, chan_id) ON CONFLICT REPLACE
                                        )";
                        com.ExecuteNonQuery();
                        con.Close();
                    }
                }
                else
                {
                    long[] stat = stats();
                    Logging.LogMessage("Loaded statistics database. " + stat[0] + " viewers, " + stat[1] + " rounds, " + stat[2] + " guesses tracked across " + stat[3] + " channels.");
                }

                Configuration.Initialize();

                /* launch chat */
                mainThread.Start();
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "Startup Exception");
            }

            while (Console.Read() != 13) ;
        }

        private static void SendMessagesTogether(List<string> messages)
        {
            var msg = "";
            int maxMsgLength = 450;
            foreach (var message in messages)
            {
                if ((msg + message).Length > maxMsgLength)
                {
                    sendMessage(msg);
                    msg = message + " ";
                    Thread.Sleep(500);
                }
                else
                {
                    msg += message + " ";
                }
            }

            sendMessage(msg);
        }

        private static long[] stats()
        {
            long playerCount = 0;
            long roundCount = 0;
            long guessCount = 0;
            long channelCount = 0;
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();
                com.CommandText = "Select Count(*) from players";
                playerCount = (long)com.ExecuteScalar();
                com.CommandText = "Select Count(*) from rounds";
                roundCount = (long)com.ExecuteScalar();
                com.CommandText = "Select Count(*) from guesses";
                guessCount = (long)com.ExecuteScalar();
                com.CommandText = "Select Count(*) from channels";
                channelCount = (long)com.ExecuteScalar();
                con.Close();
            }

            return new long[] { playerCount, roundCount, guessCount, channelCount };
        }

        //private static int numberOfMessagesAllowed = 8;
        //private static int secondsThrottled = 30;
        private static int numberOfMessagesAllowed = 16;
        private static int secondsThrottled = 15;
        private static void MainThread()
        {
            try
            {
                connect();
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "Main Thread could not start up. Perhaps restarting could help?");
            }
        }

        private static void connect()
        {
            ConnectionCredentials credentials = new ConnectionCredentials(user, oauth);
            cl = new TwitchClient(credentials, channel, '!', '!', true, false);
            cl.Logging = false;
            cl.ChatThrottler = new TwitchLib.Services.MessageThrottler(numberOfMessagesAllowed, TimeSpan.FromSeconds(secondsThrottled));
            cl.ChatThrottler.OnClientThrottled += new EventHandler<TwitchLib.Events.Services.MessageThrottler.OnClientThrottledArgs>((sender, e) =>
            {
                try
                {
                    new Thread(() =>
                    {
                        try
                        {
                            if (e.ThrottleViolation == TwitchLib.Enums.ThrottleType.TooManyMessages)
                            {
                                var color = Console.ForegroundColor;
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"Throttled message, retrying in {(secondsThrottled / 2)} seconds: {e.Message}");
                                Console.ForegroundColor = ConsoleColor.White;

                                Thread.Sleep(TimeSpan.FromSeconds(secondsThrottled / 2));
                                sendMessage(e.Message);
                            }
                            else
                            {
                                Logging.LogMessage($"Could not send message due to throttle error {e.ThrottleViolation.ToString()}: {e.Message}", true);
                            }
                        }
                        catch (Exception exc)
                        {
                            Logging.LogException(exc, "Throttle Thread Exception");
                        }
                    }).Start();
                }
                catch (Exception exc)
                {
                    Logging.LogException(exc, "Starting Throttle Thread Exception");
                }
            });
            cl.OnMessageReceived += new EventHandler<OnMessageReceivedArgs>(globalChatMessageReceived);
            cl.OnWhisperReceived += new EventHandler<OnWhisperReceivedArgs>(komarusSecretCommand);
            cl.OnConnected += new EventHandler<OnConnectedArgs>(onConnected);
            cl.OnConnectionError += Cl_OnConnectionError;
            cl.OnIncorrectLogin += Cl_OnIncorrectLogin;
            cl.OnDisconnected += Cl_OnDisconnected;
            cl.Connect();
            Logging.LogMessage("Connecting...");
        }

        private static int reconnectTryCt = 0;
        private static int maxRetryCt = 10;
        private static void Cl_OnDisconnected(object sender, OnDisconnectedArgs e)
        {

            try
            {
                reconnectTryCt++;
                if (reconnectTryCt >= maxRetryCt)
                {
                    doNotReconnect = true;
                }

                if (doNotReconnect)
                {
                    Logging.LogMessage("Chat Disconnected. Not attempting to reconnect.", true);
                }
                else
                {
                    new Thread(() =>
                    {
                        try
                        {
                            Logging.LogMessage($"Chat Disconnected. Trying to reconnect (attempt {reconnectTryCt}) in 15 seconds... ", true);
                            Thread.Sleep(15000);
                            Logging.LogMessage($"Attempt {reconnectTryCt} Connecting...", true);
                            connect();
                        }
                        catch (Exception exc)
                        {
                            Logging.LogException(exc, "On Disconnected Errror. Perhaps restarting could help?");
                        }
                    }).Start();
                }
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "On Disconnected Starting Thread Errror. Perhaps restarting could help?");
            }
        }

        private static bool doNotReconnect = false;
        private static void Cl_OnIncorrectLogin(object sender, OnIncorrectLoginArgs e)
        {
            try
            {
                doNotReconnect = true;
                Logging.LogMessage($"Incorrect Login Exception Occurred. \r\nPlease double-check that {config_fn} is correct. \r\nIt should look like this: {configExample}\r\n\r\nIf you have further issues, please message Komaru!", true);
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "On Connected Error Exception");
            }
        }

        private static void Cl_OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            try
            {
                Logging.LogMessage("An Exception Occurred Connecting!  Perhaps restarting could help?", true);
                Logging.LogMessage($"Error username: {e.Username}");
                Logging.LogMessage($"Error message: {e.Error}");
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "On Connected Error Exception");
            }
        }

        private static bool notified = false;

        private static void onConnected(object sender, OnConnectedArgs e)
        {
            try
            {
                reconnectTryCt = 0;

                Logging.LogMessage("Connected! Channel: #" + channel);

                if (!notified)
                {
                    notified = true;
                    sendMessage($"{user} is ready to go!");
                }
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "On Connected Exception");
            }
        }

        public static void about(ChatMessage c, Command commandTriggered)
        {
            Logging.LogMessage("About req from " + c.Username);
            WhisperOrSend(c.Username, "I was programmed by @kraln, then modified by Komaru. You can find @kraln's github here: https://github.com/kraln/schickenbot");
        }

        public static void help(ChatMessage c, Command commandTriggered)
        {
            // TODO: fill out with new config stuff
            //verb("Help req from " + c.Username);
            //sendMessage($"I respond to the following commands: !{pointsCommandString}, !leaderboard, !stats, !help, !about, !guess xxxx");
            //if (c.IsBroadcaster || c.IsModerator)
            //{
            //    sendMessage("Mods can also !start, !reset, !end xxxx");
            //}
        }

        public static void stats(ChatMessage c, Command commandTriggered)
        {
            Logging.LogMessage("Stats req from " + c.Username);
            long[] stat = stats();
            WhisperOrSend(c.Username, stat[0] + " viewers, " + stat[1] + " rounds, " + stat[2] + " guesses tracked across " + stat[3] + " channels.");
        }

        private static void award_points_OLD(string user_id, long guess, string endtime, long place)
        {
            long new_points = 0;
            round_awarded++;
            switch (place)
            {
                case 0:
                    new_points = 500; // exact guess
                    break;
                case 1:
                    new_points = 50; // first
                    break;
                case 2:
                    new_points = 15; // second
                    break;
                case 3:
                    new_points = 5; // third
                    break;
            }

            string player_name = "<unknown>";
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                // add points to the user
                com.CommandText = "UPDATE players SET points = points + @new_points WHERE id = @id";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@id", user_id);
                com.Parameters.AddWithValue("@new_points", new_points);
                com.ExecuteNonQuery();

                // and get their name
                com.CommandText = "SELECT nickname FROM players WHERE id = @id";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@id", user_id);
                object res = com.ExecuteScalar();
                if (res != null)
                {
                    player_name = (string)res;
                }
            }

            if (player_name != "<unknown>")
            {
                long? finalPoints;
                pointsManager.GivePlayerPoints(player_name, new_points, out finalPoints);
            }

            var curString = (new_points == 1 ? currencySingular : currencyPlural);

            // notify the channel
            switch (place)
            {
                case 0:
                    sendMessage($"{player_name} guessed exactly, and wins {new_points} {curString}!");
                    break;
                case 1:
                    sendMessage($"{player_name} was the closest, and wins {new_points} {curString}!");
                    break;
                case 2:
                    sendMessage($"{player_name} came in second and earns {new_points} {curString}.");
                    break;
                case 3:
                    sendMessage($"{player_name} had the third best guess, earning {new_points} {curString}.");
                    break;
            }
        }

        private static void award_points(string user_id, int numPoints, string closenessString, ref List<string> messages)
        {
            round_awarded++;

            string player_name = "<unknown>";
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();

                // add points to the user
                com.CommandText = "UPDATE players SET points = points + @new_points WHERE id = @id";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@id", user_id);
                com.Parameters.AddWithValue("@new_points", numPoints);
                com.ExecuteNonQuery();

                // and get their name
                com.CommandText = "SELECT nickname FROM players WHERE id = @id";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@id", user_id);
                object res = com.ExecuteScalar();
                if (res != null)
                {
                    player_name = (string)res;
                }

                con.Close();
            }

            Logging.LogMessage($"Awarding {numPoints} points to {player_name}");
            if (player_name != "<unknown>")
            {
                long? newPoints;

                pointsManager.GivePlayerPoints(player_name, numPoints, out newPoints);

                var curString = (numPoints == 1 ? currencySingular : currencyPlural);

                if (newPoints != null)
                {
                    // notify the channel
                    messages.Add($"{player_name} guessed {closenessString}, and wins {numPoints} {curString}{(newPoints.HasValue ? ($" ({newPoints} total)") : "")}!");
                }
            }
        }

        private static string getTimeStr(int time)
        {
            return time.ToString().Insert(2, ".");
        }

        private static void award_everyone_points(List<string> user_ids, int numPoints, int time, ref List<string> messages)
        {
            Logging.LogMessage($"Awarding {numPoints} to all guessers because the time was {time}");
            if (!user_ids.Any())
            {
                messages.Add($"Nobody guessed. You all missed out on {numPoints} from a Ceres time of {getTimeStr(time)}! :(");
                return;
            }

            var playerNames = new List<string>();
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();

                foreach (var user_id in user_ids)
                {
                    // add points to the user
                    com.CommandText = "UPDATE players SET points = points + @new_points WHERE id = @id";
                    com.CommandType = System.Data.CommandType.Text;
                    com.Parameters.AddWithValue("@id", user_id);
                    com.Parameters.AddWithValue("@new_points", numPoints);
                    com.ExecuteNonQuery();

                    // and get their name
                    com.CommandText = "SELECT nickname FROM players WHERE id = @id";
                    com.CommandType = System.Data.CommandType.Text;
                    com.Parameters.AddWithValue("@id", user_id);
                    object res = com.ExecuteScalar();
                    if (res != null)
                    {
                        playerNames.Add((string)res);
                    }
                }

                con.Close();
            }

            Logging.LogMessage($"Points being distributed to: {(string.Join(", ", playerNames.ToArray()))}");
            

            if (playerNames.Count > 4)
            {
                messages.Add($"Awarding {numPoints} points to {playerNames.Count} beautiful people for a Ceres time of {getTimeStr(time)}!");
            }
            else
            {
                messages.Add($"Awarding {numPoints} points to {(string.Join(", ", playerNames.ToArray()))} for a Ceres time of {getTimeStr(time)}!");
            }

            foreach (var player_name in playerNames)
            {
                long? newPoints;
                if (numPoints != 0)
                {
                    pointsManager.GivePlayerPoints(player_name, numPoints, out newPoints);
                }
            }
        }


        private class UserAward
        {
            public string userId { get; set; }
            public int guesstime { get; set; }
            public int awardedPoints { get; set; }
            public string closeness { get; set; }
        }

        public static void round_end(ChatMessage c, Command commandTriggered)
        {
            string endtime = new string(c.Message.Where(Char.IsDigit).ToArray()); // linq magic to extract any leading/trailing chars

            if (endtime.Length != 4)
            {
                Logging.LogMessage("Invalid endtime (" + endtime + ")", true);
                return;
            }

            cancelRoundEndTimer();
            cancelRoundRealEndTimer();
            round_started = false;
            guesses_allowed = false;

            long chan_id = get_channel_id(c.Channel);

            Logging.LogMessage("round ended by " + c.Username + ", with time of " + endtime);

            List<string> allUserIDs = new List<string>();

            var awards = new List<UserAward>();
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();

                // first, all the perfect guesses
                com.CommandText = @"SELECT user_id, time 
                                    FROM guesses
                                    WHERE round_id = @round_id
                                    AND chan_id = @chanid";

                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@round_id", round_id);
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.Parameters.AddWithValue("@end_time", endtime);

                
                using (System.Data.SQLite.SQLiteDataReader r = com.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var uid = (string)r["user_id"];

                        allUserIDs.Add(uid);

                        var time = (int)((long)r["time"]);
                        string closenessString;
                        var pts = RewardClass.GetPointsAwarded(int.Parse(endtime), time, out closenessString);
                        if (pts.HasValue && pts != 0)
                        {
                            awards.Add(new UserAward { awardedPoints = pts.Value, guesstime = time, userId = uid,
                            closeness = closenessString
                            });
                        }
                    }
                }

                con.Close();
            }

            var magicTime = MagicTime.GetPointsAwarded(int.Parse(endtime));

            List<string> messages = new List<string>();

            var roundStuffWon = false;
            if (magicTime != null)
            {
                roundStuffWon = true;
                award_everyone_points(allUserIDs, magicTime.reward, int.Parse(endtime), ref messages);
            }

            if (awards.Any())
            {
                roundStuffWon = true;
                Logging.LogMessage("Awarding points...");
                foreach (var a in awards)
                {
                    award_points(a.userId, a.awardedPoints, a.closeness, ref messages);
                }
            }

            if (!roundStuffWon)
            {
                Logging.LogMessage("Awarding no points...");
                messages.Add("Round #" + round_id + " ended. Nobody won. :(");
            }

            SendMessagesTogether(messages);


            /*
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();

                // first, all the perfect guesses
                com.CommandText = @"SELECT user_id, time 
                                    FROM guesses
                                    WHERE round_id = @round_id
                                    AND chan_id = @chanid
                                    AND time = @end_time";
                com.CommandType = System.Data.CommandType.Text;
                com(.Parameters.AddWithValue("@round_id", round_id);
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.Parameters.AddWithValue("@end_time", endtime);

                using (System.Data.SQLite.SQLiteDataReader r = com.ExecuteReader())
                {
                    while (r.Read())
                    {
                        award_points((string)r["user_id"], (long)r["time"], endtime, 0);
                    }
                }

                // then, all the users who weren't exactly right
                com.CommandText = @"SELECT user_id, time 
                                    FROM guesses
                                    WHERE round_id = @round_id
                                    AND time != @end_time
                                    AND chan_id = @chanid
                                    ORDER BY ABS(time - @end_time) ASC LIMIT 3";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@round_id", round_id);
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.Parameters.AddWithValue("@end_time", endtime);

                using (System.Data.SQLite.SQLiteDataReader r = com.ExecuteReader())
                {
                    long place = 1;
                    while (r.Read())
                    {
                        award_points((string)r["user_id"], (long)r["time"], endtime, place);
                        place++;
                    }
                }

                // then update the round with the final time, for stats
                com.CommandText = @"UPDATE rounds SET time = @end_time WHERE id = @id AND chan_id = @chanid";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@id", round_id);
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.Parameters.AddWithValue("@end_time", endtime);
                com.ExecuteNonQuery();

                con.Close();
            }

            if (round_awarded == 0)
            {
                sendMessage("Round #" + round_id + " ended without anyone playing :(");
            }
            */

            // end the round
        }

        public static void round_reset(ChatMessage c, Command commandTriggered)
        {
            round_started = false;
            guesses_allowed = false;
            cancelRoundEndTimer();
            cancelRoundRealEndTimer();
            sendMessage("Round #" + round_id + " cancelled.");
            Logging.LogMessage("Round #" + round_id + " cancelled.");
        }

        public static void round_begin(ChatMessage c, Command commandTriggered)
        {
            long chan_id = get_channel_id(c.Channel);
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();
                com.CommandText = "INSERT INTO rounds (chan_id) VALUES (@chanid)";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.ExecuteNonQuery();
                round_id = con.LastInsertRowId;
                con.Close();
            }
            round_started = true;
            round_awarded = 0;
            round_started_time = DateTime.Now.ToFileTimeUtc();

            guesses_allowed = true;
            sendMessage($"Round #{round_id} started. Type {(Command.commands.FirstOrDefault(x => x.commandType == Constants.CommandType.Guess).commandText)} xxxx to register your Ceres time. You have {secondsToGuess} seconds to place your guess.");
            Logging.LogMessage("Round #" + round_id + " started.");

            setRoundEndTimer();
        }

        private static object roundEndLock = new object();
        private static Timer roundEndTimer = null;
        private static bool guesses_allowed = false;
        private static void cancelRoundEndTimer()
        {
            lock (roundEndLock)
            {
                if (roundEndTimer != null)
                {
                    roundEndTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    roundEndTimer.Dispose();
                    roundEndTimer = null;
                }
            }
        }
        private static void setRoundEndTimer()
        {
            lock (roundEndLock)
            {
                roundEndTimer = new Timer((state) =>
                {
                    lock (roundEndLock)
                    {
                        sendMessage($"Guessing for round #{round_id} has ended. Good luck!");
                        Logging.LogMessage($"Round #{round_id} guesses wnding warning message sent ({secondsToGuess} has passed, but an additional {secondsToGuessSecretExtra} is allowed for guesses).");

                        cancelRoundEndTimer();
                        setRoundRealEndTimer();
                    }
                }, null, secondsToGuess * 1000, Timeout.Infinite);
            }
        }


        private static object roundRealEndLock = new object();
        private static Timer roundRealEndTimer = null;
        private static void cancelRoundRealEndTimer()
        {
            lock (roundRealEndLock)
            {
                if (roundRealEndTimer != null)
                {
                    roundRealEndTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    roundRealEndTimer.Dispose();
                    roundRealEndTimer = null;
                }
            }
        }

        private static void setRoundRealEndTimer()
        {
            lock (roundRealEndLock)
            {
                roundEndTimer = new Timer((state2) =>
                {
                    guesses_allowed = false;
                    cancelRoundRealEndTimer();
                    Logging.LogMessage($"Round #{round_id} guesses are over.");
                }, null, secondsToGuessSecretExtra * 1000, Timeout.Infinite);
            }
        }

        public static void hypeCommand(ChatMessage c, Command commandTriggered)
        {
            var cmd = commandTriggered as HypeCommand;
            if (cmd == null)
            {
                return;
            }

            var cost = cmd.CostInPoints;

            if (cost != 0)
            {
                var curPoints = pointsManager.GetCurrentPlayerPoints(c.Username);
                if (curPoints < cost)
                {
                    sendMessage($"You have only {curPoints} {(curPoints == 1 ? currencySingular : currencyPlural)}, @{c.Username} . {cost} {(cost == 1 ? (currencySingular + " is") : (currencyPlural + " are"))} required for that command.");
                    return;
                }

                long? newPoints;
                pointsManager.GivePlayerPoints(c.Username, (-1 * cost), out newPoints);
            }

            var chatStrings = cmd.getChatStrings();
            foreach (var chatMessage in chatStrings)
            {
                sendMessage(chatMessage);
            }
        }

        private static Dictionary<string, DateTime> userLastGambles = new Dictionary<string, DateTime>();
        public static void gamble(ChatMessage c, Command commandTriggered)
        {
            string gambleAmountStr = new string(c.Message.Where(Char.IsDigit).ToArray());

            lock (userLastGambles)
            {
                DateTime lastGamble;
                if (userLastGambles.TryGetValue(c.Username, out lastGamble))
                {
                    var lastGambleMustBeBeforeThisDate = DateTime.Now.AddMinutes(GambleConfiguration.MinMinutesBetweenGambles * -1);
                    if (lastGamble > lastGambleMustBeBeforeThisDate)
                    {
                        var timespan = (lastGamble - lastGambleMustBeBeforeThisDate);

                        var timeString = timespan.Minutes + " minutes";
                        if (timespan.Minutes == 1) { timeString = timespan.Minutes + " minute"; }

                        if (timespan.Minutes < 1)
                        {
                            timeString = timespan.Seconds + " seconds";
                            if (timespan.Seconds == 1) { timeString = timespan.Seconds + " second"; }
                        }

                        sendMessage($"@{c.Username}, you can only gamble once every {GambleConfiguration.MinMinutesBetweenGambles} {(GambleConfiguration.MinMinutesBetweenGambles == 1 ? "minute" : "minutes")}. Please wait another {timeString}.");
                        return;
                    }
                }
            }

            int gambleAmount;
            if (!int.TryParse(gambleAmountStr, out gambleAmount))
            {
                sendMessage($"I'm not sure what guess you meant, @{c.Username} . Please enter a new gamble with {(Command.commands.FirstOrDefault(x => x.commandType == Constants.CommandType.Gamble).commandText)} [amount]");
                return;
            }

            if (gambleAmount < GambleConfiguration.MinGamble)
            {
                sendMessage($"@{c.Username}, You must gamble at least {GambleConfiguration.MinGamble} {currencyPlural}");
                return;
            }

            if (gambleAmount > GambleConfiguration.MaxGamble)
            {
                sendMessage($"@{c.Username}, You cannot gamble more than {GambleConfiguration.MaxGamble} {currencyPlural}");
                return;
            }

            if (gambleAmount <= 0)
            {
                sendMessage($"@{c.Username}, You cannot gamble less than 1 {currencySingular}");
                return;
            }

            var curPoints = pointsManager.GetCurrentPlayerPoints(c.Username);
            if (curPoints < gambleAmount)
            {
                sendMessage($"You have only {curPoints} {(curPoints == 1 ? currencySingular : currencyPlural)}, @{c.Username} .");
                return;
            }

            lock (userLastGambles)
            {
                if (userLastGambles.ContainsKey(c.Username))
                {
                    userLastGambles[c.Username] = DateTime.Now;
                }
                else
                {
                    userLastGambles.Add(c.Username, DateTime.Now);
                }
            }

            var roll = r.Next(1, 101);
            var multiplier = GambleConfiguration.GambleRolls[roll];
            if (multiplier == 1)
            {
                sendMessage($"{c.Username} rolled {roll}. No {currencyPlural} won or lost.");
                return;
            }
            else if (multiplier < 1)
            {
                var amountLost = ((int)Math.Round((1 - multiplier) * gambleAmount));

                long? newPoints;
                pointsManager.GivePlayerPoints(c.Username, (amountLost * -1), out newPoints);

                sendMessage($"{c.Username} rolled {roll} and lost {amountLost} {(amountLost == 1 ? currencySingular : currencyPlural)}{(newPoints.HasValue ? ($" ({newPoints} total)") : "")}.");
                return;
            }
            else if (multiplier > 1)
            {
                var amountGained = ((int)Math.Round(multiplier * gambleAmount));

                long? newPoints;
                pointsManager.GivePlayerPoints(c.Username, amountGained, out newPoints);

                sendMessage($"{c.Username} rolled a {roll} and won {amountGained} {(amountGained == 1 ? currencySingular : currencyPlural)}{(newPoints.HasValue ? ($" ({newPoints} total)") : "")}!");
                return;
            }
        }

        private static Random r = new Random();

        public static void round_guess(ChatMessage c, Command commandTriggered)
        {
            if (!guesses_allowed)
            {
                return;
            }

            if (!round_started)
            {
                return;
            }

            string guess = new string(c.Message.Where(Char.IsDigit).ToArray()); // linq magic to extract any leading/trailing chars

            if (guess.Length != 4)
            {
                sendMessage($"I'm not sure what guess you meant, @{c.Username} . Please enter a new guess with {(Command.commands.FirstOrDefault(x => x.commandType == Constants.CommandType.Guess).commandText)} xxxx");
                return;
            }

            Logging.LogMessage($"guess from {c.Username} of {guess}");

            string user = c.Username;
            long chan_id = get_channel_id(c.Channel);

            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();

                // we track players based on their first guess
                com.CommandText = "INSERT OR IGNORE INTO players (nickname, chan_id) VALUES (@nickname, @chanid)";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@nickname", c.Username);
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.ExecuteNonQuery();

                con.Close();

                con.Open();

                // get the userid for this nickname
                com.CommandText = "SELECT id FROM players WHERE nickname = @nickname AND chan_id = @chanid";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@nickname", c.Username);
                com.Parameters.AddWithValue("@chanid", chan_id);
                Object res = com.ExecuteScalar();

                long userId = -1;
                if (res != null)
                {
                    userId = (long)com.ExecuteScalar();
                }
                else
                {
                    Logging.LogMessage("Problem with guess from " + c.Username + ". Couldn't find id?", true);
                    con.Close();
                    return;
                }

                // This is a goofy sqlite upsert
                com.CommandText = @"UPDATE OR IGNORE guesses 
                                    SET time=@guess, t=CURRENT_TIMESTAMP 
                                    WHERE user_id=@user_id AND round_id=@round_id AND chan_id=@chanid";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@guess", guess);
                com.Parameters.AddWithValue("@user_id", userId);
                com.Parameters.AddWithValue("@round_id", round_id);
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.ExecuteNonQuery();

                com.CommandText = "INSERT OR IGNORE INTO guesses (time, user_id, round_id, chan_id) VALUES (@guess, @user_id, @round_id, @chanid)";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@guess", guess);
                com.Parameters.AddWithValue("@user_id", userId);
                com.Parameters.AddWithValue("@round_id", round_id);
                com.Parameters.AddWithValue("@chanid", chan_id);
                com.ExecuteNonQuery();

                con.Close();
            }
        }


        public static void player_leaderboard(ChatMessage c, Command commandTriggered)
        {

            long chan_id = get_channel_id(c.Channel);
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();

                com.CommandText = @"SELECT nickname, points
                                    FROM players
                                    WHERE chan_id = @chanid
                                    ORDER BY points DESC LIMIT 5";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@chanid", chan_id);

                long pos = 1;
                string list = "Ceres Leaderboard: ";
                using (System.Data.SQLite.SQLiteDataReader r = com.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list = list + " (" + pos + ") " + ((string)r["nickname"]).Trim() + " - " + r["points"] + " won at Ceres, ";
                        pos++;
                    }
                }

                // then tell the player their position
                com.CommandText = @"SELECT count(*) AS rank 
                                    FROM players 
                                    WHERE chan_id = @chanid AND points > (SELECT points from players where nickname = @nickname)
                                    ORDER BY points DESC";

                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@nickname", c.Username);
                com.Parameters.AddWithValue("@chanid", chan_id);
                object res = com.ExecuteScalar();
                long rank = 0;
                if (res != null)
                {
                    rank = (long)res;
                }

                com.CommandText = @"SELECT count(*) from players where chan_id = @chanid";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@chanid", chan_id);
                res = com.ExecuteScalar();
                long total = 0;
                if (res != null)
                {
                    total = (long)res;
                }

                con.Close();

                // TODO: fix this:
                //WhisperOrSend(c.Username, list + " you are ranked " + (rank != 0 ? rank : total) + "/" + total);
                WhisperOrSend(c.Username, list);
            }
            Logging.LogMessage("Leaderboard req from " + c.Username);
        }

        public static void player_points(ChatMessage c, Command commandTriggered)
        {
            long playerPoints = 0;
            long chan_id = get_channel_id(c.Channel);
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();
                com.CommandText = "SELECT points FROM players WHERE nickname = @nickname AND chan_id = @chanid";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@nickname", c.Username);
                com.Parameters.AddWithValue("@chanid", chan_id);
                object res = com.ExecuteScalar();
                if (res != null)
                {
                    playerPoints = (long)res;
                }
                con.Close();
            }

            WhisperOrSend(c.Username, $"You have {playerPoints} {(playerPoints == 1 ? currencySingular : currencyPlural)}.");

            Logging.LogMessage($"{currencyPlural} reqest from {c.Username}");
        }

        private static void ensure_channel(ChatMessage c)
        {
            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();
                com.CommandText = "INSERT OR IGNORE INTO channels (channel_name) VALUES (@channel)";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@channel", c.Channel);
                com.ExecuteNonQuery();
                con.Close();
            }

        }

        private static long get_channel_id(string name)
        {
            long chan_id = -1;

            using (System.Data.SQLite.SQLiteCommand com = new System.Data.SQLite.SQLiteCommand(con))
            {
                con.Open();
                com.CommandText = "SELECT ID FROM channels WHERE channel_name=@channel_name";
                com.CommandType = System.Data.CommandType.Text;
                com.Parameters.AddWithValue("@channel_name", name);
                Object res = com.ExecuteScalar();
                if (res != null)
                {
                    chan_id = (long)res;
                }
                con.Close();
            }

            return chan_id;
        }

        private static void globalChatMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            try
            {
                // make sure there is a channel entry for this place
                if (e.ChatMessage.Message.StartsWith("!"))
                {
                    ensure_channel(e.ChatMessage);
                }

                var command = Command.GetCommand(e.ChatMessage.Message);

                if ((command == null) ||
                    (command.requiredRoundStarted && !round_started) ||
                    (command.requiredRoundNotStarted && round_started)
                    )
                {
                    return;
                }

                var callerAccessLevel = Constants.AccessLevel.Public;
                if (e.ChatMessage.IsModerator)
                {
                    callerAccessLevel = Constants.AccessLevel.Moderator;
                }
                if (e.ChatMessage.IsBroadcaster)
                {
                    callerAccessLevel = Constants.AccessLevel.Broadcaster;
                }

                if (command.requiredAccessLevel > callerAccessLevel)
                {
                    return;
                }

                command.onRun(e.ChatMessage, command);
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "On Message Received Exception");
            }
        }

        private static void WhisperOrSend(string username, string message)
        {
            if (whisperMode)
            {
                cl.SendWhisper(username, message);
            }
            else
            {
                sendMessage(message);
            }
        }

        private static class RevloAPI
        {
            
        }


        private static void komarusSecretCommand(object sender, OnWhisperReceivedArgs e)
        {
            try
            {
                var msg = e.WhisperMessage.Message;
                if (e.WhisperMessage.Username.ToLower() == "Komaru".ToLower())
                {
                    if (msg.Contains("!h"))
                    {
                        lock (sendLock)
                        {
                            cl.SendWhisper(e.WhisperMessage.Username, "\"!give x\"\r\n\"!take x\"\r\n\"!say x x x x\"\r\n\"!h or !help\"");
                        }
                    }
                    else if (msg.StartsWith("!say "))
                    {
                        var toremove = "!say ";
                        var toSay = msg.Remove(msg.IndexOf(toremove), toremove.Length);
                        sendMessage(toSay);
                    }
                    else if (msg.StartsWith("!"))
                    {

                        var msgSplt = msg.Split(' ');
                        if (msgSplt.Length == 2)
                        {
                            int amt;
                            if (int.TryParse(msgSplt[1], out amt))
                            {
                                long? newPoints;
                                if (msgSplt[0] == "!give")
                                {
                                    pointsManager.GivePlayerPoints(e.WhisperMessage.Username, amt, out newPoints);
                                    lock (sendLock)
                                    {
                                        cl.SendWhisper(e.WhisperMessage.Username, $"Given {amt}{(newPoints.HasValue ? ($" ({newPoints} total)") : "")}");
                                    }
                                }
                                else if (msgSplt[0] == "!take")
                                {
                                    pointsManager.GivePlayerPoints(e.WhisperMessage.Username, amt * -1, out newPoints);
                                    lock (sendLock)
                                    {
                                        cl.SendWhisper(e.WhisperMessage.Username, $"Taken {amt}{(newPoints.HasValue ? ($" ({newPoints} total)") : "")}");
                                    }
                                }
                            }
                        }

                    }
                }
            }
            catch (Exception exc)
            {
                Logging.LogException(exc, "Komaru's Secret Command Exception");
            }
        }

        private static object sendLock = new object();
        public static void sendMessage(string message)
        {
            lock (sendLock)
            {
                cl.SendMessage(message);
            }
        }
    }
}
