using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Infrastructure.Enums;
using XinjingdailyBot.Infrastructure.Extensions;
using XinjingdailyBot.Infrastructure.Localization;
using XinjingdailyBot.Interface.Bot.Common;
using XinjingdailyBot.Interface.Data;
using XinjingdailyBot.Interface.Helper;
using XinjingdailyBot.Model.Models;

namespace XinjingdailyBot.Command;

/// <summary>
/// 投稿命令
/// </summary>
[AppService(LifeTime.Scoped)]
internal class PostCommand
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUserService _userService;
    private readonly IChannelService _channelService;
    private readonly IPostService _postService;
    private readonly IMarkupHelperService _markupHelperService;
    private readonly IAttachmentService _attachmentService;
    private readonly ITextHelperService _textHelperService;
    private readonly IMediaGroupService _mediaGroupService;

    public PostCommand(
        ITelegramBotClient botClient,
        IUserService userService,
        IChannelService channelService,
        IPostService postService,
        IMarkupHelperService markupHelperService,
        IAttachmentService attachmentService,
        ITextHelperService textHelperService,
        IMediaGroupService mediaGroupService)
    {
        _botClient = botClient;
        _userService = userService;
        _channelService = channelService;
        _postService = postService;
        _markupHelperService = markupHelperService;
        _attachmentService = attachmentService;
        _textHelperService = textHelperService;
        _mediaGroupService = mediaGroupService;
    }

    /// <summary>
    /// 投稿消息处理
    /// </summary>
    /// <param name="dbUser"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    [QueryCmd("POST", EUserRights.SendPost, Description = "投稿消息处理")]
    public async Task HandlePostQuery(Users dbUser, CallbackQuery query)
    {
        var message = query.Message!;
        var post = await _postService.FetchPostFromCallbackQuery(query);

        if (post == null)
        {
            await _botClient.AutoReplyAsync("未找到稿件", query);
            await _botClient.EditMessageReplyMarkupAsync(message, null);
            return;
        }

        if (post.Status != EPostStatus.Padding)
        {
            await _botClient.AutoReplyAsync("请不要重复操作", query);
            await _botClient.EditMessageReplyMarkupAsync(message, null);
            return;
        }

        if (post.PosterUID != dbUser.UserID)
        {
            await _botClient.AutoReplyAsync("这不是你的稿件", query);
            return;
        }

        switch (query.Data)
        {
            case "post anymouse":
                await SetAnymouse(post, query);
                break;
            case "post cancel":
                await CancelPost(post, query);
                break;
            case "post confirm":
                await ConfirmPost(post, dbUser, query);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 设置或者取消匿名
    /// </summary>
    /// <param name="post"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    private async Task SetAnymouse(NewPosts post, CallbackQuery query)
    {
        await _botClient.AutoReplyAsync("可以使用命令 /anymouse 切换默认匿名投稿", query);

        bool anonymous = !post.Anonymous;
        post.Anonymous = anonymous;
        post.ModifyAt = DateTime.Now;
        await _postService.Updateable(post).UpdateColumns(static x => new { x.Anonymous, x.ModifyAt }).ExecuteCommandAsync();

        var keyboard = _markupHelperService.PostKeyboard(anonymous);
        await _botClient.EditMessageReplyMarkupAsync(query.Message!, keyboard);
    }

    /// <summary>
    /// 取消投稿
    /// </summary>
    /// <param name="post"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    private async Task CancelPost(NewPosts post, CallbackQuery query)
    {
        post.Status = EPostStatus.Cancel;
        post.ModifyAt = DateTime.Now;

        await _postService.Updateable(post).UpdateColumns(static x => new { x.Status, x.ModifyAt }).ExecuteCommandAsync();

        await _botClient.EditMessageTextAsync(query.Message!, Langs.PostCanceled, replyMarkup: null);

        await _botClient.AutoReplyAsync(Langs.PostCanceled, query);
    }

    /// <summary>
    /// 确认投稿
    /// </summary>
    /// <param name="dbUser"></param>
    /// <param name="post"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    private async Task ConfirmPost(NewPosts post, Users dbUser, CallbackQuery query)
    {
        if (await _postService.CheckPostLimit(dbUser, null, query) == false)
        {
            return;
        }

        Message reviewMsg;
        if (!post.IsMediaGroup)
        {
            reviewMsg = await _botClient.ForwardMessageAsync(_channelService.ReviewGroup.Id, post.OriginChatID, (int)post.OriginMsgID);
        }
        else
        {
            var attachments = await _attachmentService.Queryable().Where(x => x.PostID == post.Id).ToListAsync();
            var group = new IAlbumInputMedia[attachments.Count];
            for (int i = 0; i < attachments.Count; i++)
            {
                var attachmentType = attachments[i].Type;
                if (attachmentType == MessageType.Unknown)
                {
                    attachmentType = post.PostType;
                }

                group[i] = attachmentType switch {
                    MessageType.Photo => new InputMediaPhoto(new InputFileId(attachments[i].FileID)) {
                        Caption = i == 0 ? post.Text : null,
                        ParseMode = ParseMode.Html
                    },
                    MessageType.Audio => new InputMediaAudio(new InputFileId(attachments[i].FileID)) {
                        Caption = i == 0 ? post.Text : null,
                        ParseMode = ParseMode.Html
                    },
                    MessageType.Video => new InputMediaVideo(new InputFileId(attachments[i].FileID)) {
                        Caption = i == 0 ? post.Text : null,
                        ParseMode = ParseMode.Html
                    },
                    MessageType.Document => new InputMediaDocument(new InputFileId(attachments[i].FileID)) {
                        Caption = i == attachments.Count - 1 ? post.Text : null,
                        ParseMode = ParseMode.Html
                    },
                    _ => throw new Exception("未知的稿件类型"),
                };
            }
            var messages = await _botClient.SendMediaGroupAsync(_channelService.ReviewGroup, group);
            reviewMsg = messages.First();
            post.ReviewMediaGroupID = reviewMsg.MediaGroupId ?? "";

            //记录媒体组消息
            await _mediaGroupService.AddPostMediaGroup(messages);
        }

        string msg = _textHelperService.MakeReviewMessage(dbUser, post.Anonymous);

        bool? hasSpoiler = post.CanSpoiler ? post.HasSpoiler : null;
        var keyboard = _markupHelperService.ReviewKeyboardA(post.Tags, hasSpoiler);

        var manageMsg = await _botClient.SendTextMessageAsync(_channelService.ReviewGroup, msg, parseMode: ParseMode.Html, disableWebPagePreview: true, replyToMessageId: reviewMsg.MessageId, replyMarkup: keyboard, allowSendingWithoutReply: true);

        post.ReviewChatID = reviewMsg.Chat.Id;
        post.ReviewMsgID = reviewMsg.MessageId;
        post.ReviewActionChatID = manageMsg.Chat.Id;
        post.ReviewActionMsgID = manageMsg.MessageId;
        post.Status = EPostStatus.Reviewing;
        post.ModifyAt = DateTime.Now;
        await _postService.Updateable(post).UpdateColumns(static x => new {
            x.ReviewChatID,
            x.ReviewMsgID,
            x.ReviewActionChatID,
            x.ReviewActionMsgID,
            x.ReviewMediaGroupID,
            x.Status,
            x.ModifyAt
        }).ExecuteCommandAsync();

        await _botClient.AutoReplyAsync(Langs.PostSendSuccess, query);
        await _botClient.EditMessageTextAsync(query.Message!, Langs.ThanksForSendingPost, replyMarkup: null);

        dbUser.PostCount++;
        dbUser.ModifyAt = DateTime.Now;
        await _userService.Updateable(dbUser).UpdateColumns(static x => new { x.PostCount, x.ModifyAt }).ExecuteCommandAsync();
    }
}
