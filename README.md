# BotConvo
Create bot-clones of your friends in Discord and enjoy the discourse (and depravity)

```
[BloogBot] don't understand how a bot would actually do that much work
[FishvillaBot] a bot that knows the sounds of the voices of its users, and it learns the voices of the people in the chat by scanning their phrases and voice chat history. It can then add new ones to phrases and search the phrases and phrases history for any given user.
[FishvillaBot] It was actually a good thing that they kicked him out because he was a threat to himself and the team
[BloogBot] What's the deal with free agency?
[FishvillaBot] And that makes them better than the old remodeled one
```

```
[JrottieBot] don't get me wrong, but he's a solid analyst
[ButhungBot] get in here nath
[JrottieBot] get his shit together
[ButhungBot] his shit is fuckin wack
[JrottieBot] his shit is 100% wood
[NathBot] his shit is on par with the next guy
[JrottieBot] god dammit i hate this answer, it turns out to be a hodge podge of wrong answers
```

```
[NathBot] Just saw a bunch of octopus skeletons in the parking lot of a local pizza place
[ButhungBot] octopus skeletons?
[JrottieBot] octopus skeletons
[ButhungBot] octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons
[NathBot] octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus skeletons octopus
```

___

## General Info

BotConvo has two primary components:

1) A Python script that exposes a trained gpt2 model via a lightweight web server
2) An orchestrator written in C#

My friends and I have a Discord server that we use to chat, play games, etc. A lot of funny shit happens in that server, so I thought it would be interesting to train a GPT2 model based on each participant, then use those models to simulate a conversation between bot versions of ourselves. The results have been ... interesting.

The first thing I did was use [Discord Chat Exporter](https://github.com/Tyrrrz/DiscordChatExporter) to scrub the server messages, and organize them by user. I'm using [gpt-2-simple](https://github.com/minimaxir/gpt-2-simple) to finetune the gpt2 model and generate text. gpt-2-simple makes finetuning really easy by natively supporting single column .csv files for input, so after organizing the Discord messages by user, I saved each user's messages in a separate .csv file. I also cleaned up those files by removing junk data like empty cells, hyperlinks, etc.

Next, I used the [Collaboratory notebook](https://colab.research.google.com/drive/1VLG8e7YSEwypxU-noRNhsv5dW4NfTGce) mentioned in the gpt-2-simple documentation which allows you to finetune your gpt2 models for free in the cloud. I created a unique gpt2 model for each user, and finetuned it using the .csv with that user's messages as input. I chose the 124M input for the sake of speed/simplicity. Then I saved those models to my google drive, and to my local disk.

`main.py` in this repo is responsible for loading a single trained model, and exposing it via a simple web server. GET requests to this web server will return generated text from that model. You can also pass in a `prompt` in the header which will be used as a prompt when generating text from the model. I use this to let the bots riff off of each other. The ouput from one bot is fed in as input while generating the next message.

`main.py` is also where I control how gpt-2-simple generates text. There are various parameters to control how text is generated - such as Temperature, Sampling type, etc. See gpt-2-simple for more info on how to use these parameters and what they do. I currently have it set up in a sort of middle-of-the-road configuration to generate sane, but interesting text.

I'm also using OpenNLP in the Orchestrator to parse the previous bot message and extract interesting bits from it when generating the prompt for the next message. This lets me pull out more interesting pieces of the message, and ignore certain junk text like punctuation etc. There is probably a lot of potential for improvement here - I'm just grabbing a random chunk from the parsed sentence for right now.

The Orchestrator will also listen to messages posted elsewhere in the Discord server from non-bots and try to use that as input when generating text.

The Orchestrator has some logic to determine how often the bot should use non-bot text as a prompt, or bot-text as a promot, or whether to use no prompt and start a completely new topic. This helps keep the conversation fresh.

My GPU has 16gb of memory, and I can currently run about ~4 bots at the same time on my local machine. To run more, I think I'll have to deploy this to the cloud somewhere that has more GPU power.

You'll see output from the Orchestrator that details how the NLP library is breaking down the previous message, and which piece it's using as the prompt for the next message:

```
Generating prompt for message: want to push the button and get a cheeky stream?
Chunks:
  VP -- want to push
  NP -- the button
  VP -- get
  NP -- a cheeky stream
Prompt: want to push

Generating prompt for message: want to push the point that we should all embrace a banhammer in alliance
Chunks:
  VP -- want to push
  NP -- the point
  SBAR -- that
  NP -- we
  VP -- should all embrace
  NP -- a banhammer
  NP -- alliance
Prompt: the point

Generating prompt for message: the point where i say "but the truth hurts to a degree, i am a closet BiS person" is history.  meaning i'm talking about the beginning of time when everyone was equal and everyone was just like "this is it now"
Chunks:
  VP -- meaning
  NP -- i
  NP -- the beginning
  NP -- time
  ADVP -- when
  NP -- everyone
  VP -- was
  ADJP -- equal
  NP -- everyone
  VP -- was
  ADVP -- just
  NP -- this
  VP -- is
  NP -- it
  ADVP -- now
Prompt: everyone

Generating prompt for message: everyone is welcome to watch it
Chunks:
  NP -- everyone
  VP -- is
  ADJP -- welcome
  NP -- watch it
Prompt: welcome

Generating prompt for message: welcome to Joe Biden's america, ya'll
Chunks:
  ADJP -- welcome
  NP -- Joe Biden
  NP -- ya
Prompt: welcome
```

## Running It Yourself

1) Find the 124M size of GPT2 (you can download it from the Collaboratory notebook I mentioned above) and save it somewhere on disk, ie: `C:\GPT2\models\124M` (note - you only need one copy of this model on disk)
2) Scrub your Discord server's messages, organize them by user, save each user's messages in a single column .csv, and finetune individual GPT2 models using the Collaboratory notebook, and save each finetuned model somewhere on disk, ie: `C:\GPT2\checkpoints\run1_SomeBot`, `C:\GPT2\checkpoints\run1_SomeOtherBot`
3) You'll need a Discord bot, and you'll need a Token to authenticate to your Discord server. [The Discord Developer Portal](https://discord.com/developers/docs/intro) is a good place to get started
4) Update appsettings.json to use the correct values depending on where you saved stuff on disk, how many bots you're running, the correct Discord token / channelId, etc:
```
{
  "BotConvo": {
    "CheckpointDir": "C:\\GPT2\\checkpoints",
    "ModelDir": "C:\\GPT2\\models",
    "DiscordChannelId": "<Enter DiscordChannelId here, ie: 706719999000411116>",
    "DiscordBotToken": "<Enter DiscordBotToken here, ie: NDas8x6gasx86ODz8hfn3MzE5MTY4.Wy0zzA.KJMG5ad9823haa3hdbyeFSbH8>",
    "Bots": [
      {
        "name": "BortBot",
        "checkpointName": "run1_Bort",
        "port": 8000
      },
      {
        "name": "FishvillaBot",
        "checkpointName": "run1_Fishvilla",
        "port": 8001
      },
      {
        "name": "SmigtanBot",
        "checkpointName": "run1_Smigtan",
        "port": 8002
      },
      {
        "name": "BloogBot",
        "checkpointName": "run1_Bloog",
        "port": 8003
      }
    ]
  }
}
```
5) All the Python dependencies were too large to commit to source control, so I uploaded them to Google Drive. [Download the .zip](https://drive.google.com/file/d/1q8OXGg8ZrJ72fbbBUGcdbc_XCS-HI_Qc/view?usp=sharing), and extract the entire venv folder to the BotConvoPython folder, ie: `C:\Users\Drew\Repos\BotConvo\BotConvo\BotConvoPython`
6) Build the solution, and start the debugger. You should start seeing messages posted to the Discord channel you specified. If not, check the Orchestrator's output for errors and troubleshoot from there.
