using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using XinjingdailyBot.Infrastructure;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Infrastructure.Enums;
using XinjingdailyBot.Interface.Data;

namespace XinjingdailyBot.Tasks;

/// <summary>
/// 过期稿件处理
/// </summary>
[Job("0 0 0 * * ?")]
internal class ExpiredPostsTask : IJob
{
    private readonly ILogger<ExpiredPostsTask> _logger;
    private readonly IPostService _postService;
    private readonly IUserService _userService;
    private readonly ITelegramBotClient _botClient;
    /// <summary>
    /// 稿件过期时间
    /// </summary>
    private readonly TimeSpan PostExpiredTime;

    public ExpiredPostsTask(
        ILogger<ExpiredPostsTask> logger,
        IPostService postService,
        IUserService userService,
        ITelegramBotClient botClient,
        IOptions<OptionsSetting> options)
    {
        _logger = logger;
        _postService = postService;
        _userService = userService;
        _botClient = botClient;

        var expiredTime = options.Value.Post.PostExpiredTime;
        PostExpiredTime = expiredTime > 0 ? TimeSpan.FromDays(options.Value.Post.PostExpiredTime) : TimeSpan.Zero;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (PostExpiredTime.TotalDays == 0)
        {
            return;
        }

        _logger.LogInformation("开始定时任务, 清理过期稿件任务");

        var expiredDate = DateTime.Now - PostExpiredTime;

        //获取有过期稿件的用户
        var userIDList = await _postService.Queryable()
            .Where(x => (x.Status == EPostStatus.Padding || x.Status == EPostStatus.Reviewing) && x.ModifyAt < expiredDate)
            .Distinct().Select(x => x.PosterUID).ToListAsync();

        if (!userIDList.Any())
        {
            _logger.LogInformation("结束定时任务, 没有需要清理的过期稿件");
            return;
        }

        _logger.LogInformation("成功获取 {Count} 个有过期稿件的用户", userIDList.Count);

        foreach (var userID in userIDList)
        {
            //获取过期投稿
            var paddingPosts = await _postService.Queryable()
                .Where(x => x.PosterUID == userID && (x.Status == EPostStatus.Padding || x.Status == EPostStatus.Reviewing) && x.ModifyAt < expiredDate)
                .ToListAsync();

            if (!paddingPosts.Any())
            {
                continue;
            }

            int cTmout = 0, rTmout = 0;
            foreach (var post in paddingPosts)
            {
                if (post.Status == EPostStatus.Padding)
                {
                    post.Status = EPostStatus.ConfirmTimeout;
                    cTmout++;
                }
                else
                {
                    post.Status = EPostStatus.ReviewTimeout;
                    rTmout++;
                }
                post.ModifyAt = DateTime.Now;

                await _postService.Updateable(post).UpdateColumns(static x => new { x.Status, x.ModifyAt }).ExecuteCommandAsync();
            }

            var user = await _userService.Queryable().FirstAsync(x => x.UserID == userID);

            if (user == null)
            {
                _logger.LogInformation("清理了 {userID} 的 {cTmout} / {rTmout} 条确认/审核超时投稿", userID, cTmout, rTmout);
            }
            else
            {
                _logger.LogInformation("清理了 {user} 的 {cTmout} / {rTmout} 条确认/审核超时投稿", user.ToString(), cTmout, rTmout);

                //满足条件则通知投稿人
                //1.未封禁
                //2.有PrivateChatID
                //3.启用通知
                if (!user.IsBan && user.PrivateChatID > 0 && user.Notification)
                {
                    var sb = new StringBuilder();

                    if (cTmout > 0)
                    {
                        sb.AppendLine($"你有 <code>{cTmout}</code> 份稿件因为确认超时被清理");
                    }

                    if (rTmout > 0)
                    {
                        sb.AppendLine($"你有 <code>{rTmout}</code> 份稿件因为审核超时被清理");
                    }

                    try
                    {
                        await _botClient.SendTextMessageAsync(user.PrivateChatID, sb.ToString(), parseMode: ParseMode.Html, disableNotification: true);
                        await Task.Delay(500);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "通知消息发送失败, 自动禁用更新");
                        user.PrivateChatID = -1;
                        await Task.Delay(5000);
                    }
                }

                user.ExpiredPostCount += rTmout;
                user.ModifyAt = DateTime.Now;

                //更新用户表
                await _userService.Updateable(user).UpdateColumns(static x => new {
                    x.PrivateChatID,
                    x.ExpiredPostCount,
                    x.ModifyAt
                }).ExecuteCommandAsync();
            }
        }
    }
}
