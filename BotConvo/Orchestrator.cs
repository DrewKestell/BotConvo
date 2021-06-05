using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using OpenNLP.Tools.Chunker;
using OpenNLP.Tools.PosTagger;
using OpenNLP.Tools.SentenceDetect;
using OpenNLP.Tools.Tokenize;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BotConvo
{
    class Orchestrator
    {
        readonly BotConvoSettings botConvoSettings;
        readonly ulong discordChannelId;
        readonly string discordBotToken;

        public Orchestrator(IConfiguration config)
        {
            botConvoSettings = config.GetSection("BotConvo").Get<BotConvoSettings>();
            discordChannelId = botConvoSettings.DiscordChannelId;
            discordBotToken = botConvoSettings.DiscordBotToken;
        }

        static readonly IDictionary<string, int> bots = new Dictionary<string, int>();
        static readonly Random random = new();
        static readonly HttpClient httpClient = new();

        // OpenNLP depends on a bunch of data to do its thing
        // these are included in the project and will get copied to the build output folder
        // to make sure they're available at runtime
        static readonly string openNlpPath = Path.Combine(Directory.GetCurrentDirectory(), "OpenNLP");
        readonly EnglishMaximumEntropySentenceDetector sentenceDetector = new(Path.Combine(openNlpPath, "EnglishSD.nbin"));
        readonly EnglishRuleBasedTokenizer tokenizer = new(false);
        readonly EnglishMaximumEntropyPosTagger posTagger = new(Path.Combine(openNlpPath, "EnglishPOS.nbin"), Path.Combine(openNlpPath, "tagdict"));
        readonly EnglishTreebankChunker chunker = new(Path.Combine(openNlpPath, "EnglishChunk.nbin"));

        static readonly string pythonExePath = Path.Combine(Directory.GetCurrentDirectory(), @"BotConvoPython\venv\Scripts\python.exe");
        static readonly string pythonScriptPath = Path.Combine(Directory.GetCurrentDirectory(), @"BotConvoPython\main.py");

        DiscordSocketClient client;
        SocketTextChannel channel;

        int messageIndex = 0;
        int nextMessageIndex = 0;
        string previousBotMessage = string.Empty;
        string previousUserMessage = string.Empty;

        internal async Task Initialize()
        {
            client = new DiscordSocketClient();

            client.Log += Log;
            client.Ready += ClientReady;
            client.MessageReceived += MessageReceived;

            await client.LoginAsync(TokenType.Bot, discordBotToken);
            await client.StartAsync();
        }

        // log Discord diagnostic messages to console
        async Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
        }

        async Task ClientReady()
        {
            channel = client.GetChannel(discordChannelId) as SocketTextChannel;

            StartConversation();
        }

        // this code will keep track of the most recent message posted by
        // all the non-bots in the discord server to use as a prompt
        async Task MessageReceived(SocketMessage msg)
        { 
            try
            {
                var msgText = msg.ToString();
                if (!bots.Any(b => msgText.Contains($"[{b.Key}]")))
                    previousUserMessage = msg.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        async void StartConversation()
        {
            foreach (var bot in botConvoSettings.Bots)
                StartBot(bot.Name, bot.CheckpointName, bot.Port, botConvoSettings);

            // give gpt2 time to initialize tensorflow and load the models
            await Task.Delay(10000);

            while (true)
            {
                // choose a random bot to send the next message,
                // and prevent the same bot from posting two messages in a row
                while (messageIndex == nextMessageIndex)
                    nextMessageIndex = random.Next(0, bots.Count);

                messageIndex = nextMessageIndex;

                var bot = bots.ElementAt(messageIndex);
                var port = bot.Value;

                string prompt = string.Empty;

                // use the most recent message from a non-bot in the server as a text prompt
                // will only use this once, until somebody else posts a message
                if (!string.IsNullOrWhiteSpace(previousUserMessage))
                {
                    prompt = GetPromptFromMessage(previousUserMessage);
                    previousUserMessage = string.Empty;
                }

                // if no humans have posted a message recently, try to use the latest
                // bot message as a prompt
                else
                {
                    // use the previous bot's message about 60% of the time
                    // the other 40% of the time, provide no prompt to get a fresh message
                    if (random.Next(0, 10) < 7)
                        prompt = GetPromptFromMessage(previousBotMessage);
                }
                
                // we have each bot model running on a different port.
                // this will send an HTTP request to the chosen bot server
                // to retrieve a message from that bot.
                // the prompt is passed in as a request header, and used
                // by the python code as a text generation prompt
                var httpRequestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://localhost:{port}"),
                    Headers = { { "prompt", prompt } }
                };
                try
                {
                    var response = await httpClient.SendAsync(httpRequestMessage);
                    var result = await response.Content.ReadAsStringAsync();
                    result = result.Replace("<|startoftext|>", string.Empty);

                    previousBotMessage = result;

                    await channel.SendMessageAsync($"[{bot.Key}] {previousBotMessage}");
                    
                    // wait between 3-7 seconds between generating messages
                    await Task.Delay(random.Next(3000, 7000));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        // given the previous message, we use OpenNLP to parse the sentence and
        // find the interesting bits to use as a prompt for the next message
        string GetPromptFromMessage(string message)
        {
            try
            {
                Console.WriteLine($"Generating prompt for message: {message}");

                var sentences = sentenceDetector.SentenceDetect(message);

                if (sentences.Any())
                    message = sentences.OrderByDescending(s => s.Length).ElementAt(0);

                // certain types of message do not make good prompts.
                if (message.Contains("http"))
                    return string.Empty;

                var tokens = tokenizer.Tokenize(message);
                var pos = posTagger.Tag(tokens);
                var chunks = chunker.GetChunks(tokens, pos)
                    .Where(c => c != null && c.Tag != null) // nlp library occasionally gives you null strings
                    .Where(c => !c.TaggedWords.Any(tw => tw.Word.StartsWith("'") || tw.Word.EndsWith("'"))) // nlp library splits contractions strangely sometimes, so this avoids prompts like "'s"
                    .Where(c => !(c.Tag == "PP" && c.TaggedWords.Count == 1)); // ignore prompts like "of"

                var prompt = string.Empty;

                if (chunks.Any())
                {
                    Console.WriteLine("Chunks:");

                    foreach (var chunk in chunks)
                        Console.WriteLine($"  {chunk.Tag} -- {string.Join(' ', chunk.TaggedWords.Select(tw => tw.Word)).Trim()}");

                    SentenceChunk selectedChunk;
                    if (chunks.Count() == 1)
                        selectedChunk = chunks.ElementAt(0);
                    else
                        selectedChunk = chunks.ElementAt(random.Next(0, chunks.Count() - 1));

                    prompt = string.Join(' ', selectedChunk.TaggedWords.Select(tw => tw.Word)).Trim();

                    if (prompt == message)
                        prompt = string.Empty;
                }

                Console.WriteLine($"Prompt: {prompt}\n");

                return prompt;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception occured while building prompt: {e}");
                return string.Empty;
            }
        }

        // start each bot running in it's own process
        // each process will host a lightweight web server on the given port,
        // and respond to GETs with gpt2 generated text
        static void StartBot(string botName, string checkpointName, int port, BotConvoSettings botConvoSettings)
        {
            bots.Add(botName, port);

            var start = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = $"{pythonScriptPath} {botConvoSettings.CheckpointDir} {botConvoSettings.ModelDir} {checkpointName} {port}",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            Process.Start(start);
        }
    }
}
