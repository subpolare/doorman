using System.Runtime.Caching;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ClubDoorman;

internal class AdminCommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly UserManager _userManager;
    private readonly BadMessageManager _badMessageManager;
    private readonly SpamHamClassifier _classifier;
    private readonly Config _config;
    private readonly AiChecks _aiChecks;
    private readonly RecentMessagesStorage _recentMessagesStorage;
    private readonly ILogger<AdminCommandHandler> _logger;
    private User? _me;

    public AdminCommandHandler(
        ITelegramBotClient bot,
        UserManager userManager,
        BadMessageManager badMessageManager,
        SpamHamClassifier classifier,
        Config config,
        AiChecks aiChecks,
        RecentMessagesStorage recentMessagesStorage,
        ILogger<AdminCommandHandler> logger
    )
    {
        _bot = bot;
        _userManager = userManager;
        _badMessageManager = badMessageManager;
        _classifier = classifier;
        _config = config;
        _aiChecks = aiChecks;
        _recentMessagesStorage = recentMessagesStorage;
        _logger = logger;
    }

    public async Task HandleAdminCallback(string cbData, CallbackQuery cb)
    {
        var chat = cb.Message?.Chat.Id;
        var admChat = chat ?? _config.AdminChatId;
        var split = cbData.Split('_').ToList();
        if (split.Count > 1)
            switch (split[0])
            {
                case "approve":
                    if (!long.TryParse(split[1], out var approveUserId))
                        return;
                    await _userManager.Approve(approveUserId);
                    await _bot.SendMessage(
                        admChat,
                        $"{Utils.FullName(cb.From)} добавил пользователя в список доверенных",
                        replyParameters: cb.Message?.MessageId
                    );
                    break;
                case "attOk":
                    if (!long.TryParse(split[1], out var attOkUserId))
                        return;
                    await _aiChecks.MarkUserOkay(attOkUserId);
                    await _bot.SendMessage(
                        admChat,
                        $"{Utils.FullName(cb.From)} добавил пользователя в список тех, чей профиль не в блеклисте (но ТЕКСТЫ его сообщений всё ещё проверяются)",
                        replyParameters: cb.Message?.MessageId
                    );
                    break;
                case "ban":
                    {
                        if (split.Count < 3 || !long.TryParse(split[1], out var chatId) || !long.TryParse(split[2], out var userId))
                            return;
                        try
                        {
                            _logger.LogDebug("Someone pressed button to ban chat {ChatId} user {UserId}", chatId, userId);
                            await _bot.BanChatMember(chatId, userId);
                            await _bot.SendMessage(
                                admChat,
                                $"{Utils.FullName(cb.From)} забанил, сообщение добавлено в список авто-бана",
                                replyParameters: cb.Message?.MessageId
                            );
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Unable to ban");
                            await _bot.SendMessage(
                                admChat,
                                $"Не могу забанить. Что-то не так... Сходите и забаньте руками",
                                replyParameters: cb.Message?.MessageId
                            );
                        }
                        await DeleteAllRecentFrom(chatId, userId);
                    }
                    break;
                case "mute": 
                    {
                        if (split.Count < 3 || !long.TryParse(split[1], out var chatId) || !long.TryParse(split[2], out var userId))
                            return;
                        try
                        {
                            _logger.LogDebug("Someone pressed button to read-only chat {ChatId} user {UserId}", chatId, userId);
                            var perms = new ChatPermissions
                            {
                                CanSendMessages = false,
                                CanSendAudios = false,
                                CanSendDocuments = false,
                                CanSendPhotos = false,
                                CanSendVideos = false,
                                CanSendVideoNotes = false,
                                CanSendVoiceNotes = false,
                                CanSendPolls = false,
                                CanSendOtherMessages = false,
                                CanAddWebPagePreviews = false,
                                CanChangeInfo = false,
                                CanInviteUsers = false,
                                CanPinMessages = false,
                                CanManageTopics = false,
                            };
                            await _bot.RestrictChatMember(
                                chatId: chatId,
                                userId: userId,
                                permissions: perms,
                                useIndependentChatPermissions: true
                            )
                            await _bot.SendMessage(
                                admChat,
                                $"Выдал пользователю read-only. Надеюсь, он расстроился",
                                replyParameters: cb.Message?.MessageId
                            )
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Unable to make read-only");
                            await _bot.SendMessage(
                                admChat,
                                $"Не могу выдать read-only. Что-то не так... Сделайте это руками",
                                replyParameters: cb.Message?.MessageId
                            );
                        }
                        await DeleteAllRecentFrom(chatId, fromChannelId);
                    }
                case "banchan":
                    {
                        if (split.Count < 3 || !long.TryParse(split[1], out var chatId) || !long.TryParse(split[2], out var fromChannelId))
                            return;
                        try
                        {
                            _logger.LogDebug("Someone pressed button to ban chat {ChatId} chan {ChannelId}", chatId, fromChannelId);
                            await _bot.BanChatSenderChat(chatId, fromChannelId);
                            await _bot.SendMessage(
                                admChat,
                                $"{Utils.FullName(cb.From)} забанил, сообщение добавлено в список авто-бана",
                                replyParameters: cb.Message?.MessageId
                            );
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Unable to ban");
                            await _bot.SendMessage(
                                admChat,
                                $"Не могу забанить. Что-то не так... Сходите и забаньте руками",
                                replyParameters: cb.Message?.MessageId
                            );
                        }
                        await DeleteAllRecentFrom(chatId, fromChannelId);
                    }
                    break;
            }

        var msg = cb.Message;
        if (msg != null)
            await _bot.EditMessageReplyMarkup(msg.Chat.Id, msg.MessageId);
    }

    private async Task DeleteAllRecentFrom(long chatId, long fromChannelId)
    {
        var recent = _recentMessagesStorage.Get(fromChannelId, chatId);
        foreach (var message in recent)
        {
            var text = message?.Caption ?? message?.Text;
            if (!string.IsNullOrWhiteSpace(text))
                await _badMessageManager.MarkAsBad(text);
        }
        try
        {
            await _bot.DeleteMessages(chatId, recent.Select(x => x.MessageId));
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Cannot delete messages in ban admin callback");
        }
    }

    public async Task AdminChatMessage(Message message)
    {
        // TODO: this is not ideal, share getter with MessageProcessor
        _me ??= await _bot.GetMe();
        if (message.Text != null && message.Text.StartsWith("/unban"))
        {
            var split = message.Text.Split(' ');
            if (split.Length > 1 && long.TryParse(split[1], out var userId))
            {
                await _userManager.Unban(userId);
                await _bot.SendMessage(message.Chat.Id, $"Unbanned user {userId}", replyParameters: message);
                return;
            }
        }

        if (message is { ReplyToMessage: { } replyToMessage, Text: "/spam" or "/ham" or "/check" })
        {
            if (replyToMessage.From?.Id == _me.Id && replyToMessage.ForwardDate == null)
            {
                await _bot.SendMessage(
                    message.Chat.Id,
                    "Похоже, что вы промахнулись и реплайнули на сообщение бота, а не пользователя",
                    replyParameters: replyToMessage
                );
                return;
            }
            var text = replyToMessage.Text ?? replyToMessage.Caption;

            if (replyToMessage.Quote?.Text != null)
                text = $"{replyToMessage.Quote.Text} {text}";

            if (!string.IsNullOrWhiteSpace(text))
            {
                switch (message.Text)
                {
                    case "/check":
                    {
                        var emojis = SimpleFilters.TooManyEmojis(text);
                        var normalized = TextProcessor.NormalizeText(text);
                        var lookalike = SimpleFilters.FindAllRussianWordsWithLookalikeSymbolsInNormalizedText(normalized);
                        var hasStopWords = SimpleFilters.HasStopWords(normalized, out var regexStopWords);
                        var (spam, score) = await _classifier.IsSpam(normalized);
                        var lookAlikeMsg = lookalike.Count == 0 ? "отсутствуют" : string.Join(", ", lookalike);
                        var msg =
                            $"Много эмодзи: {emojis}{Environment.NewLine}"
                            + $"Найдены стоп-слова: {hasStopWords} (regex: {regexStopWords}){Environment.NewLine}"
                            + $"Маскирующиеся слова: {lookAlikeMsg}{Environment.NewLine}"
                            + $"AI-классификатор спама: {spam} (score: {score}){Environment.NewLine}{Environment.NewLine}"
                            + $"Нормализованный текст: {normalized}";
                        await _bot.SendMessage(message.Chat.Id, msg);
                        break;
                    }
                    case "/spam":
                        await _classifier.AddSpam(text);
                        await _badMessageManager.MarkAsBad(text);
                        await _bot.SendMessage(
                            message.Chat.Id,
                            "Сообщение добавлено как пример спама в датасет, а так же в список авто-бана",
                            replyParameters: replyToMessage
                        );
                        break;
                    case "/ham":
                        await _classifier.AddHam(text);
                        await _bot.SendMessage(
                            message.Chat.Id,
                            "Сообщение добавлено как пример НЕ-спама в датасет",
                            replyParameters: replyToMessage
                        );
                        break;
                }
            }
        }
    }
}
