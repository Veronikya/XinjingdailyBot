namespace ZR.Admin.WebApi.Filters;

/// <summary>
/// 授权校验访问
/// 如果跳过授权登录在Action 或controller加上 AllowAnonymousAttribute
/// </summary>
//public class VerifyAttribute : Attribute, IAuthorizationFilter
//{
//    static readonly Logger logger = LogManager.GetCurrentClassLogger();

//    /// <summary>
//    /// 只判断token是否正确，不判断权限
//    /// 如果需要判断权限的在Action上加上ApiActionPermission属性标识权限类别，ActionPermissionFilter作权限处理
//    /// </summary>
//    /// <param name="context"></param>
//    public void OnAuthorization(AuthorizationFilterContext context)
//    {
//        var noNeedCheck = false;
//        if (context.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
//        {
//            noNeedCheck = controllerActionDescriptor.MethodInfo.GetCustomAttributes(inherit: true)
//              .Any(a => a.GetType().Equals(typeof(AllowAnonymousAttribute)));
//        }

//        if (noNeedCheck) return;

//        string ip = HttpContextExtension.GetClientUserIp(context.HttpContext);
//        string url = context.HttpContext.Request.Path;
//        var isAuthed = context.HttpContext.User.Identity.IsAuthenticated;

//        //使用jwt token校验2020-11-21
//        TokenModel loginUser = JwtUtil.GetLoginUser(context.HttpContext);
//        if (loginUser != null)
//        {
//            var nowTime = DateTime.UtcNow;
//            TimeSpan ts = loginUser.ExpireTime - nowTime;

//            //Console.WriteLine($"jwt到期剩余：{ts.TotalMinutes}分,{ts.TotalSeconds}秒");

//            var CK = "token_" + loginUser.UserId;
//            if (!CacheHelper.Exists(CK) && ts.TotalMinutes < 5)
//            {
//                var newToken = JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(loginUser));

//                CacheHelper.SetCache(CK, CK, 1);
//                context.HttpContext.Response.Headers.Add("X-Refresh-Token", newToken);
//            }
//        }
//        if (loginUser == null || !isAuthed)
//        {
//            string msg = $"请求访问[{url}]失败，无法访问系统资源";
//            //logger.Info(msg);

//            context.Result = new JsonResult(new ApiResult((int)ResultCode.DENY, msg));
//        }
//    }
//}
