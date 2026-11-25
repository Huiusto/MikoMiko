import 'dart:convert';

import 'package:flutter/material.dart';

import 'models/anime_rule.dart';
import 'pages/home_page.dart';
import 'pages/rules_page.dart';
import 'pages/search_page.dart';
import 'pages/settings_page.dart';
import 'pages/watchlist_page.dart';
import 'services/host_bridge.dart';
import 'services/miko_api.dart';
import 'services/rule_store.dart';
import 'ui/theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await RuleStore.instance.load();
  runApp(const MikoAnimeApp());
}

class MikoAnimeApp extends StatelessWidget {
  const MikoAnimeApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'MikoMiko · 追番',
      debugShowCheckedModeBanner: false,
      theme: buildMikoTheme(),
      home: const AnimeShell(),
    );
  }
}

enum _Mode { home, rules, watch, settings }

class AnimeShell extends StatefulWidget {
  const AnimeShell({super.key});

  @override
  State<AnimeShell> createState() => _AnimeShellState();
}

class _AnimeShellState extends State<AnimeShell> {
  final TextEditingController _search = TextEditingController();
  _Mode _mode = _Mode.home;
  String _keyword = '';
  List<AnimeCard> _results = <AnimeCard>[];
  List<String> _errors = <String>[];
  bool _searching = false;
  bool _hasSearch = false;

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _runSearch(String keyword) async {
    final value = keyword.trim();
    if (value.isEmpty) return;
    _search.text = value;
    setState(() {
      _keyword = value;
      _hasSearch = true;
      _searching = true;
      _results = <AnimeCard>[];
      _errors = <String>[];
    });

    final rules = RuleStore.instance.enabledRules;
    final results = <AnimeCard>[];
    final errors = <String>[];
    await Future.wait(
      rules.map((rule) async {
        try {
          final cards = await MikoApi.instance
              .search(rule, value, limit: 16)
              .timeout(const Duration(seconds: 22));
          results.addAll(cards);
        } catch (error) {
          errors.add('${rule.title}：${_friendly(error)}');
        }
      }),
    );

    if (!mounted) return;
    final deduped = <String, AnimeCard>{};
    for (final card in results) {
      deduped.putIfAbsent('${card.source}|${card.url}', () => card);
    }
    setState(() {
      _results = deduped.values.toList()
        ..sort((a, b) {
          final bySource = a.source.compareTo(b.source);
          return bySource != 0 ? bySource : a.title.compareTo(b.title);
        });
      _errors = errors;
      _searching = false;
    });
  }

  String _friendly(Object error) {
    final text = error.toString();
    if (text.contains('TimeoutException')) return '请求超时';
    if (text.contains('403')) return '站点拒绝访问（403）';
    if (text.contains('404')) return '页面不存在（404）';
    if (text.contains('Failed to fetch') || text.contains('XMLHttpRequest'))
      return '代理请求失败';
    return text.length > 48 ? '${text.substring(0, 48)}…' : text;
  }

  void _clearSearch() {
    _search.clear();
    setState(() {
      _hasSearch = false;
      _keyword = '';
      _results = <AnimeCard>[];
      _errors = <String>[];
    });
  }

  void _switch(_Mode mode) {
    setState(() {
      _mode = mode;
      _hasSearch = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: MikoColors.background,
      body: Column(
        children: [
          _TopBar(
            mode: _mode,
            search: _search,
            searching: _searching,
            hasSearch: _hasSearch,
            onMode: _switch,
            onSearch: _runSearch,
            onClear: _clearSearch,
          ),
          Expanded(
            child: AnimatedBuilder(
              animation: RuleStore.instance,
              builder: (context, _) {
                if (_hasSearch) {
                  return SearchPage(
                    keyword: _keyword,
                    cards: _results,
                    searching: _searching,
                    errors: _errors,
                    onRetry: () => _runSearch(_keyword),
                  );
                }
                switch (_mode) {
                  case _Mode.home:
                    return HomePage(
                      onSearch: _runSearch,
                      onOpenRules: () => _switch(_Mode.rules),
                    );
                  case _Mode.rules:
                    return const RulesPage();
                  case _Mode.watch:
                    return const WatchlistPage();
                  case _Mode.settings:
                    return ModuleSettingsPage(onExportRules: _exportRules);
                }
              },
            ),
          ),
        ],
      ),
    );
  }

  void _exportRules() {
    final payload = const JsonEncoder.withIndent('  ').convert(
      RuleStore.instance.rules
          .map((rule) => rule.toJson(includeRuntime: true))
          .toList(),
    );
    HostBridge.download('mikomiko-anime-rules.json', payload);
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('已导出全部规则')));
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.mode,
    required this.search,
    required this.searching,
    required this.hasSearch,
    required this.onMode,
    required this.onSearch,
    required this.onClear,
  });

  final _Mode mode;
  final TextEditingController search;
  final bool searching;
  final bool hasSearch;
  final ValueChanged<_Mode> onMode;
  final ValueChanged<String> onSearch;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 58,
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(bottom: BorderSide(color: Color(0xFFEDEFF3))),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 22),
      child: Stack(
        alignment: Alignment.center,
        children: [
          Align(
            alignment: Alignment.centerLeft,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                _NavPill(
                  label: '首页',
                  selected: mode == _Mode.home && !hasSearch,
                  onTap: () => onMode(_Mode.home),
                ),
                const SizedBox(width: 6),
                _NavPill(
                  label: '规则库',
                  selected: mode == _Mode.rules && !hasSearch,
                  onTap: () => onMode(_Mode.rules),
                ),
                const SizedBox(width: 6),
                _NavPill(
                  label: '我的追剧',
                  selected: mode == _Mode.watch && !hasSearch,
                  onTap: () => onMode(_Mode.watch),
                ),
              ],
            ),
          ),
          SizedBox(
            width: 460,
            height: 36,
            child: TextField(
              controller: search,
              textInputAction: TextInputAction.search,
              onSubmitted: onSearch,
              style: const TextStyle(fontSize: 12),
              decoration: InputDecoration(
                hintText: '搜索番剧、电影、电视剧',
                hintStyle: const TextStyle(
                  fontSize: 11.5,
                  color: Color(0xFFA6AEB8),
                ),
                filled: true,
                fillColor: const Color(0xFFF7F8FA),
                prefixIcon: const Icon(
                  Icons.search_rounded,
                  size: 18,
                  color: Color(0xFF7C8794),
                ),
                suffixIcon: hasSearch
                    ? IconButton(
                        onPressed: onClear,
                        icon: const Icon(Icons.close_rounded, size: 15),
                        tooltip: '清空',
                      )
                    : IconButton(
                        onPressed: searching
                            ? null
                            : () => onSearch(search.text),
                        icon: const Icon(
                          Icons.search_rounded,
                          size: 18,
                          color: Color(0xFF7C8794),
                        ),
                        tooltip: '搜索',
                      ),
                contentPadding: const EdgeInsets.symmetric(vertical: 8),
                border: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(7),
                  borderSide: const BorderSide(color: Color(0xFFE1E4E8)),
                ),
                enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(7),
                  borderSide: const BorderSide(color: Color(0xFFE1E4E8)),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(7),
                  borderSide: const BorderSide(
                    color: MikoColors.primary,
                    width: 1.1,
                  ),
                ),
              ),
            ),
          ),
          Align(
            alignment: Alignment.centerRight,
            child: _IconButton(
              icon: Icons.settings_outlined,
              tooltip: '模块设置',
              active: mode == _Mode.settings,
              onTap: () => onMode(_Mode.settings),
            ),
          ),
        ],
      ),
    );
  }
}

class _NavPill extends StatelessWidget {
  const _NavPill({
    required this.label,
    required this.selected,
    required this.onTap,
  });
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 150),
        padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 7),
        decoration: BoxDecoration(
          color: selected ? MikoColors.primarySoft : Colors.transparent,
          borderRadius: BorderRadius.circular(9),
        ),
        child: Text(
          label,
          style: TextStyle(
            fontSize: 11.5,
            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
            color: selected ? MikoColors.primaryDeep : MikoColors.secondary,
          ),
        ),
      ),
    );
  }
}

class _IconButton extends StatelessWidget {
  const _IconButton({
    required this.icon,
    required this.tooltip,
    required this.onTap,
    this.active = false,
  });
  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;
  final bool active;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          width: 34,
          height: 34,
          decoration: BoxDecoration(
            color: active ? MikoColors.primarySoft : const Color(0xFFF7FAFC),
            borderRadius: BorderRadius.circular(9),
            border: Border.all(color: MikoColors.border),
          ),
          child: Icon(
            icon,
            size: 17,
            color: active ? MikoColors.primaryDeep : MikoColors.secondary,
          ),
        ),
      ),
    );
  }
}
