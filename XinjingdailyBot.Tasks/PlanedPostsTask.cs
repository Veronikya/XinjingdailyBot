using Microsoft.Extensions.Logging;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Infrastructure.Enums;
using XinjingdailyBot.Interface.Data;

namespace XinjingdailyBot.Tasks;

/// <summary>
/// 定期发布稿件处理
/// </summary>
[Job("0 0 0 * * ?")]
internal class PlanedPostsTask : IJob
{
    private readonly ILogger<PlanedPostsTask> _logger;
    private readonly IPostService _postService;

    public PlanedPostsTask(
        ILogger<PlanedPostsTask> logger,
        IPostService postService)
    {
        _logger = logger;
        _postService = postService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("开始定时任务, 发布定时稿件");

        var post = await _postService.Queryable()
            .Where(static x => x.Status == EPostStatus.InPlan).FirstAsync();

        if (post == null)
        {
            _logger.LogInformation("无延时发布稿件");
            return;
        }

        var result = await _postService.PublicInPlanPost(post);
        _logger.LogInformation("发布定时稿件 {status}", result ? "成功" : "失败");
    }
}
