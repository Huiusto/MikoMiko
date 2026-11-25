import 'dart:convert';
import 'dart:html' as html;

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import '../models/anime_rule.dart';

class RuleStore extends ChangeNotifier {
  static final RuleStore instance = RuleStore._();
  RuleStore._();

  static const _customKey = 'miko.anime.rules.v2';
  static const _enabledKey = 'miko.anime.enabled.v2';
  static const _watchKey = 'miko.anime.watchlist.v2';
  static const _settingsKey = 'miko.anime.settings.v2';

  final List<AnimeRule> _rules = <AnimeRule>[];
  final List<AnimeCard> _watchlist = <AnimeCard>[];
  bool anime4k = true;
  bool loaded = false;

  List<AnimeRule> get rules => List.unmodifiable(_rules);
  List<AnimeRule> get enabledRules => _rules.where((rule) => rule.enabled).toList();
  List<AnimeCard> get watchlist => List.unmodifiable(_watchlist);

  Future<void> load() async {
    if (loaded) return;
    final builtin = <AnimeRule>[];
    for (final name in const ['7sefun', 'aafun', 'akianime', 'baimao', 'MXdm', 'DM84', 'xfdmneo']) {
      try {
        final text = await rootBundle.loadString('assets/rules/$name.json');
        builtin.add(AnimeRule.fromJson(jsonDecode(text) as Map<String, dynamic>, builtIn: true));
      } catch (_) {
        // 单条内置规则缺失不影响模块启动。
      }
    }

    final enabled = _readJsonMap(_enabledKey);
    for (final rule in builtin) {
      rule.enabled = enabled[rule.name] != false;
      _rules.add(rule);
    }

    final custom = _readJsonList(_customKey);
    for (final item in custom) {
      if (item is Map<String, dynamic>) {
        final rule = AnimeRule.fromJson(item, builtIn: item['builtIn'] == true);
        if (_rules.any((existing) => existing.name == rule.name)) continue;
        _rules.add(rule);
      }
    }

    final watch = _readJsonList(_watchKey);
    for (final item in watch) {
      if (item is Map<String, dynamic>) {
        _watchlist.add(AnimeCard(
          title: item['title']?.toString() ?? '',
          url: item['url']?.toString() ?? '',
          source: item['source']?.toString() ?? '',
          poster: item['poster']?.toString() ?? '',
          note: item['note']?.toString() ?? '',
        ));
      }
    }

    final settings = _readJsonMap(_settingsKey);
    anime4k = settings['anime4k'] != false;
    loaded = true;
    notifyListeners();
  }

  Future<void> toggleRule(AnimeRule rule, bool value) async {
    rule.enabled = value;
    _saveEnabled();
    notifyListeners();
  }

  Future<void> saveRule(AnimeRule rule) async {
    final index = _rules.indexWhere((item) => item.name == rule.name);
    if (index >= 0) {
      _rules[index] = rule;
    } else {
      _rules.add(rule);
    }
    _saveCustom();
    notifyListeners();
  }

  Future<void> deleteRule(AnimeRule rule) async {
    if (rule.builtIn) {
      rule.enabled = false;
      _saveEnabled();
    } else {
      _rules.removeWhere((item) => item.name == rule.name);
      _saveCustom();
    }
    notifyListeners();
  }

  Future<String> importRule(String text) async {
    final rule = AnimeRule.fromText(text);
    if (!rule.hasCoreSelectors) {
      throw const FormatException('规则缺少核心 XPath：搜索列表、名称、链接、章节结果');
    }
    if (rule.searchURL.isEmpty) {
      throw const FormatException('规则缺少 searchURL');
    }
    rule.enabled = true;
    rule.builtIn = false;
    final existing = _rules.indexWhere((item) => item.name == rule.name && !item.builtIn);
    if (existing >= 0) {
      _rules[existing] = rule;
    } else {
      _rules.add(rule);
    }
    _saveCustom();
    notifyListeners();
    return rule.title;
  }

  Future<void> setAnime4k(bool value) async {
    anime4k = value;
    _writeJson(_settingsKey, <String, dynamic>{'anime4k': value});
    notifyListeners();
  }

  bool isWatching(String url) => _watchlist.any((item) => item.url == url);

  Future<void> toggleWatch(AnimeCard card) async {
    final index = _watchlist.indexWhere((item) => item.url == card.url);
    if (index >= 0) {
      _watchlist.removeAt(index);
    } else {
      _watchlist.insert(0, card);
    }
    _writeJson(_watchKey, _watchlist
        .map((item) => <String, dynamic>{
              'title': item.title,
              'url': item.url,
              'source': item.source,
              'poster': item.poster,
              'note': item.note,
            })
        .toList());
    notifyListeners();
  }

  void _saveCustom() {
    _writeJson(_customKey, _rules
        .where((rule) => !rule.builtIn)
        .map((rule) => rule.toJson(includeRuntime: true))
        .toList());
  }

  void _saveEnabled() {
    _writeJson(_enabledKey, <String, dynamic>{
      for (final rule in _rules) rule.name: rule.enabled,
    });
  }

  List<dynamic> _readJsonList(String key) {
    try {
      final raw = html.window.localStorage[key];
      if (raw == null || raw.isEmpty) return const [];
      final value = jsonDecode(raw);
      return value is List ? value : const [];
    } catch (_) {
      return const [];
    }
  }

  Map<String, dynamic> _readJsonMap(String key) {
    try {
      final raw = html.window.localStorage[key];
      if (raw == null || raw.isEmpty) return const {};
      final value = jsonDecode(raw);
      return value is Map<String, dynamic> ? value : const {};
    } catch (_) {
      return const {};
    }
  }

  void _writeJson(String key, Object value) {
    try {
      html.window.localStorage[key] = jsonEncode(value);
    } catch (_) {
      // localStorage 不可用时只保留内存状态。
    }
  }
}
