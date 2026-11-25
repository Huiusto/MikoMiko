import 'dart:convert';
import 'dart:html' as html;
import 'dart:js_util' as js;

import '../models/anime_rule.dart';

/// 与 WPF Shell 通信：播放交给原生内置播放器，文件导出交给 WebView2。
class HostBridge {
  static void play(AnimeDetail detail, AnimeEpisode episode) {
    _post(<String, dynamic>{
      'type': 'play',
      'pageUrl': episode.url,
      'title': detail.title,
      'poster': detail.poster,
      'source': detail.source,
      'referer': detail.rule?.baseURL ?? '',
      'rule': detail.rule?.name ?? '',
      'episode': episode.name,
      'anime4k': true,
    });
  }

  static void toast(String text) {
    _post(<String, dynamic>{'type': 'toast', 'text': text});
  }

  static void openRuleFolder() {
    _post(<String, dynamic>{'type': 'openRuleFolder'});
  }

  static void download(String filename, String content) {
    try {
      js.callMethod(html.window, 'mikoDownload', [filename, content]);
    } catch (_) {
      final blob = html.Blob([content], 'application/json');
      final url = html.Url.createObjectUrlFromBlob(blob);
      final anchor = html.AnchorElement(href: url)
        ..download = filename
        ..click();
      html.Url.revokeObjectUrl(url);
    }
  }

  static Future<void> copy(String text) async {
    try {
      await js.promiseToFuture<dynamic>(
        js.callMethod(html.window.navigator, 'clipboard.writeText', [text]),
      );
    } catch (_) {
      final textArea = html.TextAreaElement()
        ..value = text
        ..style.position = 'fixed'
        ..style.opacity = '0';
      html.document.body?.append(textArea);
      textArea.select();
      html.document.execCommand('copy');
      textArea.remove();
    }
  }

  static void _post(Map<String, dynamic> payload) {
    try {
      js.callMethod(html.window, 'mikoPost', [jsonEncode(payload)]);
    } catch (_) {
      // 浏览器调试环境下静默忽略。
    }
  }
}
