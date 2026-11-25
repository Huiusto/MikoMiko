import 'dart:async';
import 'dart:convert';
import 'dart:html' as html;

import '../models/anime_rule.dart';
import 'html_xpath.dart';

/// 通过 WPF 宿主提供的同源代理读取站点页面，避免 WebView2 的跨域限制。
class MikoApi {
  MikoApi._();
  static final MikoApi instance = MikoApi._();

  final Map<String, String> _cache = <String, String>{};

  String proxied(String url, {String referer = '', bool asImage = false}) {
    final query = StringBuffer('url=${Uri.encodeQueryComponent(url)}');
    if (referer.isNotEmpty) query.write('&referer=${Uri.encodeQueryComponent(referer)}');
    if (asImage) query.write('&image=1');
    return 'https://miko.proxy/?$query';
  }

  Future<String> fetch(String url, {String referer = '', int timeoutSeconds = 18}) async {
    final cacheKey = '$url|$referer';
    final cached = _cache[cacheKey];
    if (cached != null) return cached;
    final request = html.HttpRequest.getString(proxied(url, referer: referer))
        .timeout(Duration(seconds: timeoutSeconds));
    final text = await request;
    if (_cache.length > 80) _cache.clear();
    _cache[cacheKey] = text;
    return text;
  }

  Future<List<AnimeCard>> search(AnimeRule rule, String keyword, {int limit = 18}) async {
    final searchUrl = rule.searchUrlFor(keyword);
    if (searchUrl.isEmpty || rule.searchList.isEmpty) return const [];
    final body = await fetch(searchUrl, referer: rule.referer.isEmpty ? rule.baseURL : rule.referer);
    final doc = HtmlParser.parse(body);
    final items = XPathEngine.nodes(doc, rule.searchList).take(limit).toList();
    final cards = <AnimeCard>[];
    for (final item in items) {
      var title = XPathEngine.text(item, rule.searchName);
      if (title.isEmpty) {
        title = item.attributes['title'] ?? '';
      }
      if (title.isEmpty) {
        title = item.children
            .where((child) => child.tag == 'a' || child.tag == 'img')
            .map((child) => child.attr('title').isNotEmpty
                ? child.attr('title')
                : child.attr('alt'))
            .firstWhere((value) => value.isNotEmpty, orElse: () => '');
      }
      if (title.isEmpty) title = item.deepText;
      title = _cleanTitle(title);
      if (title.isEmpty) continue;

      var link = XPathEngine.text(item, rule.searchResult);
      if (link.isEmpty) {
        final anchor = _first(item, (node) => node.tag == 'a' && node.attr('href').isNotEmpty);
        link = anchor?.attr('href') ?? '';
      }
      if (link.isEmpty) continue;

      var poster = rule.searchPoster.isEmpty ? '' : XPathEngine.text(item, rule.searchPoster);
      if (poster.isEmpty) {
        final image = _first(item, (node) => node.tag == 'img' &&
            (node.attr('src').isNotEmpty ||
             node.attr('data-src').isNotEmpty ||
             node.attr('data-original').isNotEmpty));
        poster = image == null ? '' : (image.attr('src').isNotEmpty
            ? image.attr('src')
            : image.attr('data-src').isNotEmpty
                ? image.attr('data-src')
                : image.attr('data-original'));
      }
      final note = rule.searchNote.isEmpty ? '' : _cleanNote(XPathEngine.text(item, rule.searchNote));

      cards.add(AnimeCard(
        title: title,
        url: rule.absolute(link),
        source: rule.title,
        poster: rule.absolute(poster),
        note: note,
        rule: rule,
      ));
    }
    return cards;
  }

  Future<List<AnimeCard>> home(AnimeRule rule, {int limit = 24}) async {
    if (rule.homeURL.isEmpty || rule.homeList.isEmpty) return const [];
    final body = await fetch(rule.homeURL, referer: rule.baseURL);
    final doc = HtmlParser.parse(body);
    final items = XPathEngine.nodes(doc, rule.homeList).take(limit).toList();
    final cards = <AnimeCard>[];
    for (final item in items) {
      var title = XPathEngine.text(item, rule.homeName);
      if (title.isEmpty) title = item.attr('title');
      if (title.isEmpty) title = _cleanTitle(item.deepText);
      if (title.isEmpty) continue;
      var link = XPathEngine.text(item, rule.homeResult);
      if (link.isEmpty) {
        link = _first(item, (node) => node.tag == 'a' && node.attr('href').isNotEmpty)?.attr('href') ?? '';
      }
      if (link.isEmpty) continue;
      var poster = rule.homePoster.isEmpty ? '' : XPathEngine.text(item, rule.homePoster);
      if (poster.isEmpty) {
        final image = _first(item, (node) => node.tag == 'img' &&
            (node.attr('src').isNotEmpty ||
             node.attr('data-src').isNotEmpty ||
             node.attr('data-original').isNotEmpty));
        poster = image == null ? '' : (image.attr('src').isNotEmpty
            ? image.attr('src')
            : image.attr('data-src').isNotEmpty
                ? image.attr('data-src')
                : image.attr('data-original'));
      }
      cards.add(AnimeCard(
        title: _cleanTitle(title),
        url: rule.absolute(link),
        source: rule.title,
        poster: rule.absolute(poster),
        note: rule.homeNote.isEmpty ? '' : _cleanNote(XPathEngine.text(item, rule.homeNote)),
        rule: rule,
      ));
    }
    return cards;
  }

  Future<AnimeDetail> detail(AnimeCard card) async {
    final rule = card.rule!;
    final body = await fetch(card.url, referer: rule.referer.isEmpty ? rule.baseURL : rule.referer);
    final doc = HtmlParser.parse(body);

    String poster = rule.detailPoster.isEmpty ? '' : XPathEngine.text(doc, rule.detailPoster);
    if (poster.isEmpty) {
      poster = _meta(doc, 'og:image');
    }
    if (poster.isEmpty) {
      final image = _first(doc, (node) {
        if (node.tag != 'img') return false;
        final src = node.attr('data-src').isNotEmpty
            ? node.attr('data-src')
            : node.attr('data-original').isNotEmpty
                ? node.attr('data-original')
                : node.attr('src');
        if (src.isEmpty) return false;
        final cls = node.attr('class').toLowerCase();
        return cls.contains('cover') || cls.contains('poster') || cls.contains('pic') || cls.contains('lazy');
      });
      poster = image == null ? '' : (image.attr('data-src').isNotEmpty
          ? image.attr('data-src')
          : image.attr('data-original').isNotEmpty
              ? image.attr('data-original')
              : image.attr('src'));
    }
    if (poster.isEmpty) poster = card.poster;

    var description = rule.detailDescription.isEmpty
        ? ''
        : XPathEngine.text(doc, rule.detailDescription);
    if (description.isEmpty) description = _meta(doc, 'description');
    if (description.isEmpty) description = _meta(doc, 'og:description');
    description = _cleanDescription(description);

    final roads = <AnimeRoad>[];
    final roadNodes = rule.chapterRoads.isEmpty ? <HtmlNode>[] : XPathEngine.nodes(doc, rule.chapterRoads);
    if (roadNodes.isNotEmpty) {
      final roadKeys = <String>{};
      for (var i = 0; i < roadNodes.length; i++) {
        final roadNode = roadNodes[i];
        // 站点会放一份 display:none 的移动端线路，跳过避免重复。
        final style = roadNode.attr('style').replaceAll(' ', '').toLowerCase();
        if (style.contains('display:none')) continue;
        final episodes = _episodes(roadNode, rule);
        if (episodes.isEmpty) continue;
        final key = episodes.map((episode) => episode.url).join('|');
        if (!roadKeys.add(key)) continue;
        roads.add(AnimeRoad(name: _roadName(roadNode, roads.length), episodes: episodes));
      }
    }
    if (roads.isEmpty && rule.chapterResult.isNotEmpty) {
      final episodes = _episodes(doc, rule);
      if (episodes.isNotEmpty) roads.add(AnimeRoad(name: '默认线路', episodes: episodes));
    }
    if (roads.isEmpty) {
      final fallback = _fallbackEpisodes(doc, rule);
      if (fallback.isNotEmpty) roads.add(AnimeRoad(name: '默认线路', episodes: fallback));
    }

    return AnimeDetail(
      title: card.title,
      url: card.url,
      source: card.source,
      roads: roads,
      poster: rule.absolute(poster),
      description: description,
      note: card.note,
      rule: rule,
    );
  }

  List<AnimeEpisode> _episodes(HtmlNode root, AnimeRule rule) {
    final nodes = XPathEngine.nodes(root, rule.chapterResult);
    final episodes = <AnimeEpisode>[];
    final seen = <String>{};
    for (final node in nodes) {
      final href = node.attr('href');
      if (href.isEmpty || href.startsWith('javascript') || href == '#') continue;
      final url = rule.absolute(href);
      if (!seen.add(url)) continue;
      var name = node.attr('title');
      if (name.isEmpty) name = node.deepText;
      if (name.isEmpty) name = '第${episodes.length + 1}集';
      episodes.add(AnimeEpisode(
        name: _cleanTitle(name),
        url: url,
        order: episodes.length + 1,
      ));
    }
    return episodes;
  }

  List<AnimeEpisode> _fallbackEpisodes(HtmlNode doc, AnimeRule rule) {
    final anchors = _all(doc, (node) {
      if (node.tag != 'a') return false;
      final href = node.attr('href');
      return href.contains('/vodplay/') ||
          href.contains('/play/') ||
          href.contains('/watch') ||
          href.contains('/episode');
    });
    return _episodesFromAnchors(anchors, rule);
  }

  List<AnimeEpisode> _episodesFromAnchors(List<HtmlNode> anchors, AnimeRule rule) {
    final episodes = <AnimeEpisode>[];
    final seen = <String>{};
    for (final node in anchors) {
      final href = node.attr('href');
      if (href.isEmpty) continue;
      final url = rule.absolute(href);
      if (!seen.add(url)) continue;
      var name = node.attr('title');
      if (name.isEmpty) name = node.deepText;
      if (name.isEmpty) name = '第${episodes.length + 1}集';
      episodes.add(AnimeEpisode(name: _cleanTitle(name), url: url, order: episodes.length + 1));
    }
    return episodes;
  }

  String _roadName(HtmlNode node, int index) {
    final title = node.attr('title');
    if (title.isNotEmpty) return _cleanRoad(title, index);
    final previous = node.parent;
    if (previous != null) {
      for (final child in previous.children) {
        if (identical(child, node)) break;
        final bfq = _first(child, (item) =>
            item.attr('class').toLowerCase().contains('chat-stream-bfq'));
        if (bfq != null) {
          var text = bfq.attr('title').isNotEmpty ? bfq.attr('title') : bfq.deepText;
          if (text == '播放器选择' || text == '播放源') {
            // 下拉选择器的标题不是线路名，真实线路在紧随其后的列表里。
            final list = _first(child, (item) =>
                item.tag == 'ul' && item.attr('class').toLowerCase().contains('bfqs'));
            if (list != null) {
              final active = _first(list, (item) =>
                  item.tag == 'li' && item.attr('class').toLowerCase().contains('active'));
              if (active != null && active.deepText.isNotEmpty) text = active.deepText;
            }
          }
          if (text.isNotEmpty && text != '播放器选择' && text != '播放源') {
            return _cleanRoad(text, index);
          }
        }
        if (child.tag == 'div' || child.tag == 'h3' || child.tag == 'span') {
          final text = child.deepText;
          if (text.isNotEmpty && text.length <= 36) {
            final name = _cleanRoad(text, index);
            if (name.isNotEmpty) return name;
          }
        }
      }
    }
    return '线路 ${index + 1}';
  }

  String _cleanRoad(String value, int index) {
    var text = _cleanNote(_cleanTitle(value));
    text = text.replaceAll(RegExp(r'^\s*线路\s*'), '').trim();
    // 站点常用“七色A线 七色A线 · 8”这种重复标题，取第一段即可。
    text = text.split('·').first.trim();
    final parts = text.split(RegExp(r'\s+')).where((part) => part.isNotEmpty).toList();
    if (parts.length > 1) {
      final distinct = <String>[];
      for (final part in parts) {
        if (!distinct.contains(part)) distinct.add(part);
      }
      if (distinct.length == 1) text = distinct.first;
    }
    return text.isEmpty ? '线路 ${index + 1}' : text;
  }

  String _meta(HtmlNode doc, String name) {
    final nodes = XPathEngine.nodes(doc, '//meta');
    for (final node in nodes) {
      final key = node.attr('property').isNotEmpty ? node.attr('property') : node.attr('name');
      if (key.toLowerCase() == name.toLowerCase()) {
        return HtmlParser.decodeEntities(node.attr('content'));
      }
    }
    return '';
  }

  HtmlNode? _first(HtmlNode root, bool Function(HtmlNode) test) {
    for (final node in root.descendants) {
      if (test(node)) return node;
    }
    return null;
  }

  List<HtmlNode> _all(HtmlNode root, bool Function(HtmlNode) test) {
    final list = <HtmlNode>[];
    for (final node in root.descendants) {
      if (test(node)) list.add(node);
    }
    return list;
  }

  String _cleanTitle(String value) {
    var text = value.replaceAll(RegExp(r'\s+'), ' ').trim();
    text = text.replaceAll(RegExp(r'^(更新至|已更新至)\s*\d+\s*[集话]?\s*'), '');
    text = text.replaceAll(RegExp(r'^(全\s*\d+\s*[集话]|共\s*\d+\s*[集话]|\d+\s*集)\s*'), '');
    // 站点标题常把「更新至N集」串在一起，详情与卡片都只保留作品名。
    text = text.replaceAll(RegExp(r'\s*更新至\s*.*$'), '').trim();
    return text;
  }

  String _cleanNote(String value) {
    var text = value.replaceAll(RegExp(r'\s+'), ' ').trim();
    text = text.replaceAll(RegExp(r'^(更新至|已更新至)\s*'), '');
    text = text.replaceAll(RegExp(r'\s*更新至\s*.*$'), '').trim();
    return text;
  }

  String _cleanDescription(String value) {
    final text = value.replaceAll(RegExp(r'\s+'), ' ').trim();
    if (text.length <= 8) return '';
    return text;
  }
}
