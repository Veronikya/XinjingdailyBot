using Telegram.Bot.Types.Enums;

namespace XinjingdailyBot.WebAPI.IPC.Responses;

/// <summary>
/// 连接测试响应
/// </summary>
public sealed record TestTokenResponse
{
    public string UserName { get; set; }
    public long UserID { get; set; }
    public int UID { get; set; }
    public string NickName { get; set; }
}
