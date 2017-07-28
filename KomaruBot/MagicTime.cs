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
    public class MagicTime
    {
        public static List<MagicTime> magicTimes = new List<MagicTime>();


        public int time { get; set; }
        public int reward { get; set; }
        public MagicTime(
            int time,
            int reward)
        {
            this.time = time;
            this.reward = reward;

            magicTimes.Add(this);
        }

        public static MagicTime GetPointsAwarded(int actualTime)
        {
            return magicTimes.FirstOrDefault(x => x.time == actualTime);
        }
    }
}
