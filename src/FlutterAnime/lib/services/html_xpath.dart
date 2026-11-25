/// 轻量 HTML DOM 与 XPath 子集。
/// 支持 Kazumi 规则常用的 `//`、`.//`、`[@attr='v']`、`[contains(@class,'v')]`、
/// `[2]`、`/text()`、`/@href` 等写法，不依赖浏览器 DOM。
library;

class HtmlNode {
  HtmlNode(this.tag, {this.parent});

  final String tag;
  final HtmlNode? parent;
  final Map<String, String> attributes = <String, String>{};
  final List<HtmlNode> children = <HtmlNode>[];
  final StringBuffer _text = StringBuffer();

  String get text => _text.toString();
  String attr(String name) => attributes[name.toLowerCase()] ?? '';

  String get ownText => text.trim();

  String get deepText {
    final buffer = StringBuffer();
    void walk(HtmlNode node) {
      if (node.text.isNotEmpty) buffer.write(node.text);
      for (final child in node.children) {
        walk(child);
      }
    }

    walk(this);
    return buffer.toString().replaceAll(RegExp(r'\s+'), ' ').trim();
  }

  List<HtmlNode> get descendants {
    final list = <HtmlNode>[];
    void walk(HtmlNode node) {
      for (final child in node.children) {
        list.add(child);
        walk(child);
      }
    }

    walk(this);
    return list;
  }

  List<HtmlNode> childrenByTag(String tag) => children
      .where((child) => tag == '*' || child.tag == tag.toLowerCase())
      .toList();
}

class HtmlParser {
  static HtmlNode parse(String html) {
    final root = HtmlNode('#document');
    final stack = <HtmlNode>[root];
    var index = 0;
    final length = html.length;
    const voidTags = {
      'br', 'img', 'hr', 'input', 'meta', 'link', 'source', 'area', 'base',
      'col', 'embed', 'param', 'track', 'wbr',
    };

    while (index < length) {
      final open = html.indexOf('<', index);
      if (open < 0) {
        _appendText(stack.last, html.substring(index));
        break;
      }
      if (open > index) {
        _appendText(stack.last, html.substring(index, open));
      }

      if (html.startsWith('<!--', open)) {
        final end = html.indexOf('-->', open + 4);
        index = end < 0 ? length : end + 3;
        continue;
      }
      if (html.startsWith('<![CDATA[', open)) {
        final end = html.indexOf(']]>', open + 9);
        if (end < 0) {
          index = length;
        } else {
          _appendText(stack.last, html.substring(open + 9, end));
          index = end + 3;
        }
        continue;
      }
      if (html.startsWith('<!', open) || html.startsWith('<?', open)) {
        final end = html.indexOf('>', open + 2);
        index = end < 0 ? length : end + 1;
        continue;
      }

      final close = _findTagEnd(html, open + 1);
      if (close < 0) {
        _appendText(stack.last, html.substring(open));
        break;
      }
      final raw = html.substring(open + 1, close).trim();
      index = close + 1;
      if (raw.isEmpty) continue;

      if (raw.startsWith('/')) {
        final tag = raw.substring(1).trim().split(RegExp(r'\s+')).first.toLowerCase();
        for (var i = stack.length - 1; i >= 1; i--) {
          if (stack[i].tag == tag) {
            stack.removeRange(i, stack.length);
            break;
          }
        }
        continue;
      }

      final selfClosing = raw.endsWith('/');
      final body = selfClosing ? raw.substring(0, raw.length - 1) : raw;
      final match = RegExp(r'^([^\s/>]+)').firstMatch(body);
      if (match == null) continue;
      final tag = match.group(1)!.toLowerCase();
      final node = HtmlNode(tag, parent: stack.last);
      _parseAttributes(body.substring(match.end), node.attributes);
      stack.last.children.add(node);

      if (tag == 'script' || tag == 'style') {
        final endTag = RegExp('</$tag\\s*>', caseSensitive: false).firstMatch(html.substring(index));
        if (endTag != null) {
          index += endTag.end;
        } else {
          index = length;
        }
        continue;
      }

      if (!selfClosing && !voidTags.contains(tag)) {
        stack.add(node);
      }
    }

    return root;
  }

  static int _findTagEnd(String html, int start) {
    var quote = '';
    for (var i = start; i < html.length; i++) {
      final ch = html[i];
      if (quote.isNotEmpty) {
        if (ch == quote) quote = '';
      } else if (ch == '"' || ch == "'") {
        quote = ch;
      } else if (ch == '>') {
        return i;
      }
    }
    return -1;
  }

  static void _parseAttributes(String source, Map<String, String> target) {
    final pattern = RegExp(
      r'''([^\s=/>]+)(?:\s*=\s*("[^"]*"|'[^']*'|[^\s>]+))?''',
      caseSensitive: false,
    );
    for (final match in pattern.allMatches(source)) {
      final name = match.group(1)?.toLowerCase() ?? '';
      if (name.isEmpty) continue;
      var value = match.group(2) ?? '';
      if (value.length >= 2 &&
          ((value.startsWith('"') && value.endsWith('"')) ||
              (value.startsWith("'") && value.endsWith("'")))) {
        value = value.substring(1, value.length - 1);
      }
      target[name] = decodeEntities(value);
    }
  }

  static void _appendText(HtmlNode node, String raw) {
    if (raw.isEmpty) return;
    node._text.write(decodeEntities(raw));
  }

  static String decodeEntities(String value) {
    if (!value.contains('&')) return value;
    const named = {
      'amp': '&',
      'lt': '<',
      'gt': '>',
      'quot': '"',
      'apos': "'",
      'nbsp': ' ',
      'copy': '©',
      'middot': '·',
      'hellip': '…',
      'mdash': '—',
      'ndash': '–',
      'ldquo': '“',
      'rdquo': '”',
      'lsquo': '‘',
      'rsquo': '’',
    };
    return value.replaceAllMapped(RegExp(r'&(#x?[0-9a-fA-F]+|#\d+|[a-zA-Z]+);'), (match) {
      final entity = match.group(1)!;
      if (entity.startsWith('#x') || entity.startsWith('#X')) {
        final code = int.tryParse(entity.substring(2), radix: 16);
        return code == null ? match.group(0)! : String.fromCharCode(code);
      }
      if (entity.startsWith('#')) {
        final code = int.tryParse(entity.substring(1));
        return code == null ? match.group(0)! : String.fromCharCode(code);
      }
      return named[entity.toLowerCase()] ?? match.group(0)!;
    });
  }
}

class XPathEngine {
  static const _descendant = '__desc__';

  static List<HtmlNode> nodes(HtmlNode root, String expression) {
    final expr = expression.trim();
    if (expr.isEmpty || expr == '//' || expr == './/') return const [];

    var terminalText = false;
    var terminalAttr = '';
    var path = expr;
    if (path.endsWith('/text()')) {
      terminalText = true;
      path = path.substring(0, path.length - 7);
    } else {
      final attrMatch = RegExp(r'/@([A-Za-z0-9_:-]+)$').firstMatch(path);
      if (attrMatch != null) {
        terminalAttr = attrMatch.group(1)!;
        path = path.substring(0, attrMatch.start);
      }
    }
    if (terminalText || terminalAttr.isNotEmpty) {
      final selected = nodes(root, path);
      final result = <HtmlNode>[];
      if (terminalText) return selected;
      if (terminalAttr.isNotEmpty) return selected;
      return result;
    }

    var startDescendant = false;
    if (path.startsWith('.//')) {
      startDescendant = true;
      path = path.substring(3);
    } else if (path.startsWith('//')) {
      startDescendant = true;
      path = path.substring(2);
    } else if (path.startsWith('./')) {
      path = path.substring(2);
    } else if (path.startsWith('/')) {
      path = path.substring(1);
    }

    final steps = _splitSteps(path);
    var current = <HtmlNode>[root];
    var first = true;
    for (final step in steps) {
      if (step.isEmpty || step == '.') {
        first = false;
        continue;
      }
      final next = <HtmlNode>[];
      final descendantMode = first && startDescendant;
      for (final node in current) {
        final candidates = descendantMode
            ? node.descendants
            : node.children
                .map((child) => child)
                .toList();
        next.addAll(_select(candidates, step));
      }
      current = next;
      first = false;
    }
    return current;
  }

  static String text(HtmlNode root, String expression) {
    final expr = expression.trim();
    if (expr.isEmpty) return '';
    final endsText = expr.endsWith('/text()');
    final attrMatch = RegExp(r'/@([A-Za-z0-9_:-]+)$').firstMatch(expr);
    if (endsText) {
      final selected = nodes(root, expr.substring(0, expr.length - 7));
      return selected.map((node) => node.deepText).join(' ').replaceAll(RegExp(r'\s+'), ' ').trim();
    }
    if (attrMatch != null) {
      final selected = nodes(root, expr.substring(0, attrMatch.start));
      return selected.map((node) => node.attr(attrMatch.group(1)!)).where((v) => v.isNotEmpty).join(' ').trim();
    }
    final selected = nodes(root, expr);
    return selected.map((node) => node.deepText).join(' ').replaceAll(RegExp(r'\s+'), ' ').trim();
  }

  static List<HtmlNode> _select(List<HtmlNode> candidates, String step) {
    final parsed = _Step.parse(step);
    var matches = candidates.where((node) {
      if (parsed.tag != '*' && node.tag != parsed.tag) return false;
      for (final predicate in parsed.predicates) {
        if (predicate.position != null || predicate.last) continue;
        if (!_matches(node, predicate)) return false;
      }
      return true;
    }).toList();

    for (final predicate in parsed.predicates) {
      if (predicate.position != null) {
        final position = predicate.position!;
        if (position <= 0 || position > matches.length) return const [];
        matches = [matches[position - 1]];
      } else if (predicate.last) {
        if (matches.isEmpty) return const [];
        matches = [matches.last];
      }
    }
    return matches;
  }

  static bool _matches(HtmlNode node, _Predicate predicate) {
    final attr = predicate.attr;
    if (attr != null) {
      final value = node.attr(attr);
      if (predicate.attrExistsOnly) return value.isNotEmpty;
      if (predicate.attrContains != null) {
        return value.toLowerCase().contains(predicate.attrContains!.toLowerCase());
      }
      return value == predicate.attrValue;
    }
    if (predicate.textContains != null || predicate.textEquals != null) {
      final text = node.deepText;
      if (predicate.textContains != null) {
        return text.toLowerCase().contains(predicate.textContains!.toLowerCase());
      }
      return text == predicate.textEquals;
    }
    return true;
  }

  static List<String> _splitSteps(String path) {
    final result = <String>[];
    final buffer = StringBuffer();
    var bracket = 0;
    var quote = '';
    for (var i = 0; i < path.length; i++) {
      final ch = path[i];
      if (quote.isNotEmpty) {
        buffer.write(ch);
        if (ch == quote) quote = '';
        continue;
      }
      if (ch == '"' || ch == "'") {
        quote = ch;
        buffer.write(ch);
        continue;
      }
      if (ch == '[') bracket++;
      if (ch == ']') bracket--;
      if (ch == '/' && bracket == 0) {
        result.add(buffer.toString());
        buffer.clear();
        continue;
      }
      buffer.write(ch);
    }
    if (buffer.isNotEmpty) result.add(buffer.toString());
    return result;
  }
}

class _Step {
  _Step(this.tag, this.predicates);
  final String tag;
  final List<_Predicate> predicates;

  static _Step parse(String raw) {
    final first = raw.indexOf('[');
    if (first < 0) return _Step(raw.toLowerCase(), const []);
    final tag = raw.substring(0, first).toLowerCase();
    final predicates = <_Predicate>[];
    var i = first;
    while (i < raw.length) {
      final open = raw.indexOf('[', i);
      if (open < 0) break;
      var depth = 0;
      var quote = '';
      var close = -1;
      for (var j = open; j < raw.length; j++) {
        final ch = raw[j];
        if (quote.isNotEmpty) {
          if (ch == quote) quote = '';
          continue;
        }
        if (ch == '"' || ch == "'") {
          quote = ch;
          continue;
        }
        if (ch == '[') depth++;
        if (ch == ']') {
          depth--;
          if (depth == 0) {
            close = j;
            break;
          }
        }
      }
      if (close < 0) break;
      predicates.add(_Predicate.parse(raw.substring(open + 1, close).trim()));
      i = close + 1;
    }
    return _Step(tag.isEmpty ? '*' : tag, predicates);
  }
}

class _Predicate {
  _Predicate({
    this.attr,
    this.attrValue = '',
    this.attrExistsOnly = false,
    this.attrContains,
    this.textContains,
    this.textEquals,
    this.position,
    this.last = false,
  });

  final String? attr;
  final String attrValue;
  final bool attrExistsOnly;
  final String? attrContains;
  final String? textContains;
  final String? textEquals;
  final int? position;
  final bool last;

  static _Predicate parse(String raw) {
    if (raw == 'last()') return _Predicate(last: true);
    final number = int.tryParse(raw);
    if (number != null) return _Predicate(position: number);

    final contains = RegExp(
      r"contains\(([^,]+),(.*)\)",
      caseSensitive: false,
    ).firstMatch(raw);
    if (contains != null) {
      final target = contains.group(1)!.trim().toLowerCase();
      final value = _unquote(contains.group(2)!.trim());
      if (target.startsWith('@')) {
        return _Predicate(attr: target.substring(1), attrContains: value);
      }
      return _Predicate(textContains: value);
    }

    final equals = RegExp(r'(.+?)\s*=\s*(.*)').firstMatch(raw);
    if (equals != null) {
      final target = equals.group(1)!.trim().toLowerCase();
      final value = _unquote(equals.group(2)!.trim());
      if (target.startsWith('@')) return _Predicate(attr: target.substring(1), attrValue: value);
      if (target == 'text()') return _Predicate(textEquals: value);
    }

    if (raw.startsWith('@')) {
      final name = raw.substring(1).trim().toLowerCase();
      if (name.isNotEmpty) return _Predicate(attr: name, attrExistsOnly: true);
    }
    return _Predicate();
  }

  static String _unquote(String value) {
    if (value.length >= 2 &&
        ((value.startsWith('"') && value.endsWith('"')) ||
            (value.startsWith("'") && value.endsWith("'")))) {
      return value.substring(1, value.length - 1);
    }
    return value;
  }
}
