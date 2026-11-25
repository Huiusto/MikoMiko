import 'package:flutter/material.dart';

import '../models/anime_rule.dart';
import '../services/host_bridge.dart';
import '../services/miko_api.dart';
import '../services/rule_store.dart';
import '../ui/theme.dart';
import '../ui/widgets.dart';

class AnimeDetailPage extends StatefulWidget {
  const AnimeDetailPage({super.key, required this.card});
  final AnimeCard card;

  @override
  State<AnimeDetailPage> createState() => _AnimeDetailPageState();
}

class _AnimeDetailPageState extends State<AnimeDetailPage> {
  AnimeDetail? _detail;
  bool _loading = true;
  String _error = '';
  int _roadIndex = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = '';
    });
    try {
      final detail = await MikoApi.instance.detail(widget.card);
      if (!mounted) return;
      setState(() {
        _detail = detail;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '解析详情失败：$error';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: MikoColors.background,
      body: Column(
        children: [
          _DetailBar(card: widget.card, onBack: () => Navigator.of(context).pop()),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator(color: MikoColors.primary))
                : _error.isNotEmpty
                    ? Center(
                        child: EmptyPanel(
                          title: '这部作品暂时解析不了',
                          message: _error,
                          icon: Icons.link_off_rounded,
                          action: FilledButton(onPressed: _load, child: const Text('重新解析')),
                        ),
                      )
                    : _buildDetail(_detail!),
          ),
        ],
      ),
    );
  }

  Widget _buildDetail(AnimeDetail detail) {
    final wide = MediaQuery.sizeOf(context).width > 980;
    final roads = detail.roads;
    if (_roadIndex >= roads.length) _roadIndex = 0;
    final episodes = roads.isEmpty ? <AnimeEpisode>[] : roads[_roadIndex].episodes;
    final watching = RuleStore.instance.isWatching(detail.url);

    final info = Container(
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        color: MikoColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: MikoColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(14),
                child: SizedBox(
                  width: wide ? 156 : 116,
                  height: wide ? 218 : 162,
                  child: MikoPoster(
                    url: detail.poster,
                    title: detail.title,
                    referer: detail.rule?.baseURL ?? '',
                  ),
                ),
              ),
              const SizedBox(width: 18),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      detail.title,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: wide ? 24 : 20,
                        height: 1.35,
                        fontWeight: FontWeight.w700,
                        color: MikoColors.ink,
                      ),
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 7,
                      runSpacing: 7,
                      children: [
                        TagChip(text: detail.source),
                        if (detail.note.isNotEmpty) TagChip(text: detail.note, color: const Color(0xFFF1F6FA), foreground: MikoColors.secondary),
                        const TagChip(text: '规则解析', color: Color(0xFFE9F7F1), foreground: MikoColors.success),
                        const TagChip(text: 'Anime4K', color: Color(0xFFEAF4FF), foreground: Color(0xFF3D7BD0)),
                      ],
                    ),
                    const SizedBox(height: 12),
                    Text(
                      detail.description.isEmpty
                          ? '该站点没有提供简介。选集线路来自自定义规则实时解析，播放时交给 MikoMiko 内置播放器。'
                          : detail.description,
                      maxLines: wide ? 6 : 4,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 12, height: 1.8, color: MikoColors.secondary),
                    ),
                    const SizedBox(height: 18),
                    Row(
                      children: [
                        FilledButton.icon(
                          onPressed: episodes.isEmpty
                              ? null
                              : () => _play(detail, episodes.first),
                          icon: const Icon(Icons.play_arrow_rounded, size: 19),
                          label: const Text('开始播放'),
                        ),
                        const SizedBox(width: 10),
                        OutlinedButton.icon(
                          onPressed: () => _toggleWatch(detail),
                          icon: Icon(
                            watching ? Icons.favorite_rounded : Icons.favorite_border_rounded,
                            size: 17,
                            color: watching ? const Color(0xFFE56A8B) : null,
                          ),
                          label: Text(watching ? '已追剧' : '追剧'),
                        ),
                        const Spacer(),
                        if (episodes.isNotEmpty)
                          Text(
                            '共 ${episodes.length} 集',
                            style: const TextStyle(fontSize: 11.5, color: MikoColors.tertiary),
                          ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (roads.length > 1) ...[
            const SizedBox(height: 20),
            const Divider(height: 1),
            const SizedBox(height: 16),
            Row(
              children: [
                const Text('播放线路', style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700)),
                const SizedBox(width: 12),
                Expanded(
                  child: Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (var i = 0; i < roads.length; i++)
                        _RoadChip(
                          text: roads[i].name,
                          count: roads[i].episodes.length,
                          selected: i == _roadIndex,
                          onTap: () => setState(() => _roadIndex = i),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: 22),
          const Divider(height: 1),
          const SizedBox(height: 18),
          Row(
            children: [
              const Text('选集', style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700)),
              const SizedBox(width: 8),
              Text(
                episodes.isEmpty ? '规则没有解析到章节' : '第 ${_currentEpisodeIndex(episodes) + 1} 集',
                style: const TextStyle(fontSize: 10.5, color: MikoColors.tertiary),
              ),
              const Spacer(),
              if (roads.isNotEmpty)
                Text(
                  roads[_roadIndex].name,
                  style: const TextStyle(fontSize: 10.5, color: MikoColors.secondary),
                ),
            ],
          ),
          const SizedBox(height: 12),
          if (episodes.isEmpty)
            const Text('该页面的选集结构与当前规则不匹配，可在规则编辑器里调整“章节线路 / 章节链接”。',
                style: TextStyle(fontSize: 11.5, color: MikoColors.tertiary, height: 1.7))
          else
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (var i = 0; i < episodes.length; i++)
                  _EpisodeChip(
                    text: episodes[i].name,
                    onTap: () => _play(detail, episodes[i]),
                  ),
              ],
            ),
        ],
      ),
    );

    final ruleCard = Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: const Color(0xFF102B3D),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(color: MikoColors.primary.withValues(alpha: 0.22), borderRadius: BorderRadius.circular(10)),
                child: const Icon(Icons.code_rounded, color: Color(0xFF9DD5F4), size: 18),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  detail.rule?.title ?? detail.source,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(color: Colors.white, fontSize: 14, fontWeight: FontWeight.w700),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(color: Colors.white.withValues(alpha: 0.10), borderRadius: BorderRadius.circular(7)),
                child: Text('v${detail.rule?.version ?? '-'}', style: const TextStyle(color: Color(0xFFAEDCF5), fontSize: 9.5)),
              ),
            ],
          ),
          const SizedBox(height: 14),
          _CodeLine(label: '搜索列表', value: detail.rule?.searchList ?? ''),
          _CodeLine(label: '章节线路', value: detail.rule?.chapterRoads ?? ''),
          _CodeLine(label: '章节链接', value: detail.rule?.chapterResult ?? ''),
          const SizedBox(height: 8),
          const Text(
            '规则只读取站点公开页面，不注入账号信息。',
            style: TextStyle(color: Color(0xFF7FA6BE), fontSize: 10.5, height: 1.6),
          ),
        ],
      ),
    );

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(26, 22, 26, 34),
      child: wide
          ? Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(child: info),
                const SizedBox(width: 18),
                SizedBox(width: 310, child: Column(children: [ruleCard])),
              ],
            )
          : Column(children: [info, const SizedBox(height: 16), ruleCard]),
    );
  }

  int _currentEpisodeIndex(List<AnimeEpisode> episodes) {
    return 0;
  }

  void _play(AnimeDetail detail, AnimeEpisode episode) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        duration: const Duration(seconds: 2),
        content: Text('正在打开 ${episode.name}…'),
      ),
    );
    try {
      HostBridge.play(detail, episode);
    } catch (error) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(duration: const Duration(seconds: 10), content: Text('播放桥接失败：$error')),
      );
    }
  }

  void _toggleWatch(AnimeDetail detail) {
    RuleStore.instance.toggleWatch(AnimeCard(
      title: detail.title,
      url: detail.url,
      source: detail.source,
      poster: detail.poster,
      note: detail.note,
    ));
    setState(() {});
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(RuleStore.instance.isWatching(detail.url) ? '已加入我的追剧' : '已从我的追剧移除')),
    );
  }
}

class _DetailBar extends StatelessWidget {
  const _DetailBar({required this.card, required this.onBack});
  final AnimeCard card;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 60,
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(bottom: BorderSide(color: MikoColors.border)),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 18),
      child: Row(
        children: [
          IconButton(
            onPressed: onBack,
            icon: const Icon(Icons.arrow_back_rounded, size: 20, color: MikoColors.secondary),
            tooltip: '返回',
          ),
          const SizedBox(width: 4),
          const Text('作品详情', style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700, color: MikoColors.ink)),
          const SizedBox(width: 12),
          Container(width: 1, height: 16, color: MikoColors.border),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              card.title,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 11.5, color: MikoColors.secondary),
            ),
          ),
          const TagChip(text: 'FLUTTER MODULE', color: Color(0xFFEFF8FE), foreground: MikoColors.primaryDeep),
        ],
      ),
    );
  }
}

class _RoadChip extends StatelessWidget {
  const _RoadChip({required this.text, required this.count, required this.selected, required this.onTap});
  final String text;
  final int count;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 7),
        decoration: BoxDecoration(
          color: selected ? MikoColors.primarySoft : const Color(0xFFF7FAFC),
          borderRadius: BorderRadius.circular(9),
          border: Border.all(color: selected ? MikoColors.primary.withValues(alpha: 0.35) : MikoColors.border),
        ),
        child: Text(
          '$text · $count',
          style: TextStyle(
            fontSize: 10.5,
            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
            color: selected ? MikoColors.primaryDeep : MikoColors.secondary,
          ),
        ),
      ),
    );
  }
}

class _EpisodeChip extends StatelessWidget {
  const _EpisodeChip({required this.text, required this.onTap});
  final String text;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        constraints: const BoxConstraints(minWidth: 76),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
        decoration: BoxDecoration(
          color: const Color(0xFFF7FAFC),
          borderRadius: BorderRadius.circular(9),
          border: Border.all(color: MikoColors.border),
        ),
        child: Text(
          text,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 11, color: MikoColors.ink),
        ),
      ),
    );
  }
}

class _CodeLine extends StatelessWidget {
  const _CodeLine({required this.label, required this.value});
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: RichText(
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        text: TextSpan(
          children: [
            TextSpan(text: '$label  ', style: const TextStyle(color: Color(0xFF7FA6BE), fontSize: 10)),
            TextSpan(
              text: value.isEmpty ? '未设置' : value,
              style: const TextStyle(color: Color(0xFFB8E2F8), fontSize: 10, fontFamily: 'monospace'),
            ),
          ],
        ),
      ),
    );
  }
}
