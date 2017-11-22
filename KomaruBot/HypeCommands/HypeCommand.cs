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
    public class HypeCommand : Command
    {
        public static List<HypeCommand> hypeCommands = new List<HypeCommand>();

        public string Name { get; set; }
        public List<string> CommandText { get; set; }
        public long CostInPoints { get; set; }
        public List<string> BotText { get; set; }
        public bool PickRandomBotText { get; set; }
        public int Repeat { get; set; }

        public HypeCommand() : base(
            Constants.CommandType.Hype,
            Program.hypeCommand,
            Constants.AccessLevel.Public,
            false,
            false)
        {
            hypeCommands.Add(this);
        }

        static Random rnd = new Random();

        public List<string> getChatStrings()
        {
            var res = new List<string>();

            var timesToRepeat = this.Repeat;
            var availableTextStrings = this.BotText.Select(x => x).ToList(); // make a copy of the text strings

            while (timesToRepeat > 0 && availableTextStrings.Any())
            {
                timesToRepeat--;
                var idx = 0;
                if (this.PickRandomBotText)
                {
                    idx = rnd.Next(availableTextStrings.Count);
                }

                var text = availableTextStrings[idx];
                //availableTextStrings.RemoveAll(x => x == text);
                availableTextStrings.RemoveAt(idx);
                res.Add(text);
            }
            
            return res;
        }
        
    }
}
