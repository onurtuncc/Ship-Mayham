using System.Text;

namespace ShipMayham
{
    /// <summary>Rules shared by the host and the lobby screen so both agree on who the voyage is waiting for.</summary>
    public static class LobbyRules
    {
        public const int MaxNameLength = 16;

        /// <summary>The voyage waits on every active crew member except the host, until they press Ready.</summary>
        public static bool WaitingOnPlayer(bool active, bool isHost, bool ready) => active && !isHost && !ready;

        /// <summary>Trims, strips control and markup characters and limits the length. Returns null when nothing usable is left.</summary>
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var builder = new StringBuilder(MaxNameLength);
            bool lastSpace = true;
            foreach (char c in raw)
            {
                if (builder.Length >= MaxNameLength) break;
                if (char.IsControl(c) || c == '<' || c == '>') continue;
                if (char.IsWhiteSpace(c))
                {
                    if (lastSpace) continue;
                    builder.Append(' ');
                    lastSpace = true;
                    continue;
                }
                builder.Append(c);
                lastSpace = false;
            }
            string name = builder.ToString().TrimEnd();
            return name.Length == 0 ? null : name;
        }

        public static string DisplayName(string name, int slot) => string.IsNullOrEmpty(name) ? "Player " + (slot + 1) : name;
    }
}
