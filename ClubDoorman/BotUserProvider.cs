using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ClubDoorman;

internal class BotUserProvider
{
    private readonly ITelegramBotClient _bot;
    private readonly SemaphoreSlim _meLock = new(1, 1);
    private User? _me;

    public BotUserProvider(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task<User> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (_me != null)
            return _me;

        await _meLock.WaitAsync(cancellationToken);
        try
        {
            if (_me == null)
                _me = await _bot.GetMe(cancellationToken: cancellationToken);
        }
        finally
        {
            _meLock.Release();
        }

        return _me;
    }
}
