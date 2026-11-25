import 'dart:convert';

/// 与 Kazumi 规则 JSON 兼容的规则模型。
/// 五个核心 XPath 选择器：搜索列表、名称、链接、章节线路、章节链接。
class AnimeRule {
  AnimeRule({
    required this.name,
    this.displayName = '',
    this.version = '1.0',
    this.author = '',
    this.description = '',
    this.baseURL = '',
    this.searchURL = '',
    this.searchList = '',
    this.searchName = '',
    this.searchResult = '',
    this.chapterRoads = '',
    this.chapterResult = '',
    this.searchPoster = '',
    this.searchNote = '',
    this.detailPoster = '',
    this.detailDescription = '',
    this.homeURL = '',
    this.homeList = '',
    this.homeName = '',
    this.homeResult = '',
    this.homePoster = '',
    this.homeNote = '',
    this.referer = '',
    this.builtIn = false,
    this.enabled = true,
  });

  String name;
  String displayName;
  String version;
  String author;
  String description;
  String baseURL;
  String searchURL;
  String searchList;
  String searchName;
  String searchResult;
  String chapterRoads;
  String chapterResult;
  String searchPoster;
  String searchNote;
  String detailPoster;
  String detailDescription;
  String homeURL;
  String homeList;
  String homeName;
  String homeResult;
  String homePoster;
  String homeNote;
  String referer;
  bool builtIn;
  bool enabled;

  String get title => displayName.trim().isEmpty ? name : displayName;
  String get host {
    final uri = Uri.tryParse(baseURL);
    return uri?.host ?? baseURL;
  }

  List<String> get coreSelectors =>
      [searchList, searchName, searchResult, chapterRoads, chapterResult];

  bool get hasCoreSelectors =>
      searchList.isNotEmpty &&
      searchName.isNotEmpty &&
      searchResult.isNotEmpty &&
      chapterResult.isNotEmpty;

  factory AnimeRule.fromJson(Map<String, dynamic> json, {bool builtIn = false}) {
    String s(String key, [String fallback = '']) {
      final value = json[key];
      return value == null ? fallback : value.toString();
    }

    return AnimeRule(
      name: s('name', '未命名规则'),
      displayName: s('displayName'),
      version: s('version', '1.0'),
      author: s('author'),
      description: s('description'),
      baseURL: s('baseURL'),
      searchURL: s('searchURL'),
      searchList: s('searchList'),
      searchName: s('searchName'),
      searchResult: s('searchResult'),
      chapterRoads: s('chapterRoads'),
      chapterResult: s('chapterResult'),
      searchPoster: s('searchPoster'),
      searchNote: s('searchNote'),
      detailPoster: s('detailPoster'),
      detailDescription: s('detailDescription'),
      homeURL: s('homeURL'),
      homeList: s('homeList'),
      homeName: s('homeName'),
      homeResult: s('homeResult'),
      homePoster: s('homePoster'),
      homeNote: s('homeNote'),
      referer: s('referer'),
      builtIn: builtIn || json['builtIn'] == true,
      enabled: json['enabled'] != false,
    );
  }

  factory AnimeRule.fromText(String text) {
    final decoded = jsonDecode(text);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('规则文件必须是一个 JSON 对象');
    }
    return AnimeRule.fromJson(decoded);
  }

  Map<String, dynamic> toJson({bool includeRuntime = false}) {
    final map = <String, dynamic>{
      'api': '4',
      'type': 'anime',
      'name': name,
      'displayName': displayName,
      'version': version,
      'author': author,
      'description': description,
      'baseURL': baseURL,
      'searchURL': searchURL,
      'searchList': searchList,
      'searchName': searchName,
      'searchResult': searchResult,
      'chapterRoads': chapterRoads,
      'chapterResult': chapterResult,
      'searchPoster': searchPoster,
      'searchNote': searchNote,
      'detailPoster': detailPoster,
      'detailDescription': detailDescription,
      'homeURL': homeURL,
      'homeList': homeList,
      'homeName': homeName,
      'homeResult': homeResult,
      'homePoster': homePoster,
      'homeNote': homeNote,
      'referer': referer,
      'muliSources': true,
      'useWebview': true,
      'useNativePlayer': true,
    };
    if (includeRuntime) {
      map['builtIn'] = builtIn;
      map['enabled'] = enabled;
    }
    return map;
  }

  String toPrettyJson({bool includeRuntime = false}) =>
      const JsonEncoder.withIndent('  ').convert(toJson(includeRuntime: includeRuntime));

  AnimeRule copy() => AnimeRule.fromJson(toJson(includeRuntime: true),
      builtIn: builtIn);

  String searchUrlFor(String keyword) {
    final template = searchURL.trim();
    if (template.isEmpty) return '';
    return template.replaceAll('@keyword', Uri.encodeComponent(keyword));
  }

  String absolute(String url) {
    final value = url.trim();
    if (value.isEmpty) return '';
    if (value.startsWith('http://') || value.startsWith('https://')) return value;
    if (value.startsWith('//')) return '${Uri.tryParse(baseURL)?.scheme ?? 'https'}:$value';
    final base = Uri.tryParse(baseURL);
    if (base == null) return value;
    return base.resolve(value).toString();
  }
}

class AnimeCard {
  AnimeCard({
    required this.title,
    required this.url,
    required this.source,
    this.poster = '',
    this.note = '',
    this.rule,
  });

  final String title;
  final String url;
  final String source;
  final String poster;
  final String note;
  final AnimeRule? rule;

  AnimeCard copyWith({String? poster, String? note, String? title}) => AnimeCard(
        title: title ?? this.title,
        url: url,
        source: source,
        poster: poster ?? this.poster,
        note: note ?? this.note,
        rule: rule,
      );
}

class AnimeEpisode {
  AnimeEpisode({required this.name, required this.url, required this.order});
  final String name;
  final String url;
  final int order;
}

class AnimeRoad {
  AnimeRoad({required this.name, required this.episodes});
  final String name;
  final List<AnimeEpisode> episodes;
}

class AnimeDetail {
  AnimeDetail({
    required this.title,
    required this.url,
    required this.source,
    required this.roads,
    this.poster = '',
    this.description = '',
    this.note = '',
    this.rule,
  });

  final String title;
  final String url;
  final String source;
  final List<AnimeRoad> roads;
  final String poster;
  final String description;
  final String note;
  final AnimeRule? rule;

  AnimeEpisode? get firstEpisode {
    for (final road in roads) {
      if (road.episodes.isNotEmpty) return road.episodes.first;
    }
    return null;
  }
}
