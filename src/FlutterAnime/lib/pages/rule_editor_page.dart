import 'package:flutter/material.dart';

import '../models/anime_rule.dart';
import '../services/miko_api.dart';
import '../services/rule_store.dart';
import '../ui/theme.dart';

class RuleEditorPage extends StatefulWidget {
  const RuleEditorPage({super.key, this.rule});
  final AnimeRule? rule;

  @override
  State<RuleEditorPage> createState() => _RuleEditorPageState();
}

class _RuleEditorPageState extends State<RuleEditorPage> {
  late final TextEditingController _name;
  late final TextEditingController _displayName;
  late final TextEditingController _version;
  late final TextEditingController _baseUrl;
  late final TextEditingController _searchUrl;
  late final TextEditingController _searchList;
  late final TextEditingController _searchName;
  late final TextEditingController _searchResult;
  late final TextEditingController _chapterRoads;
  late final TextEditingController _chapterResult;
  late final TextEditingController _poster;
  late final TextEditingController _note;
  late final TextEditingController _description;
  final TextEditingController _testKeyword = TextEditingController(text: '凡人');
  bool _testing = false;
  String _testResult = '';
  String _error = '';

  @override
  void initState() {
    super.initState();
    final rule = widget.rule;
    _name = TextEditingController(text: rule?.name ?? '');
    _displayName = TextEditingController(text: rule?.displayName ?? '');
    _version = TextEditingController(text: rule?.version ?? '1.0');
    _baseUrl = TextEditingController(text: rule?.baseURL ?? '');
    _searchUrl = TextEditingController(text: rule?.searchURL ?? '');
    _searchList = TextEditingController(text: rule?.searchList ?? '');
    _searchName = TextEditingController(text: rule?.searchName ?? '');
    _searchResult = TextEditingController(text: rule?.searchResult ?? '');
    _chapterRoads = TextEditingController(text: rule?.chapterRoads ?? '');
    _chapterResult = TextEditingController(text: rule?.chapterResult ?? '');
    _poster = TextEditingController(text: rule?.searchPoster ?? '');
    _note = TextEditingController(text: rule?.searchNote ?? '');
    _description = TextEditingController(text: rule?.description ?? '');
  }

  @override
  void dispose() {
    for (final controller in [
      _name, _displayName, _version, _baseUrl, _searchUrl, _searchList,
      _searchName, _searchResult, _chapterRoads, _chapterResult, _poster,
      _note, _description, _testKeyword,
    ]) {
      controller.dispose();
    }
    super.dispose();
  }

  AnimeRule _collect() {
    final base = widget.rule?.copy() ??
        AnimeRule(name: '新规则', baseURL: '', searchURL: '', searchList: '', searchName: '', searchResult: '', chapterRoads: '', chapterResult: '');
    base
      ..name = _name.text.trim()
      ..displayName = _displayName.text.trim()
      ..version = _version.text.trim().isEmpty ? '1.0' : _version.text.trim()
      ..baseURL = _baseUrl.text.trim()
      ..searchURL = _searchUrl.text.trim()
      ..searchList = _searchList.text.trim()
      ..searchName = _searchName.text.trim()
      ..searchResult = _searchResult.text.trim()
      ..chapterRoads = _chapterRoads.text.trim()
      ..chapterResult = _chapterResult.text.trim()
      ..searchPoster = _poster.text.trim()
      ..searchNote = _note.text.trim()
      ..description = _description.text.trim();
    return base;
  }

  String? _validate() {
    if (_name.text.trim().isEmpty) return '规则名称不能为空';
    if (_baseUrl.text.trim().isEmpty) return 'BaseURL 不能为空';
    if (_searchUrl.text.trim().isEmpty || !_searchUrl.text.contains('@keyword')) {
      return '搜索地址必须包含 @keyword 占位符';
    }
    final core = [_searchList, _searchName, _searchResult, _chapterRoads, _chapterResult];
    for (var i = 0; i < core.length; i++) {
      if (core[i].text.trim().isEmpty) return '第 ${i + 1} 条核心 XPath 不能为空';
    }
    for (var i = 0; i < 4; i++) {
      final value = core[i].text.trim();
      if (!value.startsWith('//') && !value.startsWith('.//')) {
        return '第 ${i + 1} 条选择器需要以 // 或 .// 开头';
      }
    }
    return null;
  }

  Future<void> _save() async {
    final error = _validate();
    if (error != null) {
      setState(() => _error = error);
      return;
    }
    final rule = _collect()..enabled = true;
    if (rule.builtIn) rule.builtIn = true;
    await RuleStore.instance.saveRule(rule);
    if (!mounted) return;
    Navigator.of(context).pop(true);
  }

  Future<void> _test() async {
    final error = _validate();
    if (error != null) {
      setState(() => _error = error);
      return;
    }
    if (_testKeyword.text.trim().isEmpty) return;
    setState(() {
      _testing = true;
      _testResult = '';
      _error = '';
    });
    try {
      final cards = await MikoApi.instance.search(_collect(), _testKeyword.text.trim(), limit: 8);
      if (!mounted) return;
      setState(() {
        _testing = false;
        _testResult = cards.isEmpty
            ? '0 条结果：页面结构可能已变化，或该站开启了反爬。'
            : '成功解析 ${cards.length} 条：${cards.take(3).map((e) => e.title).join(' / ')}${cards.length > 3 ? ' …' : ''}';
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _testing = false;
        _testResult = '测试失败：$error';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: MikoColors.background,
      body: Column(
        children: [
          Container(
            height: 60,
            decoration: const BoxDecoration(color: Colors.white, border: Border(bottom: BorderSide(color: MikoColors.border))),
            padding: const EdgeInsets.symmetric(horizontal: 18),
            child: Row(
              children: [
                IconButton(
                  onPressed: () => Navigator.of(context).pop(),
                  icon: const Icon(Icons.arrow_back_rounded, size: 20, color: MikoColors.secondary),
                ),
                const SizedBox(width: 4),
                Text(
                  widget.rule == null ? '新建自定义规则' : '编辑规则 · ${widget.rule!.title}',
                  style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700),
                ),
                const Spacer(),
                TextButton(
                  onPressed: _testing ? null : _test,
                  child: _testing
                      ? const SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('测试规则'),
                ),
                const SizedBox(width: 8),
                FilledButton(
                  onPressed: _save,
                  child: const Text('保存规则'),
                ),
              ],
            ),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(26, 22, 26, 36),
              child: LayoutBuilder(builder: (context, constraints) {
                final wide = constraints.maxWidth > 980;
                final form = _buildForm();
                final aside = _buildAside();
                if (!wide) {
                  return Column(children: [form, const SizedBox(height: 16), aside]);
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(flex: 7, child: form),
                    const SizedBox(width: 18),
                    SizedBox(width: 320, child: aside),
                  ],
                );
              }),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildForm() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _Panel(
          title: '基础信息',
          subtitle: '规则名称会显示在番剧卡片角标与规则库中。',
          child: Column(
            children: [
              Row(
                children: [
                  Expanded(child: _Field(label: '规则名称', hint: '例如 7sefun', controller: _name)),
                  const SizedBox(width: 12),
                  Expanded(child: _Field(label: '显示名称', hint: '七色番', controller: _displayName)),
                  const SizedBox(width: 12),
                  SizedBox(width: 110, child: _Field(label: '版本', hint: '1.3', controller: _version)),
                ],
              ),
              const SizedBox(height: 14),
              _Field(label: 'BaseURL', hint: 'https://www.example.com/', controller: _baseUrl),
              const SizedBox(height: 14),
              _Field(
                label: '搜索地址',
                hint: 'https://www.example.com/search?wd=@keyword',
                controller: _searchUrl,
                helper: '使用 @keyword 作为关键词占位符，模块会自动 URL 编码。',
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        _Panel(
          title: '五行 XPath 核心选择器',
          subtitle: '只需这五条，就能构建一个站点规则的搜索与选集。',
          trailing: Container(
            padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
            decoration: BoxDecoration(color: MikoColors.primarySoft, borderRadius: BorderRadius.circular(7)),
            child: const Text('5 / 5', style: TextStyle(color: MikoColors.primaryDeep, fontSize: 10, fontWeight: FontWeight.w700)),
          ),
          child: Column(
            children: [
              _SelectorField(index: 1, label: '搜索列表 SearchList', hint: "//div[contains(@class,'video')]", controller: _searchList,
                  helper: '选中搜索结果卡片集合。'),
              _SelectorField(index: 2, label: '搜索结果名称 SearchName', hint: ".//div[@class='video-by']", controller: _searchName,
                  helper: '在单个结果卡片内取标题。'),
              _SelectorField(index: 3, label: '搜索结果链接 SearchResult', hint: ".//a[@class='video-wrapper']", controller: _searchResult,
                  helper: '取详情页链接，模块会自动补全相对地址。'),
              _SelectorField(index: 4, label: '章节线路 ChapterRoads', hint: "//div[@class='message-container vod-play-list-container']", controller: _chapterRoads,
                  helper: '选中一个或多个播放线路容器。'),
              _SelectorField(index: 5, label: '章节链接 ChapterResult', hint: ".//a[contains(@href,'/vodplay/')]", controller: _chapterResult,
                  helper: '在线路容器内取每一集链接。'),
            ],
          ),
        ),
        const SizedBox(height: 16),
        _Panel(
          title: '进阶字段（可选）',
          subtitle: '不填写时模块会自动从页面中寻找海报、备注与简介。',
          child: Column(
            children: [
              Row(
                children: [
                  Expanded(child: _Field(label: '搜索海报 XPath', hint: './/img[@class="videoimg"]/@src', controller: _poster)),
                  const SizedBox(width: 12),
                  Expanded(child: _Field(label: '搜索备注 XPath', hint: './/div[@class="video-time"]', controller: _note)),
                ],
              ),
              const SizedBox(height: 14),
              _Field(label: '规则说明', hint: '这条规则适合采集哪些站点、需要注意什么。', controller: _description, maxLines: 3),
            ],
          ),
        ),
        if (_error.isNotEmpty)
          Container(
            width: double.infinity,
            margin: const EdgeInsets.only(top: 14),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(color: const Color(0xFFFFF2F2), borderRadius: BorderRadius.circular(12), border: Border.all(color: const Color(0xFFF5D2D2))),
            child: Text(_error, style: const TextStyle(color: Color(0xFFC05050), fontSize: 11.5)),
          ),
      ],
    );
  }

  Widget _buildAside() {
    return Column(
      children: [
        Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(color: const Color(0xFF102B3D), borderRadius: BorderRadius.circular(18)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Row(
                children: [
                  Icon(Icons.account_tree_rounded, color: Color(0xFF9DD5F4), size: 18),
                  SizedBox(width: 8),
                  Text('规则契约', style: TextStyle(color: Colors.white, fontSize: 13.5, fontWeight: FontWeight.w700)),
                ],
              ),
              const SizedBox(height: 14),
              const Text(
                '采集流程',
                style: TextStyle(color: Color(0xFF7FA6BE), fontSize: 10.5, fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: 8),
              _flow('1', '搜索页', 'SearchURL + @keyword'),
              _flow('2', '结果卡片', 'SearchList · SearchName · SearchResult'),
              _flow('3', '详情页', 'ChapterRoads · ChapterResult'),
              _flow('4', '播放', '交给 MikoMiko 内置播放器取流'),
              const SizedBox(height: 14),
              const Divider(color: Color(0xFF25475D), height: 1),
              const SizedBox(height: 14),
              const Text(
                '支持 Kazumi 规则字段：api / type / name / version / muliSources / useNativePlayer / baseURL / searchURL / searchList / searchName / searchResult / chapterRoads / chapterResult。导入后会保留未知字段以外的核心字段。',
                style: TextStyle(color: Color(0xFF8EAFC4), fontSize: 10.5, height: 1.8),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18), border: Border.all(color: MikoColors.border)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('在线测试', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
              const SizedBox(height: 6),
              const Text('使用当前 XPath 请求真实站点搜索页。', style: TextStyle(fontSize: 10.5, color: MikoColors.tertiary)),
              const SizedBox(height: 12),
              TextField(
                controller: _testKeyword,
                decoration: const InputDecoration(hintText: '测试关键词'),
                onSubmitted: (_) => _test(),
              ),
              const SizedBox(height: 10),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton(
                  onPressed: _testing ? null : _test,
                  child: Text(_testing ? '正在请求站点…' : '运行测试'),
                ),
              ),
              if (_testResult.isNotEmpty) ...[
                const SizedBox(height: 12),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(11),
                  decoration: BoxDecoration(color: const Color(0xFFF4FAFE), borderRadius: BorderRadius.circular(10)),
                  child: Text(_testResult, style: const TextStyle(fontSize: 10.5, height: 1.7, color: MikoColors.secondary)),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }

  Widget _flow(String index, String title, String detail) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 18,
            height: 18,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: MikoColors.primary.withValues(alpha: 0.20), borderRadius: BorderRadius.circular(6)),
            child: Text(index, style: const TextStyle(color: Color(0xFF9DD5F4), fontSize: 9, fontWeight: FontWeight.w700)),
          ),
          const SizedBox(width: 9),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.w600)),
                const SizedBox(height: 2),
                Text(detail, style: const TextStyle(color: Color(0xFF7FA6BE), fontSize: 9.5, height: 1.5)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Panel extends StatelessWidget {
  const _Panel({required this.title, required this.child, this.subtitle = '', this.trailing});
  final String title;
  final String subtitle;
  final Widget child;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18), border: Border.all(color: MikoColors.border)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(title, style: const TextStyle(fontSize: 14.5, fontWeight: FontWeight.w700)),
              if (trailing != null) ...[const Spacer(), trailing!],
            ],
          ),
          if (subtitle.isNotEmpty) ...[
            const SizedBox(height: 5),
            Text(subtitle, style: const TextStyle(fontSize: 10.5, color: MikoColors.tertiary)),
          ],
          const SizedBox(height: 16),
          child,
        ],
      ),
    );
  }
}

class _Field extends StatelessWidget {
  const _Field({required this.label, required this.hint, required this.controller, this.helper = '', this.maxLines = 1});
  final String label;
  final String hint;
  final TextEditingController controller;
  final String helper;
  final int maxLines;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 10.5, fontWeight: FontWeight.w600, color: MikoColors.secondary)),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          maxLines: maxLines,
          style: const TextStyle(fontSize: 12),
          decoration: InputDecoration(hintText: hint, helperText: helper.isEmpty ? null : helper, helperMaxLines: 2),
        ),
      ],
    );
  }
}

class _SelectorField extends StatelessWidget {
  const _SelectorField({required this.index, required this.label, required this.hint, required this.controller, required this.helper});
  final int index;
  final String label;
  final String hint;
  final TextEditingController controller;
  final String helper;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 8),
      decoration: BoxDecoration(color: const Color(0xFFF8FBFD), borderRadius: BorderRadius.circular(12), border: Border.all(color: MikoColors.border)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 24,
            height: 24,
            margin: const EdgeInsets.only(top: 8),
            alignment: Alignment.center,
            decoration: BoxDecoration(color: MikoColors.primarySoft, borderRadius: BorderRadius.circular(7)),
            child: Text('$index', style: const TextStyle(color: MikoColors.primaryDeep, fontSize: 10, fontWeight: FontWeight.w700)),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: const TextStyle(fontSize: 10.5, color: MikoColors.secondary, fontWeight: FontWeight.w600)),
                const SizedBox(height: 4),
                TextField(
                  controller: controller,
                  style: const TextStyle(fontSize: 11.5, fontFamily: 'monospace'),
                  decoration: InputDecoration(
                    hintText: hint,
                    filled: true,
                    fillColor: Colors.white,
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 11),
                    helperText: helper,
                    helperStyle: const TextStyle(fontSize: 9.5, color: MikoColors.tertiary),
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
