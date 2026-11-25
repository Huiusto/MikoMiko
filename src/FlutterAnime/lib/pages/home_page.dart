import 'package:flutter/material.dart';

import '../services/host_bridge.dart';
import '../ui/theme.dart';

/// 固定片单：仅保留用户指定的哔哩哔哩番剧与音乐会。
class AnimeEntry {
  const AnimeEntry({
    required this.title,
    required this.subtitle,
    required this.cover,
    required this.url,
    required this.badge,
  });

  final String title;
  final String subtitle;
  final String cover;
  final String url;
  final String badge;
}

const List<AnimeEntry> bangumiEntries = <AnimeEntry>[
  AnimeEntry(
    title: '女武神的餐桌',
    subtitle: '崩坏3 · 官方料理番外',
    cover: 'assets/covers/c01.jpg',
    url: 'https://www.bilibili.com/bangumi/play/ss27991',
    badge: '番剧',
  ),
  AnimeEntry(
    title: '女武神的餐桌 第二季',
    subtitle: '崩坏3 · 第二季',
    cover: 'assets/covers/c02.jpg',
    url: 'https://www.bilibili.com/bangumi/play/ss33688',
    badge: '番剧',
  ),
  AnimeEntry(
    title: '人偶学园',
    subtitle: '崩坏3 · 学园日常',
    cover: 'assets/covers/c03.png',
    url: 'https://www.bilibili.com/bangumi/play/ss38966',
    badge: '番剧',
  ),
  AnimeEntry(
    title: '黄金庭院：冬日里的新年愿望',
    subtitle: '崩坏3 · 冬日特别篇',
    cover: 'assets/covers/c04.jpg',
    url: 'https://www.bilibili.com/bangumi/play/ss44066',
    badge: '番剧',
  ),
  AnimeEntry(
    title: '旅途危机！圣芙蕾雅号特别事件',
    subtitle: '崩坏3 · 特别篇',
    cover: 'assets/covers/c05.png',
    url: 'https://www.bilibili.com/bangumi/play/ss47106',
    badge: '番剧',
  ),
  AnimeEntry(
    title: '《原神》长期项目启动·概念PV',
    subtitle: '原神 · 官方概念 PV',
    cover: 'assets/covers/c06.jpg',
    url: 'https://www.bilibili.com/video/BV15B4y1n7dg',
    badge: 'PV',
  ),
  AnimeEntry(
    title: '《崩坏：星穹铁道》原创动画•概念PV——「死于午后」',
    subtitle: '星穹铁道 · 原创动画概念 PV',
    cover: 'assets/covers/c07.jpg',
    url: 'https://www.bilibili.com/video/BV1YXDfBUETP',
    badge: 'PV',
  ),
];

const List<AnimeEntry> concertEntries = <AnimeEntry>[
  AnimeEntry(
    title: '崩坏3「林海绮律」',
    subtitle: '崩坏3 · 线上音乐会',
    cover: 'assets/covers/c08.jpg',
    url: 'https://www.bilibili.com/bangumi/play/ss45573',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '崩坏3「纯真梦歌」',
    subtitle: '崩坏3 · 线上音乐会',
    cover: 'assets/covers/c09.png',
    url: 'https://www.bilibili.com/bangumi/play/ss42711',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '崩坏3「星火流音」',
    subtitle: '崩坏3 · 纪念演出',
    cover: 'assets/covers/c10.png',
    url: 'https://www.bilibili.com/bangumi/play/ss39777',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '未定事件簿「心乐未名」',
    subtitle: '未定事件簿 · 音乐会官方完整版',
    cover: 'assets/covers/c11.jpg',
    url: 'https://www.bilibili.com/video/BV1nE826rEJ4',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '原神「无际之旅的旋律」',
    subtitle: '原神 · 2021 线上音乐会',
    cover: 'assets/covers/c12.jpg',
    url: 'https://www.bilibili.com/video/BV1E44y1t7Kn',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '原神「映春华章」',
    subtitle: '原神 · 交响音乐会特别篇',
    cover: 'assets/covers/c13.jpg',
    url: 'https://www.bilibili.com/video/BV1TY411L7ZD',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '原神交响音乐会2023',
    subtitle: '原神 · 上海场官方录播',
    cover: 'assets/covers/c14.jpg',
    url: 'https://www.bilibili.com/video/BV1Rj411p7b1',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '崩坏：星穹铁道「2024星铁LIVE」',
    subtitle: '星穹铁道 · 演唱会官方录播',
    cover: 'assets/covers/c15.jpg',
    url: 'https://www.bilibili.com/video/BV1ZH4y1V7AE',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '崩坏：星穹铁道「2025星铁LIVE」',
    subtitle: '星穹铁道 · 演唱会官方录播',
    cover: 'assets/covers/c16.jpg',
    url: 'https://www.bilibili.com/video/BV1aDGfziEGh',
    badge: '音乐会',
  ),
  AnimeEntry(
    title: '崩坏：星穹铁道「2026星铁奇妙夜」',
    subtitle: '星穹铁道 · 线上音乐演出官方录播',
    cover: 'assets/covers/c17.jpg',
    url: 'https://www.bilibili.com/video/BV16p9UBgEhT',
    badge: '音乐会',
  ),
];

class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth;
        final columns = width >= 1180
            ? 4
            : width >= 880
                ? 3
                : 2;
        return ScrollConfiguration(
          behavior: ScrollConfiguration.of(context).copyWith(scrollbars: false),
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(24, 18, 24, 30),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _HeroBanner(total: bangumiEntries.length + concertEntries.length),
                const SizedBox(height: 22),
                _SectionHeader(
                  title: '番剧',
                  subtitle: '崩坏3 与米哈游官方概念 PV',
                  count: bangumiEntries.length,
                ),
                const SizedBox(height: 12),
                _CardGrid(entries: bangumiEntries, columns: columns),
                const SizedBox(height: 26),
                _SectionHeader(
                  title: '音乐会',
                  subtitle: '米哈游官方音乐会与演唱会录播',
                  count: concertEntries.length,
                ),
                const SizedBox(height: 12),
                _CardGrid(entries: concertEntries, columns: columns),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _HeroBanner extends StatelessWidget {
  const _HeroBanner({required this.total});

  final int total;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 128,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(16),
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF2F86B9), Color(0xFF57B7E8), Color(0xFF9AD8F4)],
        ),
      ),
      child: Stack(
        children: [
          Positioned(
            right: -36,
            top: -70,
            child: Container(
              width: 230,
              height: 230,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: Colors.white.withValues(alpha: 0.12),
              ),
            ),
          ),
          Positioned(
            right: 66,
            bottom: -80,
            child: Container(
              width: 190,
              height: 190,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: Colors.white.withValues(alpha: 0.10),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(26, 22, 26, 20),
            child: Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 9,
                              vertical: 4,
                            ),
                            decoration: BoxDecoration(
                              color: Colors.white.withValues(alpha: 0.22),
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: const Text(
                              '哔哩哔哩官方片单',
                              style: TextStyle(
                                fontSize: 11,
                                color: Colors.white,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Text(
                            '$total 部内容',
                            style: TextStyle(
                              fontSize: 11,
                              color: Colors.white.withValues(alpha: 0.82),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      const Text(
                        '追剧 / 影视',
                        style: TextStyle(
                          fontSize: 25,
                          height: 1.05,
                          color: Colors.white,
                          fontWeight: FontWeight.w800,
                          letterSpacing: 0.5,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        '点击海报即可在软件内观看，弹幕与评论随哔哩哔哩页面同步。',
                        style: TextStyle(
                          fontSize: 12.5,
                          color: Colors.white.withValues(alpha: 0.9),
                        ),
                      ),
                    ],
                  ),
                ),
                Container(
                  width: 54,
                  height: 54,
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.18),
                    borderRadius: BorderRadius.circular(15),
                  ),
                  child: const Icon(
                    Icons.play_circle_fill_rounded,
                    color: Colors.white,
                    size: 34,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({
    required this.title,
    required this.subtitle,
    required this.count,
  });

  final String title;
  final String subtitle;
  final int count;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Container(
          width: 4,
          height: 18,
          margin: const EdgeInsets.only(right: 9, bottom: 1),
          decoration: BoxDecoration(
            color: MikoColors.primary,
            borderRadius: BorderRadius.circular(3),
          ),
        ),
        Text(
          title,
          style: const TextStyle(
            fontSize: 18,
            fontWeight: FontWeight.w800,
            color: MikoColors.ink,
          ),
        ),
        const SizedBox(width: 9),
        Padding(
          padding: const EdgeInsets.only(bottom: 2),
          child: Text(
            subtitle,
            style: const TextStyle(fontSize: 11.5, color: MikoColors.tertiary),
          ),
        ),
        const Spacer(),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
          decoration: BoxDecoration(
            color: const Color(0xFFEAF6FE),
            borderRadius: BorderRadius.circular(7),
          ),
          child: Text(
            '$count 部',
            style: const TextStyle(
              fontSize: 11,
              color: MikoColors.primaryDeep,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ],
    );
  }
}

class _CardGrid extends StatelessWidget {
  const _CardGrid({required this.entries, required this.columns});

  final List<AnimeEntry> entries;
  final int columns;

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      itemCount: entries.length,
      gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: columns,
        mainAxisSpacing: 14,
        crossAxisSpacing: 14,
        childAspectRatio: 1.28,
      ),
      itemBuilder: (context, index) => _AnimeCard(entry: entries[index]),
    );
  }
}

class _AnimeCard extends StatefulWidget {
  const _AnimeCard({required this.entry});

  final AnimeEntry entry;

  @override
  State<_AnimeCard> createState() => _AnimeCardState();
}

class _AnimeCardState extends State<_AnimeCard> {
  bool _hover = false;
  bool _coverFailed = false;

  @override
  Widget build(BuildContext context) {
    final entry = widget.entry;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: () {
          HostBridge.openBilibili(entry.url, entry.title);
          HostBridge.toast('正在软件内打开「${entry.title}」…');
        },
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 170),
          transform: Matrix4.translationValues(0, _hover ? -3 : 0, 0),
          decoration: BoxDecoration(
            color: MikoColors.surface,
            borderRadius: BorderRadius.circular(13),
            border: Border.all(
              color: _hover ? const Color(0xFFB7DFF5) : MikoColors.border,
            ),
            boxShadow: [
              BoxShadow(
                color: const Color(0xFF1E526D).withValues(
                  alpha: _hover ? 0.14 : 0.055,
                ),
                blurRadius: _hover ? 18 : 9,
                offset: const Offset(0, 5),
              ),
            ],
          ),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      if (!_coverFailed)
                        Image.asset(
                          entry.cover,
                          fit: BoxFit.cover,
                          filterQuality: FilterQuality.medium,
                          errorBuilder: (_, __, ___) {
                            WidgetsBinding.instance.addPostFrameCallback((_) {
                              if (mounted) setState(() => _coverFailed = true);
                            });
                            return _coverPlaceholder();
                          },
                        )
                      else
                        _coverPlaceholder(),
                      Positioned(
                        left: 0,
                        right: 0,
                        bottom: 0,
                        child: Container(
                          height: 46,
                          decoration: const BoxDecoration(
                            gradient: LinearGradient(
                              begin: Alignment.topCenter,
                              end: Alignment.bottomCenter,
                              colors: [Colors.transparent, Color(0x8A0A1C27)],
                            ),
                          ),
                        ),
                      ),
                      Positioned(
                        left: 10,
                        top: 10,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 3,
                          ),
                          decoration: BoxDecoration(
                            color: const Color(0xE64EA4DD),
                            borderRadius: BorderRadius.circular(5),
                          ),
                          child: Text(
                            entry.badge,
                            style: const TextStyle(
                              fontSize: 10,
                              color: Colors.white,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ),
                      Center(
                        child: AnimatedOpacity(
                          duration: const Duration(milliseconds: 150),
                          opacity: _hover ? 1 : 0.82,
                          child: Container(
                            width: 44,
                            height: 44,
                            decoration: BoxDecoration(
                              color: Colors.white.withValues(alpha: 0.92),
                              shape: BoxShape.circle,
                              boxShadow: const [
                                BoxShadow(
                                  color: Color(0x33000000),
                                  blurRadius: 10,
                                  offset: Offset(0, 3),
                                ),
                              ],
                            ),
                            child: const Icon(
                              Icons.play_arrow_rounded,
                              color: MikoColors.primaryDeep,
                              size: 28,
                            ),
                          ),
                        ),
                      ),
                      Positioned(
                        right: 10,
                        bottom: 9,
                        child: Row(
                          children: [
                            const Icon(
                              Icons.chat_bubble_outline_rounded,
                              size: 11,
                              color: Colors.white,
                            ),
                            const SizedBox(width: 4),
                            Text(
                              '弹幕 · 评论',
                              style: TextStyle(
                                fontSize: 10,
                                color: Colors.white.withValues(alpha: 0.94),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(12, 9, 12, 10),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        entry.title,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 13,
                          height: 1.25,
                          fontWeight: FontWeight.w700,
                          color: MikoColors.ink,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        entry.subtitle,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 10.5,
                          color: MikoColors.tertiary,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _coverPlaceholder() {
    return Container(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFFE9F5FC), Color(0xFFCFE9F8)],
        ),
      ),
      alignment: Alignment.center,
      child: const Icon(
        Icons.movie_filter_rounded,
        size: 34,
        color: Color(0xFF7DB9D8),
      ),
    );
  }
}
