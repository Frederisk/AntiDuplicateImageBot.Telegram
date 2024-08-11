using Microsoft.Data.Sqlite;

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using Shipwreck.Phash;
//using Shipwreck.Phash.Bitmaps;

using WMessage = WTelegram.Types.Message;

namespace AntiDuplicateImageBot;

internal class Program {
    private static SqliteConnection groupConnection = null!;
    private static String? botName;

    private static Int64?[] trustedUsers = [
        691216126, // FuckWikipedia
    ];

    private static async Task Main(String[] _) {
        String? botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
        Int32 apiId = Int32.Parse(Environment.GetEnvironmentVariable("API_ID")!);
        String apiHash = Environment.GetEnvironmentVariable("API_HASH")!;

        if (String.IsNullOrWhiteSpace(botToken) || String.IsNullOrWhiteSpace(apiHash)) {
            Console.WriteLine("Token is empty!");
            return;
        }
        using SqliteConnection connection = new("Data Source=AntiDuplicateImageBot.sqlite");

        await SetupDatabase();

        WTelegramBotClient bot = new(botToken, apiId, apiHash, connection);
        User me = await bot.GetMeAsync();
        using CancellationTokenSource cancellationTokenSource = new();
        bot.StartReceiving(HandleUpdateAsync, PollingErrorHandler, null, cancellationTokenSource.Token);
        await LoggingAsync(bot, $"Start listening for @{me.Username}");
        botName = me.Username;
        while (true) {
            var exit = Console.ReadLine();
            if (exit is "exit") {
                cancellationTokenSource.Cancel();
                bot.Dispose();
                groupConnection.Dispose();
                break;
            }
        }
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct) {
        try {
            await (update.Type switch {
                UpdateType.Message => BotOnMessageReceived(bot, update.Message!),
                _ => Task.CompletedTask,
            });
        } catch (Exception ex) {
            await LoggingAsync(bot, $"Exception while handling {update.Type}: {ex}");
        }
    }

    private static async Task PollingErrorHandler(ITelegramBotClient bot, Exception ex, CancellationToken ct) {
        await LoggingAsync(bot, $"Exception while polling for updates: {ex}");
        return;
    }

    private static async Task LoggingAsync(ITelegramBotClient bot, String message) {
        try {
            await bot.SendTextMessageAsync(new ChatId(-1002201062127), message);
        } catch (Exception ex) {
            Console.WriteLine($"Exception while logging: {ex}");
        }
    }

    // Logic

    private static async Task SetupDatabase() {
        groupConnection = new("Data Source=AntiDuplicateInfo.sqlite");
        groupConnection.Open();
        using var command = groupConnection.CreateCommand();
        command.CommandText = """
            Select name From sqlite_master
             Where type='table'
               And name In ('allow_group', 'message_info');
            """;
        using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!reader.HasRows) {
            await reader.DisposeAsync();
            command.CommandText = """
                Create Table allow_group (
                    group_id Integer Not Null
                );
                Create Table message_info (
                    group_id Integer Not Null,
                    message_id Integer Not Null,
                    media_group_id Text Null,
                    photo_hash Blob Not Null
                    -- ,file_unique_id Text Null
                );
                """;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task BotOnMessageReceived(ITelegramBotClient bot, Message message) {
        Console.WriteLine("Updated");
        if (message.Type is MessageType.Photo) {
            // Handle Photo
            await ProcessPhotoAsync(bot, message);
        } else if (message.Text is not null) {
            String command = message.Text;
            if (command.StartsWith($"/start@{botName}")) {
                await bot.SendTextMessageAsync(message.Chat.Id, "Hello! This is Anti-Duplicate Image Bot!");
            } else if (command.StartsWith($"/init@{botName}") || command.StartsWith($"/init_sample@{botName}")) {
                if (Array.IndexOf<Int64?>(trustedUsers, message.From?.Id) >= 0) {
                    if (bot is not WTelegramBotClient wBot) {
                        await bot.SendTextMessageAsync(message.Chat.Id, $"Oops, not {typeof(WTelegramBotClient).Name}");
                    } else {
                        await InitAsync(wBot, message, command.StartsWith($"/init_sample@{botName}"));
                    }
                } else {
                    await bot.SendTextMessageAsync(message.Chat.Id, "No, I don't trust you. Please look for @FuckWikipedia.");
                }
            }
        }
    }

    private static async Task InitAsync(WTelegramBotClient bot, Message message, Boolean isSample) {
        Chat chat = message.Chat;
        await bot.SendTextMessageAsync(message.Chat.Id, "Insert into database");
        using var command = groupConnection.CreateCommand();
        command.CommandText = @"
            Select group_id
              From allow_group
             Where group_id = $id;";
        command.Parameters.AddWithValue("$id", chat.Id);
        using (var reader = await command.ExecuteReaderAsync()) {
            if (reader.HasRows) {
                await bot.SendTextMessageAsync(message.Chat.Id, "Already exist.");
                return;
            }
        }

        command.Parameters.Clear();
        command.CommandText = @"Insert Into allow_group (group_id) Values ($id);";
        command.Parameters.AddWithValue("$id", chat.Id);
        await command.ExecuteNonQueryAsync();

        if (isSample) { return; }
        // Loading all messages and record hash.
        await bot.SendTextMessageAsync(message.Chat.Id, "Loading all messages...");
        List<WMessage> allMessages = [];
        Int32 begin = 1;
        Int32 end = message.MessageId;
        IEnumerable<Int32> range;
        while (true) {
            if (begin + 100 > end) {
                range = Enumerable.Range(begin, end - begin); // not include the end itself.
                allMessages.AddRange(await bot.GetMessagesById(chat, range));
                break;
            }
            range = Enumerable.Range(begin, 100);
            allMessages.AddRange(await bot.GetMessagesById(chat, range));
            begin += 100;
        }
        await bot.SendTextMessageAsync(message.Chat.Id, "Processing...");
        var imageMessages = from msg in allMessages
                            where msg.Type is MessageType.Photo
                            select msg;
        foreach (var img_msg in imageMessages) {
            await ProcessPhotoAsync(bot, img_msg);
        }

        await bot.SendTextMessageAsync(message.Chat.Id, $"Done, MaxTotal {message.MessageId}, Total {allMessages.Count}, Photo {imageMessages.Count()}");
    }

    private static async Task ProcessPhotoAsync(ITelegramBotClient bot, Message message) {
        using var command = groupConnection.CreateCommand();
        command.CommandText = """
            Select group_id
              From allow_group
             Where group_id = $id;
            """;
        command.Parameters.AddWithValue("$id", message.Chat.Id);
        using (var reader = await command.ExecuteReaderAsync()) {
            if (!reader.HasRows) {
                await bot.SendTextMessageAsync(message.Chat.Id, "Unregistered group.");
                return;
            }
        }
        command.Parameters.Clear();

        var fileId = message.Photo!.MaxBy(p => p.FileSize)!.FileId;
        //message.Photo[0].
        var file = await bot.GetFileAsync(fileId);
        using MemoryStream fileStream = new();
        await bot.DownloadFileAsync(file.FilePath!, fileStream);
        Bitmap bitmap = new(Image.FromStream(fileStream));
        Digest hash = ImagePhash.ComputeDigest(bitmap.ToLuminanceImage());
        await LoggingAsync(bot, $"File {file.FilePath} downloaded.");

        command.CommandText = """
            Select message_id, media_group_id, photo_hash
              From message_info
             Where group_id = $id;
            """;
        command.Parameters.AddWithValue("$id", message.Chat.Id);
        Double maxScore = 0;
        using (var reader = command.ExecuteReader()) {
            while (reader.Read()) {
                Int32 message_id = reader.GetInt32(0);
                String? media_group_id = reader.IsDBNull(1) ? null : reader.GetString(1);
                Byte[] hash_raw = new Byte[40];
                reader.GetBytes(2, 0, hash_raw, 0, 40);
                Digest hash_in = new Digest {
                    Coefficients = hash_raw,
                };
                var score = ImagePhash.GetCrossCorrelation(hash_in, hash);
                maxScore = Math.Max(score, maxScore);
                if (score >= 0.9) {
                    if (message.MediaGroupId is null || message.MediaGroupId != media_group_id) {
                        var groupStr = message.Chat.Id.ToString();
                        if (groupStr.StartsWith("-100")) {
                            groupStr = groupStr[4..];
                        } else if (groupStr.StartsWith('-')) {
                            groupStr = groupStr[1..];
                        }
                        await bot.SendTextMessageAsync(message.Chat.Id, $"Score: {score}, https://t.me/c/{groupStr}/{message_id}", replyParameters: message);
                    }
                }
            }
        }
        command.Parameters.Clear();

        if (maxScore <= 0.999) {
            command.CommandText = """
                Insert Into message_info (group_id, message_id, media_group_id, photo_hash)
                     Values ($group_id, $message_id, $media_group_id, $photo_hash);
                """;
            command.Parameters.AddWithValue("$group_id", message.Chat.Id);
            command.Parameters.AddWithValue("$message_id", message.MessageId);
            command.Parameters.AddWithValue("$media_group_id", message.MediaGroupId as Object ?? DBNull.Value);
            command.Parameters.AddWithValue("$photo_hash", hash.Coefficients);
            await command.ExecuteNonQueryAsync();
        }
    }
}
