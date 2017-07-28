using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KomaruBot.PointsManager
{
    public interface IPointsManager
    {
        void GivePlayerPoints(string userName, long amount);

        long GetCurrentPlayerPoints(string userName);
    }
}
