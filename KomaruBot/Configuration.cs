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
    public static class Configuration
    {
        private static string commandConfigFile = "commandconfiguration.txt";
        private static string magicTimesFile = "magictimes.txt";
        private static string gambleconfigFile = "gambleconfiguration.txt";

        public static void Initialize()
        {
            if (File.Exists(commandConfigFile))
            {
                using (StreamReader file = new StreamReader(commandConfigFile))
                {
                    file.ReadLine();
                    file.ReadLine();
                    file.ReadLine();
                    file.ReadLine();
                    file.ReadLine();
                    file.ReadLine();
                    file.ReadLine();
                    file.ReadLine();
                    string cmdText;
                    Constants.AccessLevel accessLevel;

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.Guess, Program.round_guess, accessLevel, true, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.End, Program.round_end, accessLevel, true, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.Reset, Program.round_reset, accessLevel, true, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.Start, Program.round_begin, accessLevel, false, true, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.GetPoints, Program.player_points, accessLevel, false, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.Leaderboard, Program.player_leaderboard, accessLevel, false, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.Stats, Program.stats, accessLevel, false, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.Help, Program.help, accessLevel, false, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.About, Program.about, accessLevel, false, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.ReadLine(); // Command Type: x
                    cmdText = file.ReadLine(); // cmd text
                    accessLevel = ((Constants.AccessLevel)(Enum.Parse(typeof(Constants.AccessLevel), file.ReadLine())));
                    new Command(Constants.CommandType.Gamble, Program.gamble, accessLevel, false, false, cmdText);
                    file.ReadLine(); // whitespace at end

                    file.Close();
                }
            }
            else
            {
                var color = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"You need to set the {commandConfigFile} file! Check with Komarulon!");
                Console.ForegroundColor = color;
            }



            if (File.Exists(magicTimesFile))
            {
                using (StreamReader file = new StreamReader(magicTimesFile))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (!line.StartsWith("#") && line.Contains(":"))
                        {
                            var split = line.Split(':');
                            if (split.Length == 2)
                            {
                                int time;
                                int reward;
                                if (int.TryParse(split[0], out time) &&
                                    int.TryParse(split[1], out reward))
                                {
                                    new MagicTime(time, reward);
                                }
                                else
                                {
                                    Logging.LogMessage($"Rejected magic time {line} because I couldn't figure out the numbers!", true);
                                }
                            }
                            else
                            {
                                Logging.LogMessage($"Rejected magic time {line} because it had more than one \":\"?", true);
                            }
                        }
                        else
                        {
                            if (line.Contains("!"))
                            {
                                Logging.LogMessage($"Rejected magic time {line} because it starts with #");
                            }
                        }
                    }
                    file.Close();
                }
            }
            else
            {
                Logging.LogMessage($"No Magic Times file", true);
            }

            if (File.Exists(gambleconfigFile))
            {
                using (StreamReader file = new StreamReader(gambleconfigFile))
                {
                    string line = file.ReadLine(); // "Gamble Minimum Value"
                    line = file.ReadLine();
                    int minGamble;
                    if (!int.TryParse(line, out minGamble))
                    {
                        Logging.LogMessage($"Could not parse minimum gamble value from " + line, true);
                        minGamble = 1;
                    }

                    line = file.ReadLine(); // "Gamble Maximum Value"
                    line = file.ReadLine();
                    int maxGamble;
                    if (!int.TryParse(line, out maxGamble))
                    {
                        Logging.LogMessage($"Could not parse maximum gamble value from " + line, true);
                        maxGamble = -1;
                    }

                    line = file.ReadLine(); // "Minimum Minutes Between Gambles Value"
                    line = file.ReadLine();
                    int gambleMinutes;
                    if (!int.TryParse(line, out gambleMinutes))
                    {
                        Logging.LogMessage($"Could not parse minimum minutes between gambles from " + line, true);
                        gambleMinutes = 1;
                    }

                    GambleConfiguration.MinGamble = minGamble;
                    GambleConfiguration.MaxGamble = maxGamble;
                    GambleConfiguration.MinMinutesBetweenGambles = gambleMinutes;


                    line = file.ReadLine(); // "Gamble Multipliers"
                    while ((line = file.ReadLine()) != null)
                    {
                        if (line.Contains(":"))
                        {
                            var split = line.Split(':');
                            if (split.Length == 2)
                            {
                                int roll;
                                decimal multiplier;
                                if (int.TryParse(split[0], out roll) &&
                                    decimal.TryParse(split[1], out multiplier))
                                {
                                    GambleConfiguration.GambleRolls.Add(roll, multiplier);
                                }
                                else
                                {
                                    Logging.LogMessage($"Rejected Gamble Multiplier {line} because I couldn't figure out the numbers!", true);
                                }
                            }
                            else
                            {
                                Logging.LogMessage($"Rejected Gamble Multiplier {line} because it had more than one \":\"?", true);
                            }
                        }
                    }

                    file.Close();

                    int totalWon = 0;
                    int totalLost = 0;
                    foreach (var a in GambleConfiguration.GambleRolls)
                    {
                        var multiplier = a.Value;
                        if (multiplier < 1)
                        {
                            totalLost += (int)Math.Round(100 * (1 - multiplier));
                        }
                        else if (multiplier == 1)
                        {

                        }
                        else if (multiplier > 1)
                        {
                            totalWon += (int)Math.Round(100 * multiplier);
                        }
                    }
                    Logging.LogMessage($"Trial of gambling run. Average lost is {totalLost}. Average won is {totalWon}.");
                }
            }
            else
            {
                Logging.LogMessage($"No Gamble Config file", true);
            }





            new RewardClass(0, 0, 100);
            new RewardClass(1, 2, 50);
            new RewardClass(3, 4, 10);
            new RewardClass(5, 5, 1);

        }
    }
}
