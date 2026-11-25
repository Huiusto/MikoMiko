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
  String _source = '7sefun';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _load());
  }

  Future<void> _load() async {
    final rule = RuleStore.instance.rules
        .where((item) => item.enabled && item.homeList.isNotEmpty)
        .cast<AnimeRule?>()
        .firstWhere((item) => item != null, orElse: () => null);
    if (rule == null) {
      setState(() {
        _loading = false;
        _cards = <AnimeCard>[];
        _error = '没有启用的首页规则';
      });
      return;
    }
    setState(() {
      _loading = true;
      _error = '';
      _source = rule.title;
    });
    try {
      final cards = await MikoApi.instance.home(rule, limit: 24);
      if (!mounted) return;
      setState(() {
        _cards = cards;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = '$error';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: RuleStore.instance,
      builder: (context, _) {
        final enabled = RuleStore.instance.enabledRules;
        return SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(26, 24, 26, 36),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _Hero(
                ruleCount: RuleStore.instance.rules.length,
                enabledCount: enabled.length,
                onSearch: widget.onSearch,
                onOpenRules: widget.onOpenRules,
              ),
              const SizedBox(height: 22),
              SectionHeading(
                title: '继续追剧',
                subtitle: '${RuleStore.instance.watchlist.length} 部收藏',
                action: RuleStore.instance.watchlist.isEmpty
                    ? null
                    : TextButton(onPressed: () {}, child: const Text('查看我的追剧')),
              ),
              const SizedBox(height: 12),
              if (RuleStore.instance.watchlist.isEmpty)
                _QuickKeywords(onSearch: widget.onSearch)
              else
                SizedBox(
                  height: 210,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: RuleStore.instance.watchlist.length,
                    separatorBuilder: (_, __) => const SizedBox(width: 14),
                    itemBuilder: (context, index) {
                      final card = RuleStore.instance.watchlist[index];
                      return SizedBox(
                        width: 132,
                        child: AnimePosterCard(
                          card: card,
                          onOpen: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => AnimeDetailPage(card: card))),
                          watching: true,
                          onWatch: () => RuleStore.instance.toggleWatch(card),
                        ),
                      );
                    },
                  ),
                ),
              const SizedBox(height: 26),
              SectionHeading(
                title: '站点最新',
                subtitle: _loading ? '正在从 $_source 采集' : '$_source · ${_cards.length} 部',
                action: SizedBox(
                  width: 92,
                  child: TextButton.icon(
                    onPressed: _loading ? null : _load,
                    icon: const Icon(Icons.refresh_rounded, size: 15),
                    label: const Text('刷新'),
                  ),
                ),
              ),
              const SizedBox(height: 14),
              if (_loading)
                const SizedBox(
                  height: 260,
                  child: Center(child: CircularProgressIndicator(color: MikoColors.primary)),
                )
              else if (_cards.isEmpty)
                EmptyPanel(
                  title: '这个站点暂时没有采集到内容',
                  message: _error.isEmpty ? '内置首页规则没有匹配到卡片，可能是站点改版或网络受限。' : '请求失败：$_error',
                  icon: Icons.cloud_off_rounded,
                  action: FilledButton(onPressed: _load, child: const Text('再试一次')),
                )
              else
                _PosterGrid(
                  cards: _cards,
                  onOpen: (card) => Navigator.of(context).push(MaterialPageRoute(builder: (_) => AnimeDetailPage(card: card))),
                  onWatch: (card) => RuleStore.instance.toggleWatch(card),
                ),
              const SizedBox(height: 26),
              const SectionHeading(title: '规则引擎', subtitle: 'Kazumi 兼容 · 最多五行 XPath'),
              const SizedBox(height: 12),
              _EnginePanel(rules: enabled, onOpenRules: widget.onOpenRules),
            ],
          ),
        );
      },
    );
  }
}

class _Hero extends StatelessWidget {
  const _Hero({required this.ruleCount, required this.enabledCount, required this.onSearch, required this.onOpenRules});
  final int ruleCount;
  final int enabledCount;
  final ValueChanged<String> onSearch;
  final VoidCallback onOpenRules;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(minHeight: 210),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF0E3348), Color(0xFF154E6C), Color(0xFF2D83B8)],
        ),
        borderRadius: BorderRadius.circular(22),
      ),
      clipBehavior: Clip.antiAlias,
      child: Stack(
        children: [
          Positioned(
            right: -50,
            top: -70,
            child: Container(
              width: 260,
              height: 260,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: Colors.white.withValues(alpha: 0.05),
              ),
            ),
          ),
          Positioned(
            right: 70,
            bottom: -90,
            child: Container(
              width: 220,
              height: 220,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: MikoColors.primary.withValues(alpha: 0.16),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(30, 27, 30, 25),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
                            decoration: BoxDecoration(
                              color: Colors.white.withValues(alpha: 0.13),
                              borderRadius: BorderRadius.circular(7),
                              border: Border.all(color: Colors.white.withValues(alpha: 0.15)),
                            ),
                            child: const Text('FLUTTER 规则追番模块', style: TextStyle(color: Color(0xFFCBE9FA), fontSize: 9, fontWeight: FontWeight.w700, letterSpacing: 0.4)),
                          ),
                          const SizedBox(width: 9),
                          Text('v0.1.23', style: TextStyle(color: Colors.white.withValues(alpha: 0.55), fontSize: 9.5)),
                        ],
                      ),
                      const SizedBox(height: 14),
                      const Text(
                        '自定规则，追你想追。',
                        style: TextStyle(color: Colors.white, fontSize: 31, height: 1.2, fontWeight: FontWeight.w800, letterSpacing: 0.2),
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        '最多五行 XPath 构建站点规则，搜索、选集、播放一站完成。',
                        style: TextStyle(color: Color(0xFFBBD9EA), fontSize: 12, height: 1.7),
                      ),
                      const SizedBox(height: 22),
                      SizedBox(
                        width: 430,
                        height: 44,
                        child: TextField(
                          textInputAction: TextInputAction.search,
                          onSubmitted: (value) {
                            if (value.trim().isNotEmpty) onSearch(value.trim());
                          },
                          decoration: InputDecoration(
                            hintText: '搜索番剧、电影、电视剧…',
                            hintStyle: const TextStyle(color: Color(0xFF9DBDCE), fontSize: 12.5),
                            filled: true,
                            fillColor: Colors.white.withValues(alpha: 0.12),
                            prefixIcon: const Icon(Icons.search_rounded, color: Color(0xFFD7EDF9), size: 19),
                            suffixIcon: IconButton(
                              onPressed: () => onOpenRules(),
                              icon: const Icon(Icons.tune_rounded, color: Color(0xFFD7EDF9), size: 18),
                              tooltip: '规则库',
                            ),
                            contentPadding: const EdgeInsets.symmetric(vertical: 11),
                            border: OutlineInputBorder(borderRadius: BorderRadius.circular(22), borderSide: BorderSide(color: Colors.white.withValues(alpha: 0.18))),
                            enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(22), borderSide: BorderSide(color: Colors.white.withValues(alpha: 0.18))),
                            focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(22), borderSide: BorderSide(color: Colors.white.withValues(alpha: 0.45))),
                          ),
                          style: const TextStyle(color: Colors.white, fontSize: 12.5),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 26),
                SizedBox(
                  width: 214,
                  child: Row(
                    children: [
                      Expanded(child: _Metric(value: '$ruleCount', label: '规则总数')),
                      Container(width: 1, height: 34, color: Colors.white.withValues(alpha: 0.14)),
                      Expanded(child: _Metric(value: '$enabledCount', label: '已启用')),
                    ],
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

class _Metric extends StatelessWidget {
  const _Metric({required this.value, required this.label});
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(value, style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.w800)),
        const SizedBox(height: 3),
        Text(label, style: const TextStyle(color: Color(0xFF9FC4D8), fontSize: 9.5)),
      ],
    );
  }
}

class _QuickKeywords extends StatelessWidget {
  const _QuickKeywords({required this.onSearch});
  final ValueChanged<String> onSearch;

  @override
  Widget build(BuildContext context) {
    const keywords = ['凡人修仙传', '名侦探柯南', '咒术回战', '间谍过家家', '葬送的芙莉莲'];
    return Container(
      padding: const EdgeInsets.fromLTRB(18, 13, 18, 13),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(15),
        border: Border.all(color: MikoColors.border),
      ),
      child: Row(
        children: [
          const Icon(Icons.auto_awesome_rounded, size: 16, color: MikoColors.primary),
          const SizedBox(width: 9),
          const Text('试试搜索', style: TextStyle(fontSize: 11, color: MikoColors.secondary, fontWeight: FontWeight.w600)),
          const SizedBox(width: 14),
          Expanded(
            child: Wrap(
              spacing: 8,
              runSpacing: 7,
              children: [
                for (final keyword in keywords)
                  ActionChip(
                    onPressed: () => onSearch(keyword),
                    label: Text(keyword, style: const TextStyle(fontSize: 10, color: MikoColors.primaryDeep)),
                    backgroundColor: MikoColors.primarySoft,
                    side: BorderSide.none,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(7)),
                    padding: const EdgeInsets.symmetric(horizontal: 3, vertical: 1),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PosterGrid extends StatelessWidget {
  const _PosterGrid({required this.cards, required this.onOpen, required this.onWatch});
  final List<AnimeCard> cards;
  final ValueChanged<AnimeCard> onOpen;
  final ValueChanged<AnimeCard> onWatch;

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      itemCount: cards.length,
      gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
        maxCrossAxisExtent: 168,
        mainAxisSpacing: 20,
        crossAxisSpacing: 16,
        childAspectRatio: 0.56,
      ),
      itemBuilder: (context, index) {
        final card = cards[index];
        return AnimePosterCard(
          card: card,
          onOpen: () => onOpen(card),
          watching: RuleStore.instance.isWatching(card.url),
          onWatch: () => onWatch(card),
        );
      },
    );
  }
}

class _EnginePanel extends StatelessWidget {
  const _EnginePanel({required this.rules, required this.onOpenRules});
  final List<AnimeRule> rules;
  final VoidCallback onOpenRules;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18), border: Border.all(color: MikoColors.border)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            flex: 5,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('五个选择器，组成一条规则', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
                const SizedBox(height: 12),
                for (var i = 0; i < 5; i++) ...[
                  _FlowRow(index: i + 1, label: ['搜索列表', '结果名称', '结果链接', '章节线路', '章节链接'][i], value: i == 0 ? "//div[contains(@class,'video')]" : '...'),
                  if (i < 4) const SizedBox(height: 7),
                ],
              ],
            ),
          ),
          const SizedBox(width: 26),
          Container(width: 1, height: 150, color: MikoColors.border),
          const SizedBox(width: 26),
          Expanded(
            flex: 4,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('已启用的站点', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
                const SizedBox(height: 12),
                Wrap(
                  spacing: 7,
                  runSpacing: 7,
                  children: [
                    for (final rule in rules.take(9)) TagChip(text: rule.title, color: const Color(0xFFF3F9FD), foreground: MikoColors.secondary),
                  ],
                ),
                const SizedBox(height: 16),
                Text(
                  rules.isEmpty ? '还没有启用规则。进入规则库，导入或启用一条 Kazumi 规则即可开始采集。' : '内置规则可直接编辑与分享；自定义规则保存在本机，随模块一起使用。',
                  style: const TextStyle(fontSize: 10.5, height: 1.7, color: MikoColors.tertiary),
                ),
                const SizedBox(height: 14),
                OutlinedButton(onPressed: onOpenRules, child: const Text('打开规则库')),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _FlowRow extends StatelessWidget {
  const _FlowRow({required this.index, required this.label, required this.value});
  final int index;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 20,
          height: 20,
          alignment: Alignment.center,
          decoration: BoxDecoration(color: MikoColors.primarySoft, borderRadius: BorderRadius.circular(6)),
          child: Text('$index', style: const TextStyle(fontSize: 9, color: MikoColors.primaryDeep, fontWeight: FontWeight.w700)),
        ),
        const SizedBox(width: 8),
        SizedBox(width: 62, child: Text(label, style: const TextStyle(fontSize: 10.5, color: MikoColors.secondary))),
        Expanded(
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
            decoration: BoxDecoration(color: const Color(0xFFF7FAFC), borderRadius: BorderRadius.circular(6)),
            child: Text(value, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 9.5, color: MikoColors.ink, fontFamily: 'monospace')),
          ),
        ),
      ],
    );
  }
}
