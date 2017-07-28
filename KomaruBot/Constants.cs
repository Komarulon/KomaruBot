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
    public class Constants
    {
        public enum CommandType
        {
            Guess,
            End,
            Reset,
            Start,
            GetPoints,
            Leaderboard,
            Stats, 
            Help, 
            About,
            Gamble
        }

        public enum AccessLevel
        {
            Public = 0,
            Moderator = 1,
            Broadcaster = 2,
        }
    }
}
