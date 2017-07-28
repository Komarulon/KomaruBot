using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KomaruBot
{
    public class GambleConfiguration
    {
        public static int MinMinutesBetweenGambles;

        public static int MinGamble;
        public static int MaxGamble;

        public static Dictionary<int, decimal> GambleRolls = new Dictionary<int, decimal>();
    }
}
