using System.ComponentModel;
using System.Globalization;

namespace MikoMiko.Localization;

/// <summary>
/// 轻量级运行时多语言支持。
/// XAML 中通过 <c>{Binding [Nav.Home], Source={x:Static loc:Loc.Instance}}</c> 取值，
/// 切换语言时向所有索引器属性发送变更通知，界面即时刷新。
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private static readonly Dictionary<string, string> ZhHans = Build(
        ("Nav.Home", "首页"), ("Nav.Video", "视频"), ("Nav.Moment", "关注"), ("Nav.Messages", "消息"), ("Nav.Mine", "我的"), ("Nav.Upload", "上传"), ("Nav.Settings", "设置"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "搜索你感兴趣的内容"),
        ("Common.Loading", "正在加载…"), ("Common.Refresh", "刷新"), ("Common.More", "加载更多"), ("Common.Empty", "这里空空的，去逛逛别的吧"),
        ("Common.Fail", "内容加载失败，请稍后重试"), ("Common.Retry", "重试"), ("Common.Cancel", "取消"), ("Common.Confirm", "确定"),
        ("Common.Close", "关闭"), ("Common.Back", "返回"), ("Common.OpenInBrowser", "在浏览器中打开"), ("Common.Follow", "关注"), ("Common.Followed", "已关注"),
        ("Common.DemoMode", "演示数据"), ("Common.Offline", "当前为离线演示数据"),

        ("Home.Recommend", "推荐"), ("Home.Posts", "板块"), ("Home.Anime", "追番/影视"), ("Home.Official", "官方"), ("Home.Latest", "最新"), ("Home.Empty", "暂时没有找到相关内容"),
        ("Home.Subtitle.Recommend", "为你推荐的社区精选"), ("Home.Subtitle.Latest", "社区最新发布"),
        ("Post.Detail", "帖子详情"), ("Post.Read", "阅读正文"), ("Post.Comments", "评论"), ("Post.NoComments", "还没有评论，来说两句吧"),

        ("Video.Title", "视频"), ("Video.Related", "相关推荐"), ("Video.Like", "点赞"), ("Video.Coin", "投币"), ("Video.SwipeHint", "上下滑动切换视频"), ("Video.Index", "第 {0} / {1} 个"), ("Video.Sync", "已同步到米游社账号"), ("Video.SyncFail", "同步失败，请先在「我的」绑定米游社账号"),
        ("Video.Favorite", "收藏"), ("Video.Share", "分享"), ("Video.Comment", "评论"), ("Video.Danmaku", "弹幕"),
        ("Video.DanmakuHint", "发个友善的弹幕见证当下"), ("Video.Send", "发送"), ("Video.Speed", "倍速"),
        ("Video.Auto", "自动"), ("Video.Quality", "清晰度"), ("Video.NoSource", "该内容暂无可播放的视频源"), ("Video.Buffering", "正在缓冲视频…"), ("Video.CacheFail", "视频加载失败，可点击重试"),
        ("Video.Play", "播放"), ("Video.Pause", "暂停"), ("Video.Playing", "正在播放"), ("Video.Back", "返回视频列表"),

        ("Moment.Title", "关注"), ("Moment.All", "全部"), ("Moment.Following", "只看关注"), ("Moment.FollowingOnly", "只看关注"), ("Moment.Empty", "关注的人还没有发布新动态"), ("Moment.LoginRequired", "登录后查看关注动态"), ("Moment.LoginHint", "动态页只显示你关注的人发布的内容，请先在「我的」扫码登录米游社"),

        ("Mine.Title", "我的"), ("Mine.Guest", "演示账号"), ("Mine.Edit", "编辑资料"), ("Mine.Login", "登录"),
        ("Mine.Logout", "退出登录"), ("Mine.Following", "关注"), ("Mine.Fans", "粉丝"), ("Mine.Likes", "获赞"), ("Mine.Views", "浏览"),
        ("Mine.Games", "我的游戏账号"), ("Mine.Bind", "绑定米游社账号"), ("Mine.Unbind", "解除绑定"), ("Mine.Bound", "已绑定"),
        ("Mine.CookieHint", "粘贴浏览器中的 Cookie（需包含 cookie_token 与 ltuid）"),
        ("Mine.Favorites", "我的收藏"), ("Mine.Recent", "最近浏览"), ("Mine.Profile", "个人资料"),
        ("Mine.Save", "保存"), ("Mine.LocalMode", "本地模式"), ("Mine.Game.Level", "等阶"),
        ("Mine.EmptyFavorites", "还没有收藏内容"), ("Mine.Favorites.Hint", "来自首页推荐的收藏"),
        ("Mine.BindButton", "绑定"), ("Mine.BindHint", "登录后可同步真实的关注、粉丝与游戏账号数据"),
        ("Mine.GuestTitle", "登录米游社"), ("Mine.GuestHint", "扫码登录后可同步昵称、头像、等级、收藏与关注动态"),
        ("Mine.QrLogin", "米游社扫码登录"), ("Mine.QrHint", "打开米游社 App → 我的 → 右上角扫一扫"),
        ("Mine.CookieFallback", "或以 Cookie 方式登录"), ("Mine.GamesEmpty", "登录后可查看你的米游社游戏角色"),

        ("S.Title", "设置"),
        ("S.General", "常规设置"), ("S.Window", "窗口设置"), ("S.Shortcuts", "快捷键"), ("S.Download", "下载设置"),
        ("S.Cache", "缓存管理"), ("S.Playback", "播放设置"), ("S.Common", "通用设置"), ("S.Maintain", "应用维护"),
        ("S.AppInfo", "应用信息"), ("S.About", "关于"), ("S.OpenSource", "开源说明"), ("S.Disclaimer", "免责说明"), ("S.Changelog", "更新日志"),
        ("S.ColorMode", "主界面颜色模式："), ("S.ColorModeHint", "（更改后立即生效）"), ("S.AccentColor", "页面配色："), ("S.AccentHint", "（十套主题色，选中立即应用）"),
        ("S.Theme.System", "跟随系统"), ("S.Theme.Light", "浅色"), ("S.Theme.Dark", "深色"),
        ("S.Font", "字体选择："), ("S.FontHint", "（若字体显示异常，可切换回默认字体）"),
        ("S.Language", "语言切换："), ("S.LanguageHint", "（切换后界面文本立即更新）"),
        ("S.Startup", "启动选项："), ("S.AutoStart", "开机自动启动"), ("S.StartHome", "启动时默认进入首页"),
        ("S.RememberPage", "记住上次浏览的页面"),
        ("S.CloseAction", "关闭主界面时："), ("S.CloseTray", "最小化到系统托盘"), ("S.CloseExit", "退出程序"), ("S.CloseAsk", "关闭时询问"),
        ("S.WindowSize", "启动窗口大小："), ("S.WindowDefault", "默认（按屏幕自适应）"), ("S.WindowSmall", "紧凑 1100 × 720"), ("S.WindowLarge", "宽屏 1440 × 900"),
        ("S.RememberWindow", "记住窗口位置与大小"), ("S.Rounded", "启用窗口圆角"),
        ("S.ShortcutHint", "全局快捷键："), ("S.ShortcutToggle", "显示 / 隐藏主窗口"), ("S.ShortcutEnable", "启用全局快捷键"),
        ("S.DownloadPath", "下载目录："), ("S.Browse", "浏览"), ("S.DownloadThreads", "同时下载数量："),
        ("S.CacheSize", "已占用缓存："), ("S.CacheClear", "清理缓存"),
        ("S.Quality", "默认播放清晰度："), ("S.AutoPlay", "打开后自动播放"), ("S.DanmakuOn", "默认开启弹幕"), ("S.MuteOn", "默认静音"),
        ("S.CheckUpdateStart", "启动时自动检查更新"),
        ("S.CheckUpdate", "检查更新"), ("S.Uninstall", "卸载应用"),
        ("S.Version", "当前版本"), ("S.Runtime", "运行环境"), ("S.InstallPath", "安装目录"), ("S.OpenFolder", "打开安装目录"),
        ("S.AppDesc", "MikoMiko 是一个基于 .NET 8 / WPF 的米游社第三方桌面客户端，支持资讯、帖子、视频、动态与账号管理。"),
        ("S.UpToDate", "当前已是最新版本"), ("S.Checking", "正在检查更新…"), ("S.UpdateFound", "发现新版本："),
        ("S.ClearDone", "缓存已清理"), ("S.UninstallTip", "卸载前请先关闭应用。安装版可在系统设置中卸载；便携版直接删除程序目录即可。"),
        ("S.Saved", "设置已保存"),
        ("S.WindowHint", "窗口相关选项在下次启动时生效"),
        ("S.PlaybackHint", "播放设置将用于视频页播放器"),
        ("S.MaintainHint", "清理缓存或检查更新不会影响已保存的设置"),
        ("S.Changelog.Body", "MikoMiko 更新日志（简要版）\n0.1.33 · 新增消息中心（同步米游社回复、@、点赞与系统通知）；追剧/影视仅保留 7 部番剧与 10 场音乐会；更新日志精简\n0.1.32 · 追番首页七列改版；修复选集后无法播放\n0.1.31 · 首页板块游戏名增加角色头像\n0.1.30 · 首页「帖子」改为「板块」；推荐与视频推送加入作者冷却；追剧规则替换为八站\n0.1.29 · 修复扫码登录换票失败\n0.1.23 · 追番改为 Flutter 规则模块，支持规则编辑与 Anime4K\n0.1.22 · 追番搜索接入站点检索；播放失败自动换站；新增本地弹幕\n0.1.21 · 追番首页与播放器站点切换、我的追剧\n0.1.20 · 追番首页、筛选与 B 站式播放页\n0.1.19 · 改为站内直连 HLS 播放；修复评论回复\n0.1.18 · 追番首页重排；修复登录 -100；设置页对齐\n0.1.17 · 未下载模块时只保留下载入口\n0.1.16 · 九站聚合影视首页与站内播放\n0.1.15 · 视频进度拖动、评论复制与上传页恢复\n0.1.14 · 评论排序、用户主页跳转与关注流\n0.1.13 · 新增追番入口；视频作者冷却改为 3 天\n0.1.12 · 视频作者冷却、关注页双栏与上传入口\n0.1.11 · 导航与视频进度条、用户主页、关注流\n0.1.10 · 搜索精简、评论修复、角色管理与用户主页\n0.1.9 · 设置、下载、评论与视频菜单完善\n0.1.8 · 导航、阅读器、评论与我的页完善\n0.1.7 · 新导航图标、帖子筛选、阅读器与视频交互\n0.1.6 · 导航文案与顺序调整\n0.1.5 · 阅读器、视频、动态、我的与应用内更新\n0.1.4 · 富文本阅读器、Range 播放、内嵌扫码登录\n0.1.3 · 全站搜索、评论同步、随机视频流\n0.1.2 · 新标志、设置布局与登录引导\n0.1.1 · 米蓝主色、随机帖子流与视频滑动\n0.1.0 · 首个版本，完成基础页面与米游社接口"),


        ("About.Title", "MikoMiko"),
        ("About.Slogan", "一个更轻盈的米游社桌面客户端"),
        ("About.Desc", "MikoMiko 以「米游社」社区内容为核心，提供资讯浏览、帖子阅读、视频播放、关注动态与账号资料管理。项目完全开源，遵循 MIT 许可证。"),
        ("OpenSource.Desc", "本项目在架构与交互上参考了下列优秀的开源项目，在此向作者致谢："),
        ("Disclaimer.Desc", "MikoMiko 为非官方第三方客户端，与米哈游（miHoYo）无任何隶属关系。所有内容版权归原作者及米哈游所有，本程序仅作学习与技术交流使用，请勿用于商业用途。")
    );

    private static readonly Dictionary<string, string> ZhHant = Build(
        ("Nav.Home", "首頁"), ("Nav.Video", "影片"), ("Nav.Moment", "關注"), ("Nav.Messages", "訊息"), ("Nav.Mine", "我的"), ("Nav.Upload", "上傳"), ("Nav.Settings", "設定"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "搜尋你感興趣的內容"),
        ("Common.Loading", "正在載入…"), ("Common.Refresh", "重新整理"), ("Common.More", "載入更多"), ("Common.Empty", "這裡空空的，去逛逛別的吧"),
        ("Common.Fail", "內容載入失敗，請稍後重試"), ("Common.Retry", "重試"), ("Common.Cancel", "取消"), ("Common.Confirm", "確定"),
        ("Common.Close", "關閉"), ("Common.Back", "返回"), ("Common.OpenInBrowser", "在瀏覽器中開啟"), ("Common.Follow", "關注"), ("Common.Followed", "已關注"),
        ("Common.DemoMode", "示範資料"), ("Common.Offline", "目前為離線示範資料"),

        ("Home.Recommend", "推薦"), ("Home.Posts", "板塊"), ("Home.Anime", "追番/影視"), ("Home.Official", "官方"), ("Home.Latest", "最新"), ("Home.Empty", "暫時沒有找到相關內容"),
        ("Home.Subtitle.Recommend", "為你推薦的社群精選"), ("Home.Subtitle.Latest", "社群最新發佈"),
        ("Post.Detail", "貼文詳情"), ("Post.Read", "閱讀正文"), ("Post.Comments", "評論"), ("Post.NoComments", "還沒有評論，來說兩句吧"),

        ("Video.Title", "影片"), ("Video.Related", "相關推薦"), ("Video.Like", "按讚"), ("Video.Coin", "投幣"), ("Video.SwipeHint", "上下滑動切換影片"), ("Video.Index", "第 {0} / {1} 個"), ("Video.Sync", "已同步到米遊社帳號"), ("Video.SyncFail", "同步失敗，請先在「我的」綁定米遊社帳號"),
        ("Video.Favorite", "收藏"), ("Video.Share", "分享"), ("Video.Comment", "評論"), ("Video.Danmaku", "彈幕"),
        ("Video.DanmakuHint", "發個友善的彈幕見證當下"), ("Video.Send", "發送"), ("Video.Speed", "倍速"),
        ("Video.Auto", "自動"), ("Video.Quality", "畫質"), ("Video.NoSource", "該內容暫無可播放的影片來源"), ("Video.Buffering", "正在緩衝影片…"), ("Video.CacheFail", "影片載入失敗，可點擊重試"),
        ("Video.Play", "播放"), ("Video.Pause", "暫停"), ("Video.Playing", "正在播放"), ("Video.Back", "返回影片列表"),

        ("Moment.Title", "關注"), ("Moment.All", "全部"), ("Moment.Following", "只看關注"), ("Moment.FollowingOnly", "只看關注"), ("Moment.Empty", "關注的人還沒有發佈新動態"), ("Moment.LoginRequired", "登入後查看關注動態"), ("Moment.LoginHint", "動態頁只顯示你關注的人發佈的內容，請先在「我的」掃碼登入米遊社"),

        ("Mine.Title", "我的"), ("Mine.Guest", "示範帳號"), ("Mine.Edit", "編輯資料"), ("Mine.Login", "登入"),
        ("Mine.Logout", "登出"), ("Mine.Following", "關注"), ("Mine.Fans", "粉絲"), ("Mine.Likes", "獲讚"), ("Mine.Views", "瀏覽"),
        ("Mine.Games", "我的遊戲帳號"), ("Mine.Bind", "綁定米遊社帳號"), ("Mine.Unbind", "解除綁定"), ("Mine.Bound", "已綁定"),
        ("Mine.CookieHint", "貼上瀏覽器中的 Cookie（需包含 cookie_token 與 ltuid）"),
        ("Mine.Favorites", "我的收藏"), ("Mine.Recent", "最近瀏覽"), ("Mine.Profile", "個人資料"),
        ("Mine.Save", "儲存"), ("Mine.LocalMode", "本機模式"), ("Mine.Game.Level", "等階"),
        ("Mine.EmptyFavorites", "還沒有收藏內容"), ("Mine.Favorites.Hint", "來自首頁推薦的收藏"),
        ("Mine.BindButton", "綁定"), ("Mine.BindHint", "登入後可同步真實的關注、粉絲與遊戲帳號資料"),
        ("Mine.GuestTitle", "登入米遊社"), ("Mine.GuestHint", "掃碼登入後可同步暱稱、頭像、等級、收藏與關注動態"),
        ("Mine.QrLogin", "米遊社掃碼登入"), ("Mine.QrHint", "開啟米遊社 App → 我的 → 右上角掃一掃"),
        ("Mine.CookieFallback", "或以 Cookie 方式登入"), ("Mine.GamesEmpty", "登入後可查看你的米遊社遊戲角色"),

        ("S.Title", "設定"),
        ("S.General", "一般設定"), ("S.Window", "視窗設定"), ("S.Shortcuts", "快速鍵"), ("S.Download", "下載設定"),
        ("S.Cache", "快取管理"), ("S.Playback", "播放設定"), ("S.Common", "通用設定"), ("S.Maintain", "應用程式維護"),
        ("S.AppInfo", "應用程式資訊"), ("S.About", "關於"), ("S.OpenSource", "開源說明"), ("S.Disclaimer", "免責說明"), ("S.Changelog", "更新日誌"),
        ("S.ColorMode", "主介面色彩模式："), ("S.ColorModeHint", "（變更後立即生效）"), ("S.AccentColor", "頁面配色："), ("S.AccentHint", "（十套主題色，選中立即套用）"),
        ("S.Theme.System", "跟隨系統"), ("S.Theme.Light", "淺色"), ("S.Theme.Dark", "深色"),
        ("S.Font", "字型選擇："), ("S.FontHint", "（若字型顯示異常，可切換回預設字型）"),
        ("S.Language", "語言切換："), ("S.LanguageHint", "（切換後介面文字立即更新）"),
        ("S.Startup", "啟動選項："), ("S.AutoStart", "開機自動啟動"), ("S.StartHome", "啟動時預設進入首頁"),
        ("S.RememberPage", "記住上次瀏覽的頁面"),
        ("S.CloseAction", "關閉主介面時："), ("S.CloseTray", "最小化到系統匣"), ("S.CloseExit", "結束程式"), ("S.CloseAsk", "關閉時詢問"),
        ("S.WindowSize", "啟動視窗大小："), ("S.WindowDefault", "預設（依螢幕自適應）"), ("S.WindowSmall", "緊湊 1100 × 720"), ("S.WindowLarge", "寬螢幕 1440 × 900"),
        ("S.RememberWindow", "記住視窗位置與大小"), ("S.Rounded", "啟用視窗圓角"),
        ("S.ShortcutHint", "全域快速鍵："), ("S.ShortcutToggle", "顯示 / 隱藏主視窗"), ("S.ShortcutEnable", "啟用全域快速鍵"),
        ("S.DownloadPath", "下載目錄："), ("S.Browse", "瀏覽"), ("S.DownloadThreads", "同時下載數量："),
        ("S.CacheSize", "已佔用快取："), ("S.CacheClear", "清理快取"),
        ("S.Quality", "預設播放畫質："), ("S.AutoPlay", "開啟後自動播放"), ("S.DanmakuOn", "預設開啟彈幕"), ("S.MuteOn", "預設靜音"),
        ("S.CheckUpdateStart", "啟動時自動檢查更新"),
        ("S.CheckUpdate", "檢查更新"), ("S.Uninstall", "解除安裝"),
        ("S.Version", "目前版本"), ("S.Runtime", "執行環境"), ("S.InstallPath", "安裝目錄"), ("S.OpenFolder", "開啟安裝目錄"),
        ("S.AppDesc", "MikoMiko 是一個以 .NET 8 / WPF 打造的米遊社第三方桌面客戶端，支援資訊、貼文、影片、動態與帳號管理。"),
        ("S.UpToDate", "目前已是最新版本"), ("S.Checking", "正在檢查更新…"), ("S.UpdateFound", "發現新版本："),
        ("S.ClearDone", "快取已清理"), ("S.UninstallTip", "解除安裝前請先關閉應用程式。安裝版可在系統設定中解除安裝；可攜版直接刪除程式目錄即可。"),
        ("S.Saved", "設定已儲存"),
        ("S.WindowHint", "視窗相關選項在下次啟動時生效"),
        ("S.PlaybackHint", "播放設定將用於影片頁播放器"),
        ("S.MaintainHint", "清理快取或檢查更新不會影響已儲存的設定"),
        ("S.Changelog.Body", "MikoMiko 更新日志（简要版）\n0.1.33 · 新增消息中心（同步米游社回复、@、点赞与系统通知）；追剧/影视仅保留 7 部番剧与 10 场音乐会；更新日志精简\n0.1.32 · 追番首页七列改版；修复选集后无法播放\n0.1.31 · 首页板块游戏名增加角色头像\n0.1.30 · 首页「帖子」改为「板块」；推荐与视频推送加入作者冷却；追剧规则替换为八站\n0.1.29 · 修复扫码登录换票失败\n0.1.23 · 追番改为 Flutter 规则模块，支持规则编辑与 Anime4K\n0.1.22 · 追番搜索接入站点检索；播放失败自动换站；新增本地弹幕\n0.1.21 · 追番首页与播放器站点切换、我的追剧\n0.1.20 · 追番首页、筛选与 B 站式播放页\n0.1.19 · 改为站内直连 HLS 播放；修复评论回复\n0.1.18 · 追番首页重排；修复登录 -100；设置页对齐\n0.1.17 · 未下载模块时只保留下载入口\n0.1.16 · 九站聚合影视首页与站内播放\n0.1.15 · 视频进度拖动、评论复制与上传页恢复\n0.1.14 · 评论排序、用户主页跳转与关注流\n0.1.13 · 新增追番入口；视频作者冷却改为 3 天\n0.1.12 · 视频作者冷却、关注页双栏与上传入口\n0.1.11 · 导航与视频进度条、用户主页、关注流\n0.1.10 · 搜索精简、评论修复、角色管理与用户主页\n0.1.9 · 设置、下载、评论与视频菜单完善\n0.1.8 · 导航、阅读器、评论与我的页完善\n0.1.7 · 新导航图标、帖子筛选、阅读器与视频交互\n0.1.6 · 导航文案与顺序调整\n0.1.5 · 阅读器、视频、动态、我的与应用内更新\n0.1.4 · 富文本阅读器、Range 播放、内嵌扫码登录\n0.1.3 · 全站搜索、评论同步、随机视频流\n0.1.2 · 新标志、设置布局与登录引导\n0.1.1 · 米蓝主色、随机帖子流与视频滑动\n0.1.0 · 首个版本，完成基础页面与米游社接口"),

        ("About.Title", "MikoMiko"),
        ("About.Slogan", "一個更輕盈的米遊社桌面客戶端"),
        ("About.Desc", "MikoMiko 以「米遊社」社群內容為核心，提供資訊瀏覽、貼文閱讀、影片播放、關注動態與帳號資料管理。專案完全開源，遵循 MIT 授權。"),
        ("OpenSource.Desc", "本專案在架構與互動上參考了下列優秀的開源專案，在此向作者致謝："),
        ("Disclaimer.Desc", "MikoMiko 為非官方第三方客戶端，與米哈遊（miHoYo）無任何隸屬關係。所有內容版權歸原作者及米哈遊所有，本程式僅供學習與技術交流，請勿用於商業用途。")
    );

    private static readonly Dictionary<string, string> En = Build(
        ("Nav.Home", "Home"), ("Nav.Video", "Videos"), ("Nav.Moment", "Following"), ("Nav.Messages", "Messages"), ("Nav.Mine", "Me"), ("Nav.Upload", "Upload"), ("Nav.Settings", "Settings"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "Search anything you like"),
        ("Common.Loading", "Loading…"), ("Common.Refresh", "Refresh"), ("Common.More", "Load more"), ("Common.Empty", "Nothing here yet"),
        ("Common.Fail", "Failed to load, please retry"), ("Common.Retry", "Retry"), ("Common.Cancel", "Cancel"), ("Common.Confirm", "OK"),
        ("Common.Close", "Close"), ("Common.Back", "Back"), ("Common.OpenInBrowser", "Open in browser"), ("Common.Follow", "Follow"), ("Common.Followed", "Following"),
        ("Common.DemoMode", "Demo data"), ("Common.Offline", "Showing offline demo data"),

        ("Home.Recommend", "For You"), ("Home.Posts", "Posts"), ("Home.Anime", "Anime/Films"), ("Home.Official", "Official"), ("Home.Latest", "Latest"), ("Home.Empty", "No content found"),
        ("Home.Subtitle.Recommend", "Community picks for you"), ("Home.Subtitle.Latest", "Latest from the community"),
        ("Post.Detail", "Post"), ("Post.Read", "Read"), ("Post.Comments", "Comments"), ("Post.NoComments", "No comments yet"),

        ("Video.Title", "Videos"), ("Video.Related", "Related"), ("Video.Like", "Like"), ("Video.Coin", "Coin"), ("Video.SwipeHint", "Swipe up/down for next video"), ("Video.Index", "#{0} of {1}"), ("Video.Sync", "Synced to your miyoushe account"), ("Video.SyncFail", "Sync failed - bind a miyoushe account first"),
        ("Video.Favorite", "Save"), ("Video.Share", "Share"), ("Video.Comment", "Comments"), ("Video.Danmaku", "Danmaku"),
        ("Video.DanmakuHint", "Send a friendly danmaku"), ("Video.Send", "Send"), ("Video.Speed", "Speed"),
        ("Video.Auto", "Auto"), ("Video.Quality", "Quality"), ("Video.NoSource", "No playable source for this video"), ("Video.Buffering", "Buffering video…"), ("Video.CacheFail", "Video failed to load, click to retry"),
        ("Video.Play", "Play"), ("Video.Pause", "Pause"), ("Video.Playing", "Now playing"), ("Video.Back", "Back to list"),

        ("Moment.Title", "Following"), ("Moment.All", "All"), ("Moment.Following", "Following"), ("Moment.FollowingOnly", "Following only"), ("Moment.Empty", "No new posts from people you follow"), ("Moment.LoginRequired", "Sign in to see your following feed"), ("Moment.LoginHint", "Only posts from people you follow are shown here. Sign in on the Me page first."),

        ("Mine.Title", "Me"), ("Mine.Guest", "Demo account"), ("Mine.Edit", "Edit profile"), ("Mine.Login", "Sign in"),
        ("Mine.Logout", "Sign out"), ("Mine.Following", "Following"), ("Mine.Fans", "Followers"), ("Mine.Likes", "Likes"), ("Mine.Views", "Views"),
        ("Mine.Games", "My game accounts"), ("Mine.Bind", "Link Miyoushe account"), ("Mine.Unbind", "Unlink"), ("Mine.Bound", "Linked"),
        ("Mine.CookieHint", "Paste browser Cookie (cookie_token and ltuid required)"),
        ("Mine.Favorites", "My favorites"), ("Mine.Recent", "Recently viewed"), ("Mine.Profile", "Profile"),
        ("Mine.Save", "Save"), ("Mine.LocalMode", "Local mode"), ("Mine.Game.Level", "Rank"),
        ("Mine.EmptyFavorites", "Nothing saved yet"), ("Mine.Favorites.Hint", "Saved from home recommendations"),
        ("Mine.BindButton", "Link"), ("Mine.BindHint", "Sign in to sync your real follows, followers and game accounts"),
        ("Mine.GuestTitle", "Sign in to Miyoushe"), ("Mine.GuestHint", "Scan the QR code to sync your nickname, avatar, level, favorites and following feed"),
        ("Mine.QrLogin", "Sign in with QR code"), ("Mine.QrHint", "Miyoushe App → Me → scan icon at top right"),
        ("Mine.CookieFallback", "Or sign in with Cookie"), ("Mine.GamesEmpty", "Sign in to see your Miyoushe game accounts"),

        ("S.Title", "Settings"),
        ("S.General", "General"), ("S.Window", "Window"), ("S.Shortcuts", "Shortcuts"), ("S.Download", "Downloads"),
        ("S.Cache", "Cache"), ("S.Playback", "Playback"), ("S.Common", "Common"), ("S.Maintain", "Maintenance"),
        ("S.AppInfo", "App info"), ("S.About", "About"), ("S.OpenSource", "Open source"), ("S.Disclaimer", "Disclaimer"), ("S.Changelog", "Changelog"),
        ("S.ColorMode", "Appearance: "), ("S.ColorModeHint", "(applies immediately)"), ("S.AccentColor", "Accent color: "), ("S.AccentHint", "(ten presets, applied instantly)"),
        ("S.Theme.System", "System"), ("S.Theme.Light", "Light"), ("S.Theme.Dark", "Dark"),
        ("S.Font", "Font: "), ("S.FontHint", "(switch back to default if text looks wrong)"),
        ("S.Language", "Language: "), ("S.LanguageHint", "(interface updates immediately)"),
        ("S.Startup", "Startup: "), ("S.AutoStart", "Launch at Windows startup"), ("S.StartHome", "Always open on Home"),
        ("S.RememberPage", "Remember last visited page"),
        ("S.CloseAction", "When closing the window: "), ("S.CloseTray", "Minimize to tray"), ("S.CloseExit", "Exit app"), ("S.CloseAsk", "Ask me"),
        ("S.WindowSize", "Startup window size: "), ("S.WindowDefault", "Default (fit screen)"), ("S.WindowSmall", "Compact 1100 × 720"), ("S.WindowLarge", "Wide 1440 × 900"),
        ("S.RememberWindow", "Remember window position and size"), ("S.Rounded", "Rounded window corners"),
        ("S.ShortcutHint", "Global hotkey: "), ("S.ShortcutToggle", "Show / hide main window"), ("S.ShortcutEnable", "Enable global hotkey"),
        ("S.DownloadPath", "Download folder: "), ("S.Browse", "Browse"), ("S.DownloadThreads", "Concurrent downloads: "),
        ("S.CacheSize", "Cache used: "), ("S.CacheClear", "Clear cache"),
        ("S.Quality", "Default quality: "), ("S.AutoPlay", "Autoplay on open"), ("S.DanmakuOn", "Danmaku on by default"), ("S.MuteOn", "Muted by default"),
        ("S.CheckUpdateStart", "Check for updates on startup"),
        ("S.CheckUpdate", "Check for updates"), ("S.Uninstall", "Uninstall"),
        ("S.Version", "Version"), ("S.Runtime", "Runtime"), ("S.InstallPath", "Install path"), ("S.OpenFolder", "Open folder"),
        ("S.AppDesc", "MikoMiko is an unofficial Miyoushe desktop client built with .NET 8 and WPF, with news, posts, videos, feed and account support."),
        ("S.UpToDate", "You are on the latest version"), ("S.Checking", "Checking for updates…"), ("S.UpdateFound", "New version: "),
        ("S.ClearDone", "Cache cleared"), ("S.UninstallTip", "Close the app before uninstalling. Installed builds can be removed in Windows settings; portable builds can simply be deleted."),
        ("S.Saved", "Settings saved"),
        ("S.WindowHint", "Window options apply on next launch"),
        ("S.PlaybackHint", "Playback settings apply to the video player"),
        ("S.MaintainHint", "Clearing cache or checking updates keeps your settings"),
        ("S.Changelog.Body", "MikoMiko changelog (concise)\n0.1.33 · Added Messages synced with Miyoushe replies, mentions, likes and notices; Bilibili-only anime list with 7 series and 10 concerts\n0.1.32 · Seven-column anime home; fixed episode playback\n0.1.31 · Game avatars on home section menu\n0.1.30 · Posts renamed Sections; author cooldowns; new eight-site anime rules\n0.1.29 · Fixed QR login token exchange\n0.1.23 · Flutter anime rule module with editor and Anime4K\n0.1.22 · Site search, auto source switch, local danmaku\n0.1.21 · Anime home, provider switching and watchlist\n0.1.20 · Anime home, filters and Bilibili-style player\n0.1.19 · Direct HLS playback; comment reply fixes\n0.1.18 · Anime home refresh; login -100 fix; settings alignment\n0.1.17 · Download-only gate for anime module\n0.1.16 · Nine-site media home and in-app playback\n0.1.15 · Video seek, comment copy and upload page restore\n0.1.14 · Comment sorting, user profiles and following feed\n0.1.13 · Anime entry; video author cooldown to 3 days\n0.1.12 · Author cooldown, two-column following and upload\n0.1.11 · Nav icons, video progress, profiles and feed\n0.1.10 · Search cleanup, comments, roles and profiles\n0.1.9 · Settings, downloads, comments and video menu\n0.1.8 · Navigation, reader, comments and profile\n0.1.7 · Nav icons, post filters, reader and video\n0.1.6 · Navigation labels and order\n0.1.5 · Reader, video, feed, profile and in-app update\n0.1.4 · Rich reader, Range playback, embedded QR login\n0.1.3 · Global search, synced comments, random videos\n0.1.2 · New logo, settings layout and login guide\n0.1.1 · Blue theme, random posts and video swipe\n0.1.0 · First release with core pages and Miyoushe APIs"),

        ("About.Title", "MikoMiko"),
        ("About.Slogan", "A lighter Miyoushe desktop client"),
        ("About.Desc", "MikoMiko focuses on Miyoushe community content: news, posts, videos, followed feeds and account info. The project is fully open source under the MIT license."),
        ("OpenSource.Desc", "The architecture and interactions of this project were inspired by these excellent open-source projects. Thanks to their authors:"),
        ("Disclaimer.Desc", "MikoMiko is an unofficial third-party client and is not affiliated with miHoYo. All content belongs to its original authors and miHoYo. This app is for learning and technical exchange only, not for commercial use.")
    );

    private static readonly Dictionary<string, string> Ja = Build(
        ("Nav.Home", "ホーム"), ("Nav.Video", "動画"), ("Nav.Moment", "フォロー"), ("Nav.Messages", "メッセージ"), ("Nav.Mine", "マイ"), ("Nav.Upload", "アップロード"), ("Nav.Settings", "設定"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "気になる内容を検索"),
        ("Common.Loading", "読み込み中…"), ("Common.Refresh", "更新"), ("Common.More", "もっと見る"), ("Common.Empty", "まだ何もありません"),
        ("Common.Fail", "読み込みに失敗しました"), ("Common.Retry", "再試行"), ("Common.Cancel", "キャンセル"), ("Common.Confirm", "OK"),
        ("Common.Close", "閉じる"), ("Common.Back", "戻る"), ("Common.OpenInBrowser", "ブラウザで開く"), ("Common.Follow", "フォロー"), ("Common.Followed", "フォロー中"),
        ("Common.DemoMode", "デモデータ"), ("Common.Offline", "オフラインのデモデータを表示中"),
        ("Home.Recommend", "おすすめ"), ("Home.Posts", "投稿"), ("Home.Anime", "アニメ/映画"), ("Home.Official", "公式"), ("Home.Latest", "最新"), ("Home.Empty", "コンテンツが見つかりません"),
        ("Home.Subtitle.Recommend", "あなたへのコミュニティピック"), ("Home.Subtitle.Latest", "コミュニティの最新投稿"),
        ("Post.Detail", "投稿"), ("Post.Read", "読む"), ("Post.Comments", "コメント"), ("Post.NoComments", "まだコメントはありません"),
        ("Video.Title", "動画"), ("Video.Related", "関連"), ("Video.Like", "いいね"), ("Video.Coin", "コイン"),
        ("Video.SwipeHint", "上下にスワイプして次の動画へ"), ("Video.Index", "{0} / {1} 本目"), ("Video.Sync", "miyoushe アカウントに同期しました"), ("Video.SyncFail", "同期に失敗しました。「マイ」でログインしてください"),
        ("Video.Favorite", "保存"), ("Video.Share", "共有"), ("Video.Comment", "コメント"), ("Video.Danmaku", "弾幕"),
        ("Video.DanmakuHint", "やさしい弾幕を送ろう"), ("Video.Send", "送信"), ("Video.Speed", "速度"),
        ("Video.Auto", "自動"), ("Video.Quality", "画質"), ("Video.NoSource", "再生できる動画ソースがありません"),
        ("Video.Play", "再生"), ("Video.Pause", "一時停止"), ("Video.Playing", "再生中"), ("Video.Back", "動画一覧へ"),
        ("Video.Buffering", "動画を読み込み中…"), ("Video.CacheFail", "動画の読み込みに失敗しました。クリックで再試行"),
        ("Moment.Title", "フォロー中"), ("Moment.All", "すべて"), ("Moment.Following", "フォロー中"), ("Moment.FollowingOnly", "フォロー中のみ"),
        ("Moment.Empty", "フォロー中のユーザーの新着はありません"), ("Moment.LoginRequired", "ログインしてフィードを見る"),
        ("Moment.LoginHint", "フィードにはフォロー中のユーザーの投稿だけが表示されます。「マイ」でログインしてください。"),
        ("Mine.Title", "マイ"), ("Mine.Guest", "デモアカウント"), ("Mine.Edit", "プロフィール編集"), ("Mine.Login", "ログイン"),
        ("Mine.Logout", "ログアウト"), ("Mine.Following", "フォロー"), ("Mine.Fans", "フォロワー"), ("Mine.Likes", "いいね"), ("Mine.Views", "閲覧"),
        ("Mine.Games", "ゲームアカウント"), ("Mine.Bind", "miyoushe と連携"), ("Mine.Unbind", "連携解除"), ("Mine.Bound", "連携済み"),
        ("Mine.CookieHint", "Cookie を貼り付けてください（cookie_token と ltuid が必要）"),
        ("Mine.Favorites", "お気に入り"), ("Mine.Recent", "最近見た"), ("Mine.Profile", "プロフィール"),
        ("Mine.Save", "保存"), ("Mine.LocalMode", "ローカルモード"), ("Mine.Game.Level", "レベル"),
        ("Mine.EmptyFavorites", "お気に入りはまだありません"), ("Mine.Favorites.Hint", "ホームのおすすめから保存"),
        ("Mine.BindButton", "連携"), ("Mine.BindHint", "ログインするとフォローやゲームアカウントを同期できます"),
        ("Mine.GuestTitle", "miyoushe にログイン"), ("Mine.GuestHint", "QR コードでログインすると、ニックネーム・アイコン・レベル・お気に入り・フィードを同期できます"),
        ("Mine.QrLogin", "QR コードでログイン"), ("Mine.QrHint", "miyoushe アプリ → マイ → 右上のスキャン"),
        ("Mine.CookieFallback", "Cookie でログイン"), ("Mine.GamesEmpty", "ログインするとゲームアカウントが表示されます"),
        ("S.Title", "設定"), ("S.General", "一般"), ("S.Window", "ウィンドウ"), ("S.Shortcuts", "ショートカット"), ("S.Download", "ダウンロード"),
        ("S.Cache", "キャッシュ"), ("S.Playback", "再生"), ("S.Common", "共通"), ("S.Maintain", "メンテナンス"),
        ("S.AppInfo", "アプリ情報"), ("S.About", "概要"), ("S.OpenSource", "オープンソース"), ("S.Disclaimer", "免責"), ("S.Changelog", "更新履歴"),
        ("S.Language", "言語："), ("S.LanguageHint", "（切り替えるとすぐに反映されます）"),
        ("S.ColorMode", "外観："), ("S.AccentColor", "アクセントカラー："), ("S.AccentHint", "（10 種類、選択するとすぐに適用）"),
        ("S.Theme.System", "システム"), ("S.Theme.Light", "ライト"), ("S.Theme.Dark", "ダーク"),
        ("S.Favorites", "お気に入り"), ("S.Saved", "設定を保存しました"), ("S.ClearDone", "キャッシュを削除しました"),
        ("S.AppDesc", "MikoMiko は .NET 8 / WPF で作られた miyoushe の非公式デスクトップクライアントです。"),
        ("About.Title", "MikoMiko"), ("About.Slogan", "もっと軽やかな miyoushe クライアント"),
        ("About.Desc", "MikoMiko は miyoushe のコミュニティコンテンツを中心に、投稿・動画・フィード・アカウント管理を提供します。MIT ライセンスのオープンソースです。"),
        ("OpenSource.Desc", "本プロジェクトは以下の優れたオープンソースプロジェクトを参考にしています。"),
        ("Disclaimer.Desc", "MikoMiko は非公式のサードパーティクライアントであり、miHoYo とは関係ありません。")
    );

    private static readonly Dictionary<string, string> Ru = Build(
        ("Nav.Home", "Главная"), ("Nav.Video", "Видео"), ("Nav.Moment", "Подписки"), ("Nav.Messages", "Сообщения"), ("Nav.Mine", "Профиль"), ("Nav.Upload", "Загрузить"), ("Nav.Settings", "Настройки"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "Поиск интересного контента"),
        ("Common.Loading", "Загрузка…"), ("Common.Refresh", "Обновить"), ("Common.More", "Ещё"), ("Common.Empty", "Здесь пока пусто"),
        ("Common.Fail", "Не удалось загрузить"), ("Common.Retry", "Повторить"), ("Common.Cancel", "Отмена"), ("Common.Confirm", "ОК"),
        ("Common.Close", "Закрыть"), ("Common.Back", "Назад"), ("Common.OpenInBrowser", "Открыть в браузере"), ("Common.Follow", "Подписаться"), ("Common.Followed", "Вы подписаны"),
        ("Home.Recommend", "Рекомендации"), ("Home.Posts", "Посты"), ("Home.Anime", "Аниме/Кино"), ("Home.Official", "Официальное"), ("Home.Latest", "Новое"), ("Home.Empty", "Ничего не найдено"),
        ("Post.Detail", "Пост"), ("Post.Read", "Читать"), ("Post.Comments", "Комментарии"), ("Post.NoComments", "Комментариев пока нет"),
        ("Video.Title", "Видео"), ("Video.Like", "Нравится"), ("Video.SwipeHint", "Свайп вверх/вниз — следующее видео"),
        ("Video.Index", "{0} из {1}"), ("Video.Sync", "Синхронизировано с аккаунтом miyoushe"), ("Video.SyncFail", "Ошибка синхронизации — войдите в профиле"),
        ("Video.Favorite", "В избранное"), ("Video.Share", "Поделиться"), ("Video.Danmaku", "Данмаку"),
        ("Video.NoSource", "Нет доступного источника видео"), ("Video.Buffering", "Буферизация видео…"), ("Video.CacheFail", "Не удалось загрузить видео, нажмите для повтора"),
        ("Moment.Title", "Подписки"), ("Moment.FollowingOnly", "Только подписки"), ("Moment.Empty", "Нет новых постов от подписок"),
        ("Moment.LoginRequired", "Войдите, чтобы увидеть ленту подписок"), ("Moment.LoginHint", "Здесь показываются только посты людей, на которых вы подписаны."),
        ("Mine.Title", "Профиль"), ("Mine.Login", "Войти"), ("Mine.Logout", "Выйти"), ("Mine.Following", "Подписки"), ("Mine.Fans", "Подписчики"),
        ("Mine.Likes", "Лайки"), ("Mine.Views", "Просмотры"), ("Mine.Games", "Игровые аккаунты"), ("Mine.Bound", "Привязано"),
        ("Mine.Favorites", "Избранное"), ("Mine.EmptyFavorites", "В избранном пока пусто"),
        ("Mine.GuestTitle", "Вход в miyoushe"), ("Mine.GuestHint", "Войдите по QR-коду, чтобы синхронизировать ник, аватар, уровень, избранное и ленту"),
        ("Mine.QrLogin", "Войти по QR-коду"), ("Mine.QrHint", "Приложение miyoushe → Профиль → сканер справа сверху"),
        ("Mine.CookieFallback", "Войти через Cookie"), ("Mine.BindButton", "Привязать"), ("Mine.GamesEmpty", "Войдите, чтобы увидеть игровые аккаунты"),
        ("S.Title", "Настройки"), ("S.General", "Общие"), ("S.Window", "Окно"), ("S.Shortcuts", "Горячие клавиши"), ("S.Download", "Загрузки"),
        ("S.Cache", "Кэш"), ("S.Playback", "Воспроизведение"), ("S.Common", "Прочее"), ("S.Maintain", "Обслуживание"),
        ("S.AppInfo", "О приложении"), ("S.About", "О программе"), ("S.OpenSource", "Open source"), ("S.Disclaimer", "Отказ от ответственности"), ("S.Changelog", "История"),
        ("S.Language", "Язык: "), ("S.LanguageHint", "(применяется сразу)"),
        ("S.ColorMode", "Оформление: "), ("S.AccentColor", "Акцентный цвет: "), ("S.AccentHint", "(10 тем, применяются сразу)"),
        ("S.Theme.System", "Системная"), ("S.Theme.Light", "Светлая"), ("S.Theme.Dark", "Тёмная"),
        ("S.Saved", "Настройки сохранены"), ("S.ClearDone", "Кэш очищен"),
        ("About.Title", "MikoMiko"), ("About.Slogan", "Лёгкий клиент miyoushe"),
        ("About.Desc", "MikoMiko — неофициальный клиент miyoushe на .NET 8 / WPF: посты, видео, лента и аккаунт. Открытый код под MIT."),
        ("OpenSource.Desc", "Проект вдохновлён этими открытыми проектами:"),
        ("Disclaimer.Desc", "MikoMiko — неофициальный клиент, не связанный с miHoYo.")
    );

    private static readonly Dictionary<string, string> Ko = Build(
        ("Nav.Home", "홈"), ("Nav.Video", "영상"), ("Nav.Moment", "팔로잉"), ("Nav.Messages", "메시지"), ("Nav.Mine", "내 정보"), ("Nav.Upload", "업로드"), ("Nav.Settings", "설정"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "관심 있는 콘텐츠 검색"),
        ("Common.Loading", "불러오는 중…"), ("Common.Refresh", "새로고침"), ("Common.Empty", "아직 내용이 없습니다"), ("Common.Back", "뒤로"),
        ("Post.Detail", "게시글"), ("Post.Comments", "댓글"), ("Post.NoComments", "아직 댓글이 없습니다"),
        ("Video.Title", "영상"), ("Video.Like", "좋아요"), ("Video.Favorite", "저장"), ("Video.Share", "공유"),
        ("Video.SwipeHint", "위아래로 넘겨 다음 영상"), ("Video.Buffering", "영상을 불러오는 중…"),
        ("Moment.Title", "팔로잉"), ("Moment.FollowingOnly", "팔로잉만"), ("Moment.Empty", "팔로우한 사람의 새 소식이 없습니다"),
        ("Moment.LoginRequired", "로그인 후 팔로잉 피드 보기"), ("Moment.LoginHint", "피드에는 팔로우한 사람의 게시글만 표시됩니다."),
        ("Mine.Title", "내 정보"), ("Mine.Login", "로그인"), ("Mine.Logout", "로그아웃"), ("Mine.Following", "팔로잉"), ("Mine.Fans", "팔로워"),
        ("Mine.GuestTitle", "miyoushe 로그인"), ("Mine.QrLogin", "QR 코드로 로그인"), ("Mine.CookieFallback", "Cookie로 로그인"),
        ("Mine.GuestHint", "QR 코드로 로그인하면 닉네임, 아바타, 레벨, 즐겨찾기, 피드를 동기화합니다"),
        ("S.Title", "설정"), ("S.General", "일반"), ("S.Window", "창"), ("S.Shortcuts", "단축키"), ("S.Download", "다운로드"),
        ("S.Cache", "캐시"), ("S.Playback", "재생"), ("S.Common", "공통"), ("S.Maintain", "유지 관리"), ("S.AppInfo", "앱 정보"),
        ("S.About", "정보"), ("S.OpenSource", "오픈소스"), ("S.Disclaimer", "면책"), ("S.Changelog", "변경 기록"),
        ("About.Title", "MikoMiko"), ("About.Slogan", "더 가벼운 miyoushe 클라이언트")
    );
    private static Dictionary<string, string> Build(params (string Key, string Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Value);

    private Dictionary<string, string> _map = ZhHans;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = "zh-CN";

    public string this[string key]
        => _map.TryGetValue(key, out var value) ? value : (En.TryGetValue(key, out var fallback) ? fallback : key);

    public static IEnumerable<(string Code, string Display)> Supported => new[]
    {
        ("zh-CN", "简体中文"),
        ("zh-TW", "繁體中文"),
        ("en-US", "English"),
        ("ja-JP", "日本語"), ("ru-RU", "Русский"), ("ko-KR", "한국어"),
    };

    public void SetLanguage(string code)
    {
        Language = code;
        _map = code switch
        {
            "zh-TW" => ZhHant,
            "en-US" => En,
            "ja-JP" => Ja,
            "ru-RU" => Ru,
            "ko-KR" => Ko,
            _ => ZhHans,
        };
        CultureInfo.CurrentUICulture = new CultureInfo(code);

        // 通知所有索引器绑定刷新
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        foreach (var key in _map.Keys)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"[{key}]"));
        }
    }

    public string T(string key) => this[key];
}








