import 'dart:js_interop';

import 'dart:convert';
import 'dart:html' as html;
import 'dart:js_util' as js;

import '../models/anime_rule.dart';

/// WPF 注入的全局回调。dart2js 下 js_util.callMethod(window, ...) 在部分
/// WebView2 版本里会抛异常并被吞掉，导致点选集完全没有反应，这里改为静态绑定。
@JS('mikoPost')
external void _hostPost(String message);

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

  static void openBilibili(String url, String title) {
    _post(<String, dynamic>{
      'type': 'openBilibili',
      'url': url,
      'title': title,
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
    // window.postMessage 是最稳定的 DOM 通道，由 index.html 转发给 mikoPost。
    try {
      html.window.postMessage(<String, dynamic>{
        '__mikoHost': true,
        'payload': payload,
      }, '*');
      return;
    } catch (_) {}
    final message = jsonEncode(payload);
    try {
      html.window.dispatchEvent(html.CustomEvent('miko-host-post', detail: message));
      return;
    } catch (_) {}
    try {
      _hostPost(message);
      return;
    } catch (_) {}
    // 浏览器调试环境没有注入 mikoPost，退回通用调用。
    try {
      js.callMethod(html.window, 'mikoPost', [message]);
    } catch (_) {
      // 浏览器调试环境下静默忽略。
    }
  }
}
