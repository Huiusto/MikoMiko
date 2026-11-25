import 'package:flutter/material.dart';

import '../models/anime_rule.dart';
import '../services/miko_api.dart';
import '../services/rule_store.dart';
import '../ui/theme.dart';
import '../ui/widgets.dart';
import 'detail_page.dart';

class HomePage extends StatefulWidget {
  const HomePage({
    super.key,
    required this.onSearch,
    required this.onOpenRules,
  });

  final ValueChanged<String> onSearch;
  final VoidCallback onOpenRules;

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  List<AnimeCard> _cards = <AnimeCard>[];
  bool _loading = true;
  String _error = '';
  String _source = '';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  Future<void> _load() async {
    final rules = RuleStore.instance.rules
        .where((item) => item.enabled && item.homeList.isNotEmpty)
        .toList();
    if (rules.isEmpty) {
      setState(() {
        _loading = false;
        _cards = <AnimeCard>[];
        _error = '没有启用的首页规则，请先到规则库启用一个站点。';
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = '';
      _source = '';
    });
    try {
      // 多个站点并行采集，任一站点改版或拦截都不会让首页整片空白。
      final lists = await Future.wait(
        rules.map((rule) async {
          try {
            return (await MikoApi.instance
                    .home(rule, limit: 24)
                    .timeout(const Duration(seconds: 12)))
                .toList();
          } catch (_) {
            return <AnimeCard>[];
          }
        }),
      );
      final merged = <AnimeCard>[];
      final seen = <String>{};
      for (final list in lists) {
        for (final card in list) {
          final key = '${card.title}|${card.source}';
          if (!seen.add(key)) continue;
          merged.add(card);
          if (merged.length >= 42) break;
        }
        if (merged.length >= 42) break;
      }
      if (!mounted) return;
      final used = <String>[];
      for (final rule in rules) {
        if (merged.any((card) => card.source == rule.title))
          used.add(rule.title);
      }
      setState(() {
        _cards = merged;
        _loading = false;
        _source = used.join(' · ');
        if (merged.isEmpty) _error = '所有启用站点都没有取到内容，站点可能改版或网络受限。';
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$error';
      });
    }
  }

  void _openCard(AnimeCard card) {
    Navigator.of(
      context,
    ).push(MaterialPageRoute(builder: (_) => AnimeDetailPage(card: card)));
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: RuleStore.instance,
      builder: (context, _) {
        return ColoredBox(
          color: Colors.white,
          child: SingleChildScrollView(
            padding: EdgeInsets.zero,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const _Banner(),
                Padding(
                  padding: const EdgeInsets.fromLTRB(28, 24, 28, 38),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _SectionTitle(
                        count: _cards.length,
                        onOpenAll: _cards.isEmpty
                            ? null
                            : () => Navigator.of(context).push(
                                MaterialPageRoute(
                                  builder: (_) => AllAnimePage(
                                    cards: _cards,
                                    source: _source,
                                  ),
                                ),
                              ),
                      ),
                      const SizedBox(height: 18),
                      if (_loading)
                        const _LoadingGrid()
                      else if (_cards.isEmpty)
                        _EmptyHome(error: _error, onRetry: _load)
                      else
                        _PosterGrid(cards: _cards, onOpen: _openCard),
                    ],
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner();

  @override
  Widget build(BuildContext context) {
    return AspectRatio(
      aspectRatio: 3508 / 660,
      child: Image.asset(
        'assets/images/home_banner.png',
        fit: BoxFit.cover,
        filterQuality: FilterQuality.high,
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({required this.count, this.onOpenAll});
  final int count;
  final VoidCallback? onOpenAll;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        const Text(
          '猜你喜欢',
          style: TextStyle(
            color: Color(0xFF16181D),
            fontSize: 18,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.4,
          ),
        ),
        if (count > 0) ...[
          const SizedBox(width: 9),
          Text(
            '$count 部',
            style: const TextStyle(color: Color(0xFF9AA3AF), fontSize: 11),
          ),
        ],
        const Spacer(),
        SizedBox(
          height: 30,
          child: OutlinedButton(
            onPressed: onOpenAll,
            style: OutlinedButton.styleFrom(
              foregroundColor: const Color(0xFF6B7280),
              side: const BorderSide(color: Color(0xFFE1E4E8)),
              padding: const EdgeInsets.symmetric(horizontal: 13),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(7),
              ),
              textStyle: const TextStyle(fontSize: 12),
            ),
            child: const Text('查看全部 〉'),
          ),
        ),
      ],
    );
  }
}

class _PosterGrid extends StatelessWidget {
  const _PosterGrid({required this.cards, required this.onOpen});
  final List<AnimeCard> cards;
  final ValueChanged<AnimeCard> onOpen;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth;
        final columns = width >= 1060
            ? 7
            : width >= 1000
            ? 6
            : width >= 820
            ? 5
            : width >= 620
            ? 4
            : 3;
        return GridView.builder(
          shrinkWrap: true,
          padding: EdgeInsets.zero,
          physics: const NeverScrollableScrollPhysics(),
          itemCount: cards.length,
          gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: columns,
            mainAxisSpacing: 22,
            crossAxisSpacing: 14,
            childAspectRatio: 0.55,
          ),
          itemBuilder: (context, index) {
            final card = cards[index];
            return AnimePosterCard(
              card: card,
              onOpen: () => onOpen(card),
              showSource: false,
              showHeart: false,
              radius: 10,
              badgeText: _score(card.note),
            );
          },
        );
      },
    );
  }

  static String _score(String note) {
    final match = RegExp(r'(9\.\d|10\.0)').firstMatch(note);
    return match?.group(1) ?? '';
  }
}

class _LoadingGrid extends StatelessWidget {
  const _LoadingGrid();

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = constraints.maxWidth >= 1180 ? 7 : 5;
        return GridView.builder(
          shrinkWrap: true,
          padding: EdgeInsets.zero,
          physics: const NeverScrollableScrollPhysics(),
          itemCount: columns * 2,
          gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: columns,
            mainAxisSpacing: 22,
            crossAxisSpacing: 14,
            childAspectRatio: 0.55,
          ),
          itemBuilder: (_, index) => Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: const Color(0xFFF1F3F6),
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
              ),
              const SizedBox(height: 10),
              Container(
                width: 92,
                height: 12,
                decoration: BoxDecoration(
                  color: const Color(0xFFF1F3F6),
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
              const SizedBox(height: 6),
              Container(
                width: 62,
                height: 9,
                decoration: BoxDecoration(
                  color: const Color(0xFFF5F6F8),
                  borderRadius: BorderRadius.circular(4),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _EmptyHome extends StatelessWidget {
  const _EmptyHome({required this.error, required this.onRetry});
  final String error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 46),
      alignment: Alignment.center,
      child: Column(
        children: [
          const Icon(
            Icons.cloud_off_rounded,
            size: 38,
            color: Color(0xFFB6C1CC),
          ),
          const SizedBox(height: 12),
          const Text(
            '这个站点暂时没有取到内容',
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: MikoColors.ink,
            ),
          ),
          const SizedBox(height: 7),
          Text(
            error.isEmpty ? '站点可能改版或网络受限，可以稍后刷新。' : _friendly(error),
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 11,
              color: MikoColors.tertiary,
              height: 1.6,
            ),
          ),
          const SizedBox(height: 16),
          FilledButton(onPressed: onRetry, child: const Text('重新加载')),
        ],
      ),
    );
  }

  static String _friendly(String value) {
    if (value.contains('403')) return '站点拒绝访问（403），可换一个来源。';
    if (value.contains('TimeoutException')) return '请求超时，请检查网络后重试。';
    final text = value.replaceAll(RegExp(r'\s+'), ' ').trim();
    return text.length > 80 ? '${text.substring(0, 80)}…' : text;
  }
}

class AllAnimePage extends StatelessWidget {
  const AllAnimePage({super.key, required this.cards, required this.source});

  final List<AnimeCard> cards;
  final String source;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        surfaceTintColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios_new_rounded, size: 18),
          onPressed: () => Navigator.of(context).pop(),
          tooltip: '返回',
        ),
        title: Text(
          '猜你喜欢${source.isEmpty ? '' : ' · $source'}',
          style: const TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w700,
            color: MikoColors.ink,
          ),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(28, 8, 28, 34),
        child: _PosterGrid(
          cards: cards,
          onOpen: (card) => Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => AnimeDetailPage(card: card)),
          ),
        ),
      ),
    );
  }
}
