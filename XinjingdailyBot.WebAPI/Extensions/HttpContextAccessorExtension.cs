using XinjingdailyBot.Model.Models;

namespace XinjingdailyBot.Infrastructure.Extensions;

/// <summary>
/// HttpContextAccessor扩展
/// </summary>
public static class HttpContextAccessorExtension
{
    /// <summary>
    /// 获取用户
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    /// <returns></returns>
    public static Users GetUser(this IHttpContextAccessor httpContextAccessor)
    {
        Users? user = null;

        if ((httpContextAccessor.HttpContext?.Items.TryGetValue("Users", out var obj) ?? false) && user != null)
        {
            user = obj as Users;
        }

        ArgumentNullException.ThrowIfNull(user);

        return user;
    }
}
