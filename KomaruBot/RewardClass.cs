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
    public class RewardClass
    {
        private static List<RewardClass> rewardClasses = new List<RewardClass>();


        public int hundrethsLeewayStart { get; private set; }
        public int hundrethsLeewayEnd { get; private set; }
        public int pointsAwarded { get; private set; }
        public RewardClass(
            int hundrethsLeewayStart,
            int hundrethsLeewayEnd,
            int pointsAwarded)
        {
            this.hundrethsLeewayStart = hundrethsLeewayStart;
            this.hundrethsLeewayEnd = hundrethsLeewayEnd;
            this.pointsAwarded = pointsAwarded;
            rewardClasses.Add(this);
        }

        public static int? GetPointsAwarded(int actualTime, int guessedTime, out string closenessString)
        {
            closenessString = "";
            foreach (var rewardClass in rewardClasses)
            {
                var difference = Math.Abs(actualTime - guessedTime);
                if (difference >= rewardClass.hundrethsLeewayStart && difference <= rewardClass.hundrethsLeewayEnd)
                {
                    if (rewardClass.hundrethsLeewayEnd == 0 && rewardClass.hundrethsLeewayStart == 0)
                    {
                        closenessString = "exactly";
                    }
                    else //if (rewardClass.hundrethsLeewayStart == 0 && rewardClass.hundrethsLeewayEnd != 0)
                    {
                        var hundrethsString = "";
                        if (rewardClass.hundrethsLeewayEnd <= 9)
                        {
                            hundrethsString = "0" + (rewardClass.hundrethsLeewayEnd.ToString());
                        }
                        else
                        {
                            hundrethsString = (rewardClass.hundrethsLeewayEnd.ToString());
                        }

                        closenessString = $"within +/- 0.{hundrethsString}";
                    }
                    return rewardClass.pointsAwarded;
                }
            }

            return null;
        }
    }
}
