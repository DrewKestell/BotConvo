using System.Collections.Generic;

namespace BotConvo
{
    public class BotConvoSettings
    {
        public string CheckpointDir { get; set; }

        public string ModelDir { get; set; }

        public ulong DiscordChannelId { get; set; }

        public string DiscordBotToken { get; set; }

        public IEnumerable<Bot> Bots { get; set; }
    }
}
