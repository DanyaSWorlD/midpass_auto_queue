using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace MidpassAutoQueue;

public class TelegramBot
{
    private delegate void NewMessage(string message, long userId);
    private event NewMessage OnNewMessage;

    private TelegramBotClient _botClient;
    private CancellationTokenSource _cancellationTokenSource = new();

    public TelegramBotClient Client => _botClient;

    public TelegramBot(string token)
    {
        _botClient = new TelegramBotClient(token);

        // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cancellationTokenSource.Token
        );
    }

    public async Task<string?> GetResponseAsync(long userId)
    {
        var tcs = new TaskCompletionSource<object>();

        string? response = null;

        NewMessage handler = (m, i) =>
        {
            if (i != userId)
                return;

            response = m;
            tcs.SetResult(null);
        };

        OnNewMessage += handler;

        try
        {
            await tcs.Task;
        }
        finally
        {
            OnNewMessage -= handler;
        }

        return response;
    }

    public async Task SendMessageAsync(long chatId, string messageText)
    {
        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: messageText,
            cancellationToken: _cancellationTokenSource.Token);
    }

    public async Task SendImageAsync(long chatId, string imgPath)
    {
        await _botClient.SendPhotoAsync(
            chatId: chatId,
            photo: InputFile.FromStream(System.IO.File.OpenRead(imgPath)));
    }


    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;
        // Only process text messages
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        OnNewMessage?.Invoke(messageText, chatId);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
}
