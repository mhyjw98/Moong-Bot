using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public class ConnectionManager
    {
        public static Dictionary<ulong, System.Timers.Timer> guildConnecionTimer = new Dictionary<ulong, System.Timers.Timer>();

        public static void StartTimer(ulong guildId)
        {
            if (guildConnecionTimer.TryGetValue(guildId, out var timer))
            {
                timer.Start();
            }
        }

        public static void StopTimer(ulong guildId)
        {
            if (guildConnecionTimer.TryGetValue(guildId, out var timer))
            {
                timer.Stop();
                timer.Dispose();
            }
        }
    }
}
