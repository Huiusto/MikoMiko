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
        ("Nav.Home", "首页"), ("Nav.Video", "视频"), ("Nav.Moment", "关注"), ("Nav.Mine", "我的"), ("Nav.Upload", "上传"), ("Nav.Settings", "设置"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "搜索你感兴趣的内容"),
        ("Common.Loading", "正在加载…"), ("Common.Refresh", "刷新"), ("Common.More", "加载更多"), ("Common.Empty", "这里空空的，去逛逛别的吧"),
        ("Common.Fail", "内容加载失败，请稍后重试"), ("Common.Retry", "重试"), ("Common.Cancel", "取消"), ("Common.Confirm", "确定"),
        ("Common.Close", "关闭"), ("Common.Back", "返回"), ("Common.OpenInBrowser", "在浏览器中打开"), ("Common.Follow", "关注"), ("Common.Followed", "已关注"),
        ("Common.DemoMode", "演示数据"), ("Common.Offline", "当前为离线演示数据"),

        ("Home.Recommend", "推荐"), ("Home.Posts", "帖子"), ("Home.Anime", "追番/影视"), ("Home.Official", "官方"), ("Home.Latest", "最新"), ("Home.Empty", "暂时没有找到相关内容"),
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
        ("S.Changelog.Body", "MikoMiko 完整更新日志（0.1.0 → 0.1.20）\n\n────────────────────────────────────────\nv0.1.20 · 2026-11-24\n· 「追番/影视」未下载页改为用户提供的整幅原图居中展示，不裁切、不重绘，胶囊按钮可直接下载模块\n· 追番首页按新效果图重做：顶部白色搜索栏、整幅蓝白少女横幅、猜你喜欢七列海报，每次进入随机换一批\n· 「查看全部」新增类型、排序、地区、风格四行筛选与七列海报；修复部分站外海报因 Referer 限制加载失败\n· 点击作品进入 B 站式播放页：左侧视频、右侧简介 / 追剧 / 播放线路 / 选集网格，可自由切换线路与剧集，当前集高亮\n· 离开追番/影视页时自动关闭播放器并释放 WebView2，停止播放与音频；返回首页时随机内容自动刷新\n· 修正模块数据源编号：樱花动漫1 为 zhuidm、樱花动漫2 为 yinghdm，模块版本升至 1.3.0\n\n────────────────────────────────────────\n\nv0.1.19 · 2026-11-24\n· 「追番/影视」未下载页直接使用设计原图，不再自行绘制，大胶囊按钮可直接点击下载模块\n· 追番/影视播放改为站内提取真实视频流，内置 HLS / MP4 播放器直接播放，不再展示第三方站点网页\n· 修复「视频」评论区点击正文无法回复他人评论的问题\n· 「我的发布 / 我的评论 / 我的合集 / 我的收藏」去掉鼠标悬浮时的蓝色底色\n· 设置页「关闭主界面时」默认选择「最小化到系统托盘」，并重排缓存 / 应用信息、通用 / 维护卡片，逐行等高对齐\n\n────────────────────────────────────────\n\nv0.1.18 · 2026-11-24\n· 追番/影视首页按效果图重排：顶部悬浮搜索、满幅 Hero + 右下播放按钮、底部今日更新横排、左侧分类与 7 列海报墙\n· 修复扫码登录 -100：改用网站扫码接口并从 Set-Cookie 读取完整凭证，账号校验优先走社区资料接口\n· 设置页卡片统一宽度与左右对齐，应用维护 / 应用信息、关于 / 免责说明逐行等高\n\n────────────────────────────────────────\n\nv0.1.17 · 2026-11-24\n· 「追番/影视」未下载页按新视觉重做：白底浅蓝装饰、软萌角色探身指向大号胶囊下载按钮，页面只保留下载入口，不显示搜索框与站点链接\n· 搜索框、左侧分类与原生影视首页仅在下载模块后出现，卸载后自动回到下载页\n· 追番/影视正式聚合九个站点，作品详情与播放全部在软件内完成\n\n────────────────────────────────────────\n\nv0.1.16 · 2026-11-24\n· 「追番/影视」未下载模块时只保留一个下载按钮，隐藏顶部搜索框与底部站点链接；下载后自动进入原生影视首页\n· 影视首页重做为搜索栏 + 左侧分类 + 大图 Hero + 今日更新横排 + 猜你喜欢海报墙，支持国产 / 日韩 / 欧美 / 动画 / 港台 / 海外 / 综合 / 今日更新 / 热榜 / 资讯\n· 聚合樱花动漫1、樱花动漫2、樱花TV、PIDEC、南瓜电影、七猫视频、电影先生、漫漫看、麻花影视九个站点\n· 作品详情与视频播放全部在软件内完成，播放页隐藏地址栏并自动清理常见浮层广告，不跳转系统浏览器\n· 搜索支持站内跨源检索与本地缓存兜底，无结果时展示空态提示\n· 修复第三方封面图被固定米游社 Referer 拦截的问题；封面加载失败时显示作品名渐变占位，不再出现空白帖子图\n\n────────────────────────────────────────\n\nv0.1.15 · 2026-11-24\n· 帖子视频进度条支持拖动到任意位置即时缓冲，播放期间控制栏常驻\n· 帖子与视频评论区点赞爱心变红；帖子标题、正文、一级评论与楼中楼文本均可鼠标选中复制\n· 视频评论区单击评论即可回复；点击头像或昵称进入用户主页；点击评论面板外任意位置关闭评论区\n· 视频清晰度切换真实作用于播放源；下载成功后保存位置提示只显示 1 秒；发送按钮右侧新增「弹幕功能仅能自娱自乐。」\n· 关注页动态中的头像与昵称可直接进入用户主页\n· 我的页「我的发布 / 我的评论 / 我的合集 / 我的收藏」移除悬浮效果\n· 上传网页恢复完整原样，不再隐藏站点 logo、米游社文字、导航与搜索框；登录态仍会同步注入\n· 软件登录态同步注入发布网页，无需重复扫码；设置页新增开源说明并优化排版\n\n────────────────────────────────────────\n\nv0.1.14 · 2026-11-24\n· 帖子评论区新增「默认 / 最新」排序；点赞爱心变红，点踩后图标变灰并折叠该评论，取消踩恢复\n· 评论头像与昵称可直接进入用户主页，楼中楼同样支持\n· 视频点赞数字即时 +1 / -1；视频评论区补齐排序、回复、图片灯箱与右下角下载\n· 关注页全部动态按发布时间倒序，最新置顶；点击动态中的用户，右侧只显示 TA 的全部内容\n· 我的页角色管理设置移除「显示游戏战绩详情」；更换背景的预览区用斑马线标记显示范围\n· 上传网页隐藏站点 logo、米游社文字、首页 / 校园 / ACG 导航和搜索框，仅保留右上角头像登录\n· 发布内容九个板块替换为对应板块插画；设置页改为两列等宽卡片排版\n────────────────────────────────────────\n\nv0.1.13 · 2026-11-24\n· 视频随机流作者冷却由 7 天调整为 3 天，3 天内不再推送同一作者，到期自动清理；官方视频不受冷却限制\n· 首页新增「追番/影视」板块，可独立下载与卸载，聚合樱花TV、PIDEC、樱花动漫、南瓜电影、七猫视频、电影先生、漫漫看、麻花影视等九个站点\n· 「推荐 / 帖子 / 追番/影视」选中文字改为 #4EA4DD 蓝色；进入追番/影视后隐藏页头全局搜索，只保留板块内搜索\n· 新增影视首页与「查看更多」多维筛选，支持类型、排序、地区、来源筛选，海报显示集数与完结状态\n· 关注页「全部动态」图标改为淡蓝色，移除用户头像右侧红点\n· 更新日志移除作者徽标相关记录，版本之间新增分隔线\n\n────────────────────────────────────────\n\nv0.1.12 · 2026-11-24\n· 视频随机流新增作者冷却：实际刷到的作者记录 UID 与用户名，7 天内不再推送同一作者，到期自动清理\n· 视频随机池改为并行搜索 + 多版块并行补足，减少串行等待，随机覆盖更多作者与投稿\n· 关注页改为左侧关注用户导航 + 右侧动态流，可在「全部动态」与单人动态间切换，红点标记有动态的 TA\n· 设置上方新增蓝底白星上传入口，先选 9 个板块，再选长文 / 图片 / 视频，直达米游社发布页\n\n────────────────────────────────────────\n\nv0.1.11 · 2026-11-23\n· 首页 / 视频 / 我的导航默认图标与关注、设置统一为蓝灰双色轮廓\n· 视频页进度条固定 3px，新增可拖动的白色圆点，去掉靠近变粗与焦点边框，交互参考 B 站\n· 用户主页换用冰蓝发少女横幅，发布跨全部版块分页加载，包含视频；TA 的游戏角色显示游戏 UID，点击可查看对应游戏战绩详情\n· 修复视频评论区楼中楼回复与点赞命令绑定，子评论可直接回复\n· 关注流改为按账号级完整关注列表逐人拉取全版块最新帖子，不再只限原神版块\n· 我的 → 角色管理设置新增「显示游戏战绩详情」开关\n· 搜索相关用户默认两排，右侧「更多用户」可展开全部\n· 首页推荐与空搜索不再显示相关用户，仅关键词搜索时出现\n· 帖子视频控制栏悬停显示 / 移出隐藏，白色播放暂停图标，点击画面可切换播放；灯箱下载按钮只保留图标\n· 设置页移除窗口设置，重排缓存、通用、应用维护卡片\n\n────────────────────────────────────────\n\nv0.1.10 · 2026-11-23\n· 关注 / 设置导航默认态统一为与选中态同轮廓、同粗细的蓝灰双色图标，尺寸完全一致\n· 搜索去掉用户 / 攻略 / 视频 / 百科 / 图片 / 话题分类文本，直接展示相关用户与综合内容\n· 评论发送与回复改用网页端 2.102 签名并对齐参数，顶层评论不再传 reply_id，修复 invalid request\n· 视频清晰度移至底部倍速旁；进度条取消悬停变化，拖动即从落点缓冲；点赞 / 收藏不再重载视频，未登录提示「请登陆账号」\n· 我的角色管理改为三列，标题前新增齿轮设置，可隐藏游戏与调整顺序并持久化；六枚游戏图标换成官方图标\n· 用户主页补齐米游社 UID、全部可用标签、发布 / 评论 / 合集 / 收藏四页签、用户内容搜索与游戏角色\n· 设置页新增页标题，分区改为自适应高度，收紧卡片间距与留白\n· 帖子内视频支持点击画面或按钮暂停 / 播放，进度条可自由定位\n\n────────────────────────────────────────\n\nv0.1.9 · 2026-11-23\n· 关注 / 设置导航图标默认态与选中态统一尺寸\n· 设置顶部改为「访问项目主页（GitHub）」（浏览器打开）与「开发者首页（支持、讨论、问题反馈）」（软件内打开），移除关于区旧按钮\n· 退出窗口大小选择与播放设置；登录后账号按钮改为「前往我的主页」\n· 设置新增下载位置，可更改并直接打开；帖子图片 / 视频与视频页均支持下载\n· 视频推荐随机混合官方与用户作品，同一作者 5 条内不重复；修复评论发送 invalid request；评论作者色统一 #4EA4DD\n· 帖子内视频增加可拖动进度条与下载按钮；图片灯箱新增上一张 / 下一张与下载\n· 我的角色卡直接展示各游戏公开战绩与游戏图标，取消悬浮；发布 / 评论 / 合集 / 收藏合并为一栏切换按钮\n· 搜索页新增综合 / 攻略 / 视频 / 百科 / 图片 / 用户 / 话题分类页签\n· 关注页取消悬浮，可直接点赞与收藏；评论图片与表情正常加载\n· 视频右上角新增三点菜单，可切换画质并下载视频；进度条取消悬浮反馈\n· 搜索框与视频弹幕输入框改为随机占位文案轮换\n\n────────────────────────────────────────\n\nv0.1.8 · 2026-11-23\n· 左侧导航按钮加高，图标与文字完整显示，默认与选中态尺寸、位置一致\n· 首页推荐 / 帖子切换时自动关闭已打开的帖子；每次点击「帖子」都强制刷新帖子流\n· 修复 COS 等帖子正文缺图：结构化正文漏掉的图片自动补全并保持顺序；灯箱支持滚轮缩放与左键拖动\n· 评论输入框改矮改圆，评论图片可显示；点「回复」直接在该条评论下展开内联输入框\n· 帖子与视频作者头像可进入用户主页；帖子与视频作者右侧「关注」按钮同步米游社官方关注数据\n· 视频画质 / 倍速选项改为清晰白色文字；评论输入框与发送按钮改矮圆；回复 @ 文本统一为灰色；点击评论区不再暂停视频\n· 「我的」更换背景时先选图片显示区域；点击角色卡展示官方公开战绩详情；新增发布、评论、合集、收藏四个内容入口\n· 设置页播放 / 缓存 / 维护同栏等高，窗口 / 通用同栏等高；更新日志移入应用维护并改为点击外部关闭的抽屉弹窗\n· 搜索优先展示相关用户，再展示用户发布的内容\n\n────────────────────────────────────────\n\nv0.1.7 · 2026-11-23\n· 左侧导航图标换成新版灰线稿 / 米蓝选中样式，顺序为首页、视频、关注、我的，设置固定在左下并统一对齐\n· 首页「官方」改为「帖子」：新增分区、版块、排序三联筛选，覆盖大别野、原神、星穹铁道、崩坏3、未定事件簿、绝区零、崩坏学园2、因缘精灵、星布谷地\n· 帖内图片支持滚轮缩放，正文沿用官方富文本样式与话题标签，链接一律在软件内打开\n· 首页帖子与关注页右下角新增「刷新」「返回顶部」；进入帖子可评论、楼中楼回复\n· 视频评论区支持回复与发送，画质、倍速改为抽屉式选择，弹幕可开关，点赞收藏有变色反馈并同步官方数据\n· 我的页移除 Lv.0，新增恢复默认背景并修复第二次更换背景失效；角色管理可打开对应游戏详情与帖子\n· 设置页重排为单列分区，目录点击与选中态同步，账号信息实时同步，移除开源说明\n· 首页 / 视频启动页改为互斥单选，发现新版本直接在软件内弹窗更新\n\n────────────────────────────────────────\n\nv0.1.6 · 2026-11-23\n· 左侧导航「动态」文案改为「关注」\n· 左侧导航换成手绘图标：默认灰色线稿，选中时米蓝填充 + 深色描边\n· 导航顺序调整为首页 / 关注 / 视频 / 我的，设置固定在左下\n\n────────────────────────────────────────\n\nv0.1.5 · 2026-11-22\n· 左侧导航改用圆润图标；阅读器支持图片灯箱、内嵌视频拖动进度、正文链接与右下角回顶 / 评论按钮\n· 视频页支持关注、评论数字、发布页、点击暂停与可拖动进度条\n· 动态页显示关注用户的新旧帖子；我的页支持自定义背景；设置页改为多列卡片\n· 新增应用内检查更新、下载进度与忽略本次版本\n\n────────────────────────────────────────\n\nv0.1.4 · 2026-11-22  (｡･ω･｡)ﾉ♡\n· 设置顶部新增「访问项目主页（GitHub）」与「开发者首页（支持、讨论、问题反馈）」两个跳转按钮\n· 首页帖子改为官方富文本样式：正文、图片、视频按 structured_content 原顺序混排，长文与长图可折叠展开\n· 视频改为本机 Range 代理边缓冲边播放，进度条新增可拖动圆点，播放数前显示作者关注数，去掉「第 1/12」序号\n· 自动预加载后三个视频；右侧「收藏」下新增「评论」，同步视频帖评论，登录后可直接发表\n· 修复暂停后画面停住但声音继续；「自动」按钮改为真实切换 1080P / 720P / 480P 并保持播放位置\n· 弹幕可发送，按视频保存在本地 danmaku.json，并滚动回放本机用户发过的弹幕\n· 登录改为应用内嵌官方通行证二维码，扫码后自动换取 cookie_token / ltoken；「我的」按官方样式重做为蓝色横幅个人主页与角色管理\n· 补齐从 0.1.0 到 0.1.4 的全部更新日志\n· 增加单实例互斥：同一台电脑只允许一个 MikoMiko 进程，避免多开同时写设置与登录态造成数据错乱\n\n────────────────────────────────────────\n\nv0.1.3 · 2026-11-22\n· 首页搜索改为「搜索你感兴趣的内容」，直接走米游社全站搜索，帖子覆盖所有板块并随机出现\n· 阅读器支持登录后评论 / 回复，评论会同步到米游社官方\n· 视频页去掉滑动提示文字，进度条改为三色：白色未加载、灰色已缓冲、米蓝已播放\n· 视频改为全站所有用户随机流，点「视频」导航即可换一批\n· 修复扫码登录：兼容新版 ltoken_v2 / cookie_token_v2 Cookie，登录态持久保存\n· 设置右侧新增目录快捷跳转；十套米游社配色；移除下载目录；缓存每 5 分钟自动清理\n· 应用信息改为名称 / 版本 / 框架 / 数据目录，可打开与更改数据目录\n· 更新日志改为抽屉式展示\n· 修复关闭窗口时因托盘图标资源缺失导致的未处理异常\n\n────────────────────────────────────────\n\nv0.1.2 · 2026-11-21\n· 启用新的 MikoMiko 手写泡泡字标志与蓝色渐变图标\n· 设置页改为整页滚动布局，新增外观、启动、播放、通知、维护等分区\n· 「我的」新增未登录引导与 Cookie 绑定入口，「动态」新增关注流引导\n· 阅读器补齐评论与楼中楼展示，视频播放器支持出帧截图\n· 优化首页卡片布局、封面加载与深浅色切换细节\n\n────────────────────────────────────────\n\nv0.1.1 · 2026-11-20\n· 主色调整为米蓝 #4EA4DD，新增十套页面配色\n· 首页改为全站随机帖子流，下滑自动加载更多\n· 视频页上下滑动切换随机视频，保留米游社原生点赞 / 收藏 / 分享\n· 点赞与收藏在绑定 Cookie 后同步到米游社账号\n· 设置页支持整页滚动浏览\n\n────────────────────────────────────────\n\nv0.1.0 · 2026-11-19\n· MikoMiko 首个版本：WPF + .NET 8 桌面客户端工程搭建完成\n· 完成主窗口、左侧导航、首页、视频、动态、我的、搜索与设置基础页面\n· 接入米游社公开接口，支持首页帖子流、帖子详情、视频列表与搜索\n· 支持浅色 / 深色主题、主色切换、本地设置持久化与图片缓存\n· 内置离线演示数据，接口不可用时仍可浏览界面"),


        ("About.Title", "MikoMiko"),
        ("About.Slogan", "一个更轻盈的米游社桌面客户端"),
        ("About.Desc", "MikoMiko 以「米游社」社区内容为核心，提供资讯浏览、帖子阅读、视频播放、关注动态与账号资料管理。项目完全开源，遵循 MIT 许可证。"),
        ("OpenSource.Desc", "本项目在架构与交互上参考了下列优秀的开源项目，在此向作者致谢："),
        ("Disclaimer.Desc", "MikoMiko 为非官方第三方客户端，与米哈游（miHoYo）无任何隶属关系。所有内容版权归原作者及米哈游所有，本程序仅作学习与技术交流使用，请勿用于商业用途。")
    );

    private static readonly Dictionary<string, string> ZhHant = Build(
        ("Nav.Home", "首頁"), ("Nav.Video", "影片"), ("Nav.Moment", "關注"), ("Nav.Mine", "我的"), ("Nav.Upload", "上傳"), ("Nav.Settings", "設定"),
        ("Header.Brand", "MikoMiko"), ("Header.Search", "搜尋你感興趣的內容"),
        ("Common.Loading", "正在載入…"), ("Common.Refresh", "重新整理"), ("Common.More", "載入更多"), ("Common.Empty", "這裡空空的，去逛逛別的吧"),
        ("Common.Fail", "內容載入失敗，請稍後重試"), ("Common.Retry", "重試"), ("Common.Cancel", "取消"), ("Common.Confirm", "確定"),
        ("Common.Close", "關閉"), ("Common.Back", "返回"), ("Common.OpenInBrowser", "在瀏覽器中開啟"), ("Common.Follow", "關注"), ("Common.Followed", "已關注"),
        ("Common.DemoMode", "示範資料"), ("Common.Offline", "目前為離線示範資料"),

        ("Home.Recommend", "推薦"), ("Home.Posts", "帖子"), ("Home.Anime", "追番/影視"), ("Home.Official", "官方"), ("Home.Latest", "最新"), ("Home.Empty", "暫時沒有找到相關內容"),
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
        ("S.Changelog.Body", "v0.1.1 · 2026-11-22\n· 主色調整為米藍 #4EA4DD，新增十套頁面配色\n· 首頁改為全站隨機貼文流，下滑自動載入更多\n· 影片頁上下滑動切換隨機影片，僅保留米遊社原生按讚 / 收藏 / 分享\n· 按讚與收藏在綁定 Cookie 後同步到米遊社帳號\n· 設定頁支援整頁捲動瀏覽"),

        ("About.Title", "MikoMiko"),
        ("About.Slogan", "一個更輕盈的米遊社桌面客戶端"),
        ("About.Desc", "MikoMiko 以「米遊社」社群內容為核心，提供資訊瀏覽、貼文閱讀、影片播放、關注動態與帳號資料管理。專案完全開源，遵循 MIT 授權。"),
        ("OpenSource.Desc", "本專案在架構與互動上參考了下列優秀的開源專案，在此向作者致謝："),
        ("Disclaimer.Desc", "MikoMiko 為非官方第三方客戶端，與米哈遊（miHoYo）無任何隸屬關係。所有內容版權歸原作者及米哈遊所有，本程式僅供學習與技術交流，請勿用於商業用途。")
    );

    private static readonly Dictionary<string, string> En = Build(
        ("Nav.Home", "Home"), ("Nav.Video", "Videos"), ("Nav.Moment", "Following"), ("Nav.Mine", "Me"), ("Nav.Upload", "Upload"), ("Nav.Settings", "Settings"),
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
        ("S.Changelog.Body", "MikoMiko full changelog (0.1.0 → 0.1.20)\n\n────────────────────────────────────────\nv0.1.20 · 2026-11-24\n· The Anime / Films install page now shows the supplied artwork as one centred, uncropped image, with the pill button starting the module download\n· The film home is rebuilt to the new mockup: white search bar, full-width blue-and-white hero, and a 7-column guess-you-like wall that reshuffles on every visit\n· All Content gains type, sort, region and genre filter rows with a 7-column wall; fixes off-site posters blocked by Referer checks\n· Opening a title now leads to a Bilibili-style player: video on the left, description / follow / sources / episode grid on the right, with free source and episode switching and a highlighted current episode\n· Leaving Anime / Films closes the player and disposes WebView2, stopping audio immediately; returning to the home page refreshes the random picks\n· Source numbering corrected: Anime 1 is zhuidm and Anime 2 is yinghdm, with the module bumped to 1.3.0\n\n────────────────────────────────────────\n\nv0.1.19 · 2026-11-24\n· The Anime / Films install page now uses the supplied artwork as-is instead of a redraw, and the large pill button starts the module download\n· Anime / Films playback now resolves the real video stream and plays it in a built-in HLS / MP4 player instead of showing third-party web pages\n· Fixed video comments: clicking the comment body now replies to another user's comment\n· My Posts / Comments / Collections / Favorites no longer show a blue background on mouse hover\n· Settings defaults When closing the window to Minimize to tray, and Cache / App Info plus General / Maintenance cards are rebalanced into equal-height rows\n\n────────────────────────────────────────\n\nv0.1.18 · 2026-11-24\n· Anime / Films home rebuilt to the new mockup: floating top search, full-bleed hero with a bottom-right play button, today's updates rail, left categories and a 7-column poster wall\n· Fixes QR login -100 by using the website QR flow, reading full credentials from Set-Cookie, and validating through the community profile API first\n· Settings cards now share one width and alignment, with maintenance / app info and about / disclaimer rows at equal height\n\n────────────────────────────────────────\n\nv0.1.17 · 2026-11-24\n· The Anime / Films install page is rebuilt with a white and pale-blue soft look: a chibi mascot leaning over a large pill download button, with no search box or site links before installation\n· The search bar, category rail and native film home appear only after the module is installed; uninstalling returns to the download page\n· Anime / Films officially aggregates nine sources, with details and playback kept inside the app\n\n────────────────────────────────────────\n\nv0.1.16 · 2026-11-24\n· Anime / Films shows only one download button before the module is installed, hiding the top search box and bottom site links; installing opens the native film home\n· The film home is rebuilt as search bar + left categories + large hero + today's updates rail + a guess-you-like poster wall, covering CN / JP-KR / US-EU / Animation / HK-TW / Overseas / All / Today / Trending / News\n· Aggregates nine sites: YingHDM 1, YingHDM 2, YHDM TV, PIDEC, Nandou, Qimao, Silidm, Manmankan and Mahua\n· Details and playback stay inside the app; the player hides the address bar and cleans common overlay ads without opening the system browser\n· Search works across sources with a local-cache fallback and a clear empty state\n· Fixes third-party posters blocked by a fixed Miyoushe Referer; failed covers show a title gradient instead of a blank post placeholder\n\n────────────────────────────────────────\n\nv0.1.15 · 2026-11-24\n· In-post video seeking buffers from the drop point, and controls stay visible during playback\n· Like hearts in post and video comments turn red; post titles, bodies and comments support mouse text selection and copy\n· Clicking a video comment replies to it; avatars and nicknames open profiles; clicking outside the panel closes comments\n· Video quality switching applies to the actual source; the saved-location toast clears after 1 second; a danmaku disclaimer sits right of Send\n· Avatars and nicknames in Following moments open the user profile\n· My Posts / Comments / Collections / Favorites lose their hover effect\n· The upload page is restored to its original full layout, no longer hiding the logo, Miyoushe label, navigation or search box; login state is still injected\n· Login state is injected into the upload page to avoid a second QR scan; Settings gains open-source notes and a cleaner layout\n\n────────────────────────────────────────\n\nv0.1.14 · 2026-11-24\n· Post comments gain Default / Latest sorting; the like heart turns red, dislike turns the icon gray and collapses that comment, and undoing dislike restores it\n· Comment avatars and nicknames open the user profile, including nested replies\n· Video like counts update instantly by +1 / -1; video comments gain sorting, replies, an image lightbox and a bottom-right download\n· All moments sort newest first by publish time; clicking a user in a moment shows only that user's content on the right\n· Character settings drop Show game record details; the background crop preview marks the visible area with diagonal stripes\n· The upload page hides the site logo, Miyoushe label, Home / Campus / ACG navigation and search box, keeping only the top-right avatar login\n· The nine publish communities use their matching artwork; Settings switches to a two-column equal-width card layout\n────────────────────────────────────────\n\nv0.1.13 · 2026-11-24\n· Video feed author cooldown shortened from 7 to 3 days with automatic cleanup; official videos are exempt\n· Home gains an Anime / Films section with independent download and uninstall, aggregating nine sites including YHDM, PIDEC, Nandou, Qimao, Silidm, Manmankan and Mahua\n· The selected Home tab text turns #4EA4DD; entering Anime / Films hides the header global search and keeps only the section search\n· New film home and a More page with type, sort, region and source filters, with episode and completion labels on posters\n· The All moments icon on Following turns pale blue and the avatar red dot is removed\n· Changelog drops author-badge entries and adds separators between versions\n\n────────────────────────────────────────\n\nv0.1.12 · 2026-11-24\n· Video random feed adds author cooldown: the author actually shown is recorded by UID and nickname and is not pushed again for 7 days, then cleared automatically\n· Video pool now fills from parallel searches plus parallel multi-community lists, cutting serial waits and covering more authors and older posts\n· Following page is now a left following-user rail plus a right moment stream, with All and per-user switching and red dots for users with moments\n· New blue-star upload entry above Settings: pick one of nine communities, then long article / images / video, and jump straight to the Miyoushe publish page\n\n────────────────────────────────────────\n\nv0.1.11 · 2026-11-23\n· Home / Videos / Me default navigation icons redrawn to match Following and Settings in blue-gray two-tone outline\n· Video progress bar fixed at 3px with a draggable white dot and no hover thickening or focus chrome, following Bilibili-style interaction\n· User profile uses the ice-blue girl banner; posts page across every community and include video, game characters show the game UID, and clicking one opens its record details\n· Fixed nested video-comment reply and like command bindings so child comments can be replied to\n· Following feed now uses the account-level full following list and pulls every community's latest posts per user\n· Me → character settings gain a Show game record details switch\n· Search related users show two rows by default, with a More users expansion on the right\n· Home recommendations and empty searches no longer show related users; only keyword searches do\n· In-post video controls appear on hover and hide on leave, play / pause use white icons, clicking the picture toggles playback, and the lightbox download is icon-only\n· Window settings removed from Settings; Cache, General and Maintenance cards rearranged\n\n────────────────────────────────────────\n\nv0.1.10 · 2026-11-23\n· Following / Settings default icons rebuilt from the selected artwork with identical outline, weight and size\n· Search category labels removed, showing matching users and mixed content directly\n· Comment send / reply now reuse the 2.102 web signature and parameters, omitting reply_id for top-level comments to fix invalid request\n· Video quality moves next to the speed control; progress bar has no hover change and buffers from the drop point; like / favorite no longer reloads the video and guests see a sign-in prompt\n· Mine character cards use three columns with a gear menu to hide games and reorder them; six official game icons\n· User profiles add Miyoushe UID, available tags, Posts / Comments / Collections / Favorites tabs, per-user content search and game characters\n· Settings gain a page title, natural card heights and tighter spacing\n· In-post video supports click-to-pause and an explicit play / pause button with free seeking\n\n────────────────────────────────────────\n\nv0.1.9 · 2026-11-23\n· Following / Settings navigation icons normalized to the same visual size in default and selected states\n· Settings header now offers GitHub project home (system browser) and developer home (in-app); old About buttons removed\n· Window-size picker and playback settings removed; after login the account button becomes Go to My Profile\n· New download location in Settings with change / open actions; post images and videos plus video page can be saved\n· Video feed randomly mixes official and user videos with no author repeat within five items; fixes comment invalid request; comment author color is #4EA4DD\n· Post videos gain a draggable progress bar and download; image lightbox adds previous / next and download\n· My game cards show public record stats and game icons with hover removed; posts / comments / collections / favorites become one tab bar\n· Search gains All / Guides / Videos / Wiki / Images / Users / Topics tabs\n· Following cards lose hover and support direct like / favorite; comment images and emoji now load\n· Three-dot menu on video page switches quality and downloads; progress bar hover effect removed\n· Random rotating placeholders for search and video danmaku inputs\n\n────────────────────────────────────────\n\nv0.1.8 · 2026-11-23\n· Taller navigation buttons so icons and labels fully show, with identical default / selected size and alignment\n· Switching Home Recommend / Posts closes the open reader; every Posts click force-refreshes the feed\n· Fixes missing COS post images by appending uncovered structured images in order; lightbox supports wheel zoom and left-button drag\n· Shorter, rounder comment composer, comment images visible, and Reply opens an inline input under that comment\n· Post / video author avatars open the user profile; Follow buttons sync with official Miyoushe data\n· Video quality / speed options use clearly visible white text, shorter round comment composer, gray reply mentions, and comment-area clicks no longer pause playback\n· Mine background now lets you pick the visible crop; game cards show official public record stats; adds My Posts, Comments, Collections and Favorites\n· Settings groups Playback / Cache / Maintain and Window / General into equal-height rows; changelog moved under Maintain as an outside-click drawer\n· Search shows matching users first, then their content\n\n────────────────────────────────────────\n\nv0.1.7 · 2026-11-23\n· New gray-outline / blue-selected navigation icons in the order Home, Videos, Following, Me, with Settings pinned bottom-left\n· Home Official tab renamed Posts, with section, board and sort filters for all nine communities\n· Post images zoom with the mouse wheel, official rich text and topic tags are preserved, links open inside the app\n· Refresh and back-to-top buttons on Home posts and Following; comments and nested replies available in the reader\n· Video comments can be replied to and sent; quality and speed use drawers, danmaku toggles, likes and favorites give color feedback and sync with the official account\n· Me page drops the Lv.0 badge, restores the default background and fixes repeated background changes; game cards open game details and posts\n· Settings reflowed into one column with synced section highlighting, live account sync and the open-source card removed\n· Home / Videos startup choices are mutually exclusive; new versions prompt an in-app update dialog\n\n────────────────────────────────────────\n\nv0.1.6 · 2026-11-23\n· Left navigation label changed from Feed to Following\n· New hand-drawn navigation icons: gray outline by default, blue fill with dark outline when selected\n· Navigation order now Home / Following / Videos / Me, with Settings pinned at the bottom\n\n────────────────────────────────────────\n\nv0.1.5 · 2026-11-22\n· Rounded navigation icons; reader image lightbox, draggable inline video, body links, back-to-top and comment buttons\n· Video page adds follow, comment count, publish page, click-to-pause and a draggable progress bar\n· Following feed shows old and new posts; custom Mine background; multi-column settings cards\n· In-app update check, download progress and ignore-this-version\n\n────────────────────────────────────────\n\nv0.1.4 · 2026-11-22\n· Settings header adds GitHub home and developer feedback links\n· Home posts render official rich text: text, images and videos keep their original order, long text can be expanded\n· Video uses a local Range proxy for real buffering, draggable progress thumb, author follower count before views, page index removed\n· Preloads the next three videos; adds a comment drawer under Favorite with synced comments and posting after login\n· Fixes audio continuing after pause; Auto now really cycles 1080P / 720P / 480P and keeps playback position\n· Danmaku can be sent, stored locally per video and replayed for local users\n· Login is an embedded official passport QR panel; Mine is rebuilt as a blue banner profile with character management\n· Full changelog from 0.1.0 to 0.1.4\n· Single-instance mutex prevents multiple processes from overwriting settings and login state\n\n────────────────────────────────────────\n\nv0.1.3 · 2026-11-22\n· Site-wide search, comments and replies synced to Miyoushe, three-color video progress bar, random all-site video feed\n· Fixed QR login cookies, settings section index, ten accent presets and five-minute cache cleanup\n· App info now shows name / version / runtime / data directory\n\n────────────────────────────────────────\n\nv0.1.2 · 2026-11-21\n· New wordmark and blue gradient icon, full-page settings, Mine and Moment guest guides, reader comments and video capture\n\n────────────────────────────────────────\n\nv0.1.1 · 2026-11-20\n· Accent switched to Miko Blue #4EA4DD with ten presets, random home feed, swipe video page and synced likes / favorites\n\n────────────────────────────────────────\n\nv0.1.0 · 2026-11-19\n· First WPF + .NET 8 build with main window, navigation, home, video, moment, mine, search and settings\n· Miyoushe public APIs, light / dark themes, settings persistence, image cache and offline demo data"),

        ("About.Title", "MikoMiko"),
        ("About.Slogan", "A lighter Miyoushe desktop client"),
        ("About.Desc", "MikoMiko focuses on Miyoushe community content: news, posts, videos, followed feeds and account info. The project is fully open source under the MIT license."),
        ("OpenSource.Desc", "The architecture and interactions of this project were inspired by these excellent open-source projects. Thanks to their authors:"),
        ("Disclaimer.Desc", "MikoMiko is an unofficial third-party client and is not affiliated with miHoYo. All content belongs to its original authors and miHoYo. This app is for learning and technical exchange only, not for commercial use.")
    );

    private static readonly Dictionary<string, string> Ja = Build(
        ("Nav.Home", "ホーム"), ("Nav.Video", "動画"), ("Nav.Moment", "フォロー"), ("Nav.Mine", "マイ"), ("Nav.Upload", "アップロード"), ("Nav.Settings", "設定"),
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
        ("Nav.Home", "Главная"), ("Nav.Video", "Видео"), ("Nav.Moment", "Подписки"), ("Nav.Mine", "Профиль"), ("Nav.Upload", "Загрузить"), ("Nav.Settings", "Настройки"),
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
        ("Nav.Home", "홈"), ("Nav.Video", "영상"), ("Nav.Moment", "팔로잉"), ("Nav.Mine", "내 정보"), ("Nav.Upload", "업로드"), ("Nav.Settings", "설정"),
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








