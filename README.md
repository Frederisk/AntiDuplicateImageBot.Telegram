## Building it yourself

Before you begin, you need to [apply](https://core.telegram.org/api/obtaining_api_id) for an API_ID and API_HASH for development purposes.

You will also need to apply for a bot from [BotFather](https://t.me/BotFather) to execute, at which point you will receive a BOT_TOKEN. In the bot's settings, enable "Allow Group" and disable "Group Privacy".

First, you need to install the [.NET SDK 8.0](https://dotnet.microsoft.com/) on your device.

Clone this project to your device.

Create a group for debugging, add the bot as a member, and use another bot or tool to determine the GroupID of this group. It is usually a very large negative number; take note of it. Then, modify the ChatId [here](https://github.com/Frederisk/AntiDuplicateImageBot.Telegram/blob/main/AntiDuplicateImageBot%2FProgram.cs#L80-L80) in your files to your GroupID. Alternatively, if you do not want logs, you can skip this and delete this line directly.

Find your own UserID and the UserIDs of anyone else you want to be able to operate your bot, and add them [here](https://github.com/Frederisk/AntiDuplicateImageBot.Telegram/blob/main/AntiDuplicateImageBot%2FProgram.cs#L29-L29).

Use environment variables to pass the necessary tokens obtained in the steps above, similar to this, replacing the values on the right with your own:

```bash
export BOT_TOKEN="1234567890:AbCdEfGhIjKlMn"
export API_ID="000000"
export API_HASH="1234567890abcdef"
```

To start the bot, the method is simple. First, use a command-line tool to cd into the directory where you downloaded the project, and then execute the command:

```bash
dotnet run --configuration release --project AntiDuplicateImageBot
```

The bot will start running. You can add the bot to any group where you want to enable the de-duplication feature. After adding it, send /start@YourBotName. If everything is normal, the bot will send a welcome message.

You have two commands to start de-duplication in a group: /init@YourBotName and /init_sample@YourBotName. The choice depends on whether you want to process and de-duplicate images already sent in the group's history. If you only need to de-duplicate new messages in the future, execute the init_sample command.

It should be noted that de-duplicating images from the history is a very resource-intensive operation. If you have already sent over a thousand photos or a very large number of messages in the history, you may need to wait a very, very long time for the bot to finish processing.