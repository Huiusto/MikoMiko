using MikoMiko.Localization;
using System.Collections.Generic;

namespace MikoMiko.Services;

/// <summary>
/// 启动页 WebView2 中仍为硬编码中文的文案。切到非简体中文时，
/// 由 Loc 生成“原文 → 译文”对照表并随状态推送给 launch.html。
/// </summary>
internal static class LaunchWebTexts
{
    internal static readonly string[] Keys =
    {
        "MikoMiko 启动页",
        "米哈云游",
        "全部游戏 ▾",
        "当前游戏：原神",
        "当前游戏：",
        "未找到本地游戏，点「获取游戏」可直接在 MikoMiko 内安装；安装后也能点「定位游戏」选择已有目录。",
        "获取游戏",
        "游戏设置",
        "定位游戏",
        "预约游戏",
        "未上线 · 预约",
        "官方预下载",
        "预下载",
        "开始游戏",
        "正在下载游戏资源",
        "剩余时间：计算中",
        "剩余时间：",
        "下载",
        "安装",
        "游戏设置 · 原神",
        "游戏基本信息、安装目录与安装 / 卸载管理。",
        "游戏",
        "主程序",
        "安装目录",
        "未定位",
        "所在磁盘可用空间",
        "启动页显示",
        "显示「预约游戏」卡片",
        "显示「米哈云游」按钮",
        "官方启动器壁纸",
        "当前背景",
        "官方壁纸",
        "打开安装目录",
        "重新定位游戏",
        "卸载游戏",
        "关闭",
        "壁纸控制",
        "保存当前壁纸",
        "暂停 / 继续动态壁纸",
        "当前为静态壁纸",
        "正在下载官方动态壁纸…",
        "选择要启动的游戏 · 用 ↑ ↓ 调整顺序",
        "已定位",
        "运行中",
        "上移",
        "下移",
        "正在取消安装…",
        "继续安装",
        "暂停安装",
        "游戏启动中",
        "取消安装并清理",
        "，点击打开米哈游官方预下载",
        "当前没有官方预下载",
        "安装已暂停",
        "已暂停，等待继续",
        "等待下载完成",
        "暂无官方壁纸，正在同步…",
        "该动态壁纸无法播放，已显示静态壁纸",
        "动态",
    };

    internal static Dictionary<string, string> BuildMap()
    {
        var map = new Dictionary<string, string>();
        if (Loc.Instance.Language == "zh-CN") return map;
        foreach (var key in Keys)
        {
            var translated = Loc.Instance.TranslateText(key);
            if (!string.IsNullOrEmpty(translated) && translated != key) map[key] = translated;
        }
        return map;
    }
}
