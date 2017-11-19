using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KomaruBot.PointsManager
{
    public interface IPointsManager
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="amount"></param>
        /// <param name="newAmount">The amount of points the user has after this transaction, if available</param>
        void GivePlayerPoints(string userName, long amount, out long? newAmount);

        long GetCurrentPlayerPoints(string userName);
    }
}
