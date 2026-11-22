using MikoMiko.Models;

namespace MikoMiko.Services;

/// <summary>离线演示数据：网络不可用或接口异常时保证界面可用。</summary>
public static class DemoData
{
    private static string Cover(int index) => $"pack://application:,,,/Assets/Demo/cover{index:00}.jpg";
    private static string Avatar(int index) => $"pack://application:,,,/Assets/Demo/avatar{index:00}.png";

    public static IReadOnlyList<FeedItem> Home() => new List<FeedItem>
    {
        new()
        {
            Id = "demo-01", PostId = "demo-01", Title = "「星穹铁道」2.7 版本前瞻：新角色、新星系与新的旅途",
            Cover = Cover(12), Author = "星穹列车广播站", AuthorAvatar = Avatar(1), TimeText = "2 小时前",
            ViewText = "18.6万", ReplyText = "1240", LikeText = "2.3万", TagText = "2.3万点赞",
            IsVideo = true, DurationText = "12:36", ForumName = "星穹铁道",
        },
        new()
        {
            Id = "demo-02", PostId = "demo-02", Title = "原神 · 提瓦特摄影集：风起地的午后，光落在蒲公英上",
            Cover = Cover(02), Author = "镜水行舟", AuthorAvatar = Avatar(2), TimeText = "5 小时前",
            ViewText = "6.2万", ReplyText = "386", LikeText = "9821", ForumName = "原神",
        },
        new()
        {
            Id = "demo-03", PostId = "demo-03", Title = "绝区零 · 新艾利都街景手绘：霓虹、雨夜与一只路过的邦布",
            Cover = Cover(13), Author = "六分街画师", AuthorAvatar = Avatar(3), TimeText = "昨天 21:40",
            ViewText = "4.1万", ReplyText = "220", LikeText = "7654", ForumName = "绝区零",
        },
        new()
        {
            Id = "demo-04", PostId = "demo-04", Title = "崩坏3 · 女武神的新衣：第七代装甲设定稿公开",
            Cover = Cover(11), Author = "休伯利安档案室", AuthorAvatar = Avatar(4), TimeText = "昨天 18:12",
            ViewText = "9.8万", ReplyText = "802", LikeText = "1.6万", TagText = "1.6万点赞", ForumName = "崩坏3",
        },
        new()
        {
            Id = "demo-05", PostId = "demo-05", Title = "米游社同人图征集开启：这一届的主题是「夏天的一百种颜色」",
            Cover = Cover(05), Author = "米游社活动组", AuthorAvatar = Avatar(5), TimeText = "2 天前",
            ViewText = "12.4万", ReplyText = "1902", LikeText = "3.4万", TagText = "3.4万点赞", IsOfficial = true, ForumName = "大别野",
        },
        new()
        {
            Id = "demo-06", PostId = "demo-06", Title = "忘却之庭 12 层低配通关思路：先手顺序比面板更重要",
            Cover = Cover(14), Author = "深渊作业本", AuthorAvatar = Avatar(6), TimeText = "2 天前",
            ViewText = "3.6万", ReplyText = "415", LikeText = "6203", IsVideo = true, DurationText = "08:52", ForumName = "星穹铁道",
        },
        new()
        {
            Id = "demo-07", PostId = "demo-07", Title = "壶中洞天设计分享：把温泉旅馆搬进尘歌壶",
            Cover = Cover(06), Author = "壶中建筑师", AuthorAvatar = Avatar(7), TimeText = "3 天前",
            ViewText = "2.8万", ReplyText = "164", LikeText = "4390", ForumName = "原神",
        },
        new()
        {
            Id = "demo-08", PostId = "demo-08", Title = "「Teyvat」主题曲钢琴改编：把整片星空弹进八音盒",
            Cover = Cover(01), Author = "提瓦特琴房", AuthorAvatar = Avatar(8), TimeText = "3 天前",
            ViewText = "1.9万", ReplyText = "98", LikeText = "3120", IsVideo = true, DurationText = "04:18", ForumName = "音乐",
        },
        new()
        {
            Id = "demo-09", PostId = "demo-09", Title = "手作教程：用热缩片做一只会发光的派蒙挂件",
            Cover = Cover(09), Author = "手工小作坊", AuthorAvatar = Avatar(1), TimeText = "4 天前",
            ViewText = "5.5万", ReplyText = "530", LikeText = "8760", ForumName = "手作·绘画",
        },
        new()
        {
            Id = "demo-10", PostId = "demo-10", Title = "未定事件簿 · 新章节剧情梳理：那些藏在台词里的伏笔",
            Cover = Cover(04), Author = "案卷整理员", AuthorAvatar = Avatar(3), TimeText = "4 天前",
            ViewText = "2.2万", ReplyText = "140", LikeText = "2980", ForumName = "未定事件簿",
        },
        new()
        {
            Id = "demo-11", PostId = "demo-11", Title = "米游社新人指南：从注册到第一次发帖，只需要三步",
            Cover = Cover(08), Author = "社区向导", AuthorAvatar = Avatar(2), TimeText = "5 天前",
            ViewText = "8.4万", ReplyText = "720", LikeText = "1.1万", TagText = "1.1万点赞", IsOfficial = true, ForumName = "大别野",
        },
        new()
        {
            Id = "demo-12", PostId = "demo-12", Title = "摄影 · 校园里的秋天：银杏落满台阶的那个下午",
            Cover = Cover(15), Author = "取景框", AuthorAvatar = Avatar(5), TimeText = "6 天前",
            ViewText = "1.3万", ReplyText = "66", LikeText = "1870", ForumName = "生活记录",
        },
    };

    public static IReadOnlyList<FeedItem> Videos() => new List<FeedItem>
    {
        new()
        {
            Id = "v-01", PostId = "v-01", Title = "「原神」拾枝杂谈 · 钟离：炊金馔玉",
            Cover = Cover(03), Author = "原神", AuthorAvatar = Avatar(1), TimeText = "11-06",
            ViewText = "42.7万", ReplyText = "7090", LikeText = "9.9万", TagText = "9.9万点赞",
            IsVideo = true, DurationText = "11:35", ForumName = "官方视频",
        },
        new()
        {
            Id = "v-02", PostId = "v-02", Title = "星穹铁道 2.7 版本 PV「群星的另一端」",
            Cover = Cover(10), Author = "崩坏：星穹铁道", AuthorAvatar = Avatar(4), TimeText = "11-14",
            ViewText = "62.1万", ReplyText = "1830", LikeText = "12.4万", TagText = "12.4万点赞",
            IsVideo = true, DurationText = "02:41", ForumName = "官方视频",
        },
        new()
        {
            Id = "v-03", PostId = "v-03", Title = "绝区零 · 新艾利都的雨夜：一段没有台词的短片",
            Cover = Cover(13), Author = "六分街影像", AuthorAvatar = Avatar(6), TimeText = "11-16",
            ViewText = "18.9万", ReplyText = "420", LikeText = "3.2万", TagText = "3.2万点赞",
            IsVideo = true, DurationText = "06:12", ForumName = "实况·解说",
        },
        new()
        {
            Id = "v-04", PostId = "v-04", Title = "用 100 小时做一把「天空之刃」：木工与金属的浪漫",
            Cover = Cover(07), Author = "工房手记", AuthorAvatar = Avatar(2), TimeText = "11-18",
            ViewText = "9.6万", ReplyText = "260", LikeText = "1.8万", TagText = "1.8万点赞",
            IsVideo = true, DurationText = "19:08", ForumName = "手作·绘画",
        },
        new()
        {
            Id = "v-05", PostId = "v-05", Title = "提瓦特晚安曲：一小时无人声钢琴",
            Cover = Cover(01), Author = "提瓦特琴房", AuthorAvatar = Avatar(8), TimeText = "11-19",
            ViewText = "3.4万", ReplyText = "120", LikeText = "6800", IsVideo = true, DurationText = "58:20", ForumName = "音乐",
        },
        new()
        {
            Id = "v-06", PostId = "v-06", Title = "2.7 版本深渊满星实录：低配也能打得很漂亮",
            Cover = Cover(14), Author = "深渊作业本", AuthorAvatar = Avatar(3), TimeText = "11-20",
            ViewText = "12.7万", ReplyText = "512", LikeText = "2.6万", TagText = "2.6万点赞",
            IsVideo = true, DurationText = "14:22", ForumName = "实况·解说",
        },
    };

    public static IReadOnlyList<MomentItem> Moments() => new List<MomentItem>
    {
        new()
        {
            Id = "m-01", Author = "灰岁Huiusto", Avatar = Avatar(1), TimeText = "12 分钟前",
            Text = "今天把尘歌壶里的温泉旅馆重新摆了一遍，稻妻的樱花和须弥的陶罐混在一起，居然意外地好看。晚上整理一下布局，发出来给大家抄作业。",
            Topic = "#尘歌壶造景#", LikeText = "328", ReplyText = "42", IsFollowing = true,
            Images = { Cover(06), Cover(05), Cover(02) },
        },
        new()
        {
            Id = "m-02", Author = "镜水行舟", Avatar = Avatar(2), TimeText = "1 小时前",
            Text = "风起地的下午，蒲公英飞得比风还慢。",
            LikeText = "1.2万", ReplyText = "186", IsFollowing = true,
            Images = { Cover(02) },
        },
        new()
        {
            Id = "m-03", Author = "星穹列车广播站", Avatar = Avatar(4), TimeText = "3 小时前",
            Text = "「开拓者，前方到站——群星的另一端。」2.7 版本前瞻直播回顾已经整理好啦，新角色情报、版本活动与卡池安排都在里面。",
            Topic = "#星穹铁道#", LikeText = "2.6万", ReplyText = "720", IsOfficial = true, IsFollowing = true,
            Images = { Cover(10), Cover(12) },
        },
        new()
        {
            Id = "m-04", Author = "六分街画师", Avatar = Avatar(3), TimeText = "昨天 21:40",
            Text = "雨夜的新艾利都，霓虹被水洼接住，又被邦布踩碎。画了三个晚上，终于把那种潮湿的光画出来了。",
            Topic = "#绝区零#", LikeText = "9860", ReplyText = "204", IsFollowing = true,
            Images = { Cover(13), Cover(16) },
        },
        new()
        {
            Id = "m-05", Author = "提瓦特琴房", Avatar = Avatar(8), TimeText = "昨天 18:12",
            Text = "把《Teyvat》主题曲改编成八音盒的版本，试了七种音色，最后还是最朴素的钢片琴最像星空。",
            LikeText = "4210", ReplyText = "96", IsFollowing = true,
            VideoCover = Cover(01), DurationText = "04:18", IsVideo = true,
        },
        new()
        {
            Id = "m-06", Author = "手工小作坊", Avatar = Avatar(7), TimeText = "2 天前",
            Text = "热缩片派蒙完工！关灯以后会有一点点荧光，挂在包上像偷偷跟着你的小应急食品。",
            Topic = "#手作日常#", LikeText = "6340", ReplyText = "128", IsFollowing = false,
            Images = { Cover(09), Cover(11), Cover(04) },
        },
    };

    public static IReadOnlyList<GameAccount> Games() => new List<GameAccount>
    {
        new()
        {
            GameName = "原神", Uid = "100*****7", Server = "天空岛 · 官服", LevelLabel = "冒险等级", Level = "60",
            ExtraLabel = "世界等级", Extra = "8", AccentHex = "#7FA8FF", Badge = "提瓦特",
        },
        new()
        {
            GameName = "崩坏：星穹铁道", Uid = "102*****3", Server = "星穹列车 · 官服", LevelLabel = "开拓等级", Level = "70",
            ExtraLabel = "均衡等级", Extra = "6", AccentHex = "#B08CFF", Badge = "银河",
        },
        new()
        {
            GameName = "绝区零", Uid = "130*****9", Server = "新艾利都 · 官服", LevelLabel = "绳网等级", Level = "55",
            ExtraLabel = "探索等级", Extra = "5", AccentHex = "#FFB36B", Badge = "新艾利都",
        },
    };

    public static IReadOnlyList<FeedItem> Favorites() => Home().Take(4).ToList();
}
