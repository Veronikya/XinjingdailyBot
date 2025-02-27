using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using XinjingdailyBot.Infrastructure;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Infrastructure.Extensions;
using XinjingdailyBot.Interface.Bot.Common;

namespace XinjingdailyBot.Service.Bot.Common;

[AppService(typeof(IChannelService), LifeTime.Singleton)]

internal class ChannelService : IChannelService
{
    private readonly ITelegramBotClient _botClient;
    private readonly OptionsSetting _optionsSetting;
    private readonly ILogger<ChannelService> _logger;

    public Chat ReviewGroup { get; private set; } = new();
    public Chat ReviewLogChannel { get; private set; } = new();
    public Chat CommentGroup { get; private set; } = new();
    public Chat SubGroup { get; private set; } = new();
    public Chat SecondCommentGroup { get; private set; } = new();
    public Chat AcceptChannel { get; private set; } = new();
    public Chat? SecondChannel { get; private set; }
    public Chat RejectChannel { get; private set; } = new();
    public User BotUser { get; private set; } = new();

    public ChannelService(
        ILogger<ChannelService> logger,
        ITelegramBotClient botClient,
        IOptions<OptionsSetting> optionsSetting)
    {
        _logger = logger;
        _botClient = botClient;
        _optionsSetting = optionsSetting.Value;
    }

    public async Task InitChannelInfo()
    {
        BotUser = await _botClient.GetMeAsync();

        _logger.LogInformation("机器人信息: {Id} {nickName} @{userName}", BotUser.Id, BotUser.FullName(), BotUser.Username);

        var channelOption = _optionsSetting.Channel;

        try
        {
            AcceptChannel = await _botClient.GetChatAsync(channelOption.AcceptChannel);
            _logger.LogInformation("稿件发布频道: {chatProfile}", AcceptChannel.ChatProfile());
        }
        catch
        {
            _logger.LogError("未找到指定的稿件发布频道, 请检查拼写是否正确");
            throw;
        }
        try
        {
            RejectChannel = await _botClient.GetChatAsync(channelOption.RejectChannel);
            _logger.LogInformation("拒稿存档频道: {chatProfile}", RejectChannel.ChatProfile());
        }
        catch
        {
            _logger.LogError("未找到指定的拒稿存档频道, 请检查拼写是否正确");
            throw;
        }
        if (!string.IsNullOrEmpty(channelOption.SecondChannel))
        {
            try
            {
                SecondChannel = await _botClient.GetChatAsync(channelOption.SecondChannel);
                _logger.LogInformation("第二发布频道: {chatProfile}", SecondChannel.ChatProfile());
            }
            catch
            {
                _logger.LogError("未找到指定的稿件第二发布频道, 请检查拼写是否正确");
            }
        }

        try
        {
            if (long.TryParse(channelOption.ReviewGroup, out var groupId))
            {
                ReviewGroup = await _botClient.GetChatAsync(groupId);
            }
            else
            {
                ReviewGroup = await _botClient.GetChatAsync(channelOption.ReviewGroup);
            }
            _logger.LogInformation("审核群组: {chatProfile}", ReviewGroup.ChatProfile());
        }
        catch
        {
            _logger.LogError("未找到指定的审核群组, 可以使用 /groupinfo 命令获取群组信息");
            ReviewGroup = new Chat { Id = -1 };
        }

        if (channelOption.UseReviewLogMode)
        {
            //todo
        }

        try
        {
            if (long.TryParse(channelOption.CommentGroup, out var subGroupId))
            {
                CommentGroup = await _botClient.GetChatAsync(subGroupId);
            }
            else
            {
                CommentGroup = await _botClient.GetChatAsync(channelOption.CommentGroup);
            }
            _logger.LogInformation("评论区群组: {chatProfile}", CommentGroup.ChatProfile());
        }
        catch
        {
            _logger.LogError("未找到指定的评论区群组, 可以使用 /groupinfo 命令获取群组信息");
        }

        try
        {
            if (long.TryParse(channelOption.SubGroup, out var subGroupId))
            {
                SubGroup = await _botClient.GetChatAsync(subGroupId);
            }
            else
            {
                SubGroup = await _botClient.GetChatAsync(channelOption.SubGroup);
            }
            _logger.LogInformation("频道子群组: {chatProfile}", SubGroup.ChatProfile());
        }
        catch
        {
            _logger.LogError("未找到指定的闲聊群组, 可以使用 /groupinfo 命令获取群组信息");
            SubGroup = new Chat { Id = -1 };
        }

        try
        {
            if (long.TryParse(channelOption.SecondCommentGroup, out var subGroupId))
            {
                SecondCommentGroup = await _botClient.GetChatAsync(subGroupId);
            }
            else
            {
                SecondCommentGroup = await _botClient.GetChatAsync(channelOption.SecondCommentGroup);
            }
            _logger.LogInformation("第二频道评论区群组: {chatProfile}", SecondCommentGroup.ChatProfile());
        }
        catch
        {
            _logger.LogError("未找到指定的闲聊群组, 可以使用 /groupinfo 命令获取群组信息");
            SecondCommentGroup = new Chat { Id = -1 };
        }

        if (SubGroup.Id == -1 && CommentGroup.Id != -1)
        {
            SubGroup = CommentGroup;
        }
        else if (CommentGroup.Id == -1 && SubGroup.Id != -1)
        {
            CommentGroup = SubGroup;
        }
    }

    public bool IsChannelMessage(long chatId)
    {
        return chatId == AcceptChannel.Id || chatId == SecondChannel?.Id || chatId == RejectChannel.Id;
    }
    public bool IsChannelMessage(Chat chat)
    {
        return IsChannelMessage(chat.Id);
    }

    public bool IsGroupMessage(long chatId)
    {
        return chatId == SubGroup.Id || chatId == CommentGroup.Id || chatId == SecondCommentGroup.Id;
    }
    public bool IsGroupMessage(Chat chat)
    {
        return IsGroupMessage(chat.Id);
    }

    public bool IsReviewMessage(long chatId)
    {
        return chatId == ReviewGroup.Id;
    }
    public bool IsReviewMessage(Chat chat)
    {
        return IsReviewMessage(chat.Id);
    }
    public bool HasSecondChannel => SecondChannel != null;
}
