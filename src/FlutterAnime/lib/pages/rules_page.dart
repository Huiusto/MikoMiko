import 'dart:html' as html;

import 'package:flutter/material.dart';

import '../models/anime_rule.dart';
import '../services/host_bridge.dart';
import '../services/rule_store.dart';
import '../ui/theme.dart';
import '../ui/widgets.dart';
import 'rule_editor_page.dart';

class RulesPage extends StatelessWidget {
  const RulesPage({super.key});

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: RuleStore.instance,
      builder: (context, _) {
        final store = RuleStore.instance;
        final rules = store.rules;
        final enabled = store.enabledRules.length;
        return SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(26, 24, 26, 36),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                padding: const EdgeInsets.all(22),
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [Color(0xFF13374C), Color(0xFF0D2535)],
                  ),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Row(
                  children: [
                    Container(
                      width: 52,
                      height: 52,
                      decoration: BoxDecoration(
                        color: MikoColors.primary.withValues(alpha: 0.22),
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: const Icon(Icons.rule_folder_rounded, color: Color(0xFFA5DAF6), size: 26),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text('规则库', style: TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.w700)),
                          const SizedBox(height: 5),
                          Text(
                            '共 ${rules.length} 条规则，${enabled} 条启用。规则由 Flutter 解析，最多五行 XPath 即可采集站点。',
                            style: const TextStyle(color: Color(0xFFA5C8DA), fontSize: 11.5, height: 1.7),
                          ),
                        ],
                      ),
                    ),
                    FilledButton.icon(
                      onPressed: () => _import(context),
                      icon: const Icon(Icons.file_download_outlined, size: 17),
                      label: const Text('导入规则'),
                    ),
                    const SizedBox(width: 10),
                    OutlinedButton.icon(
                      onPressed: () => _edit(context, null),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: Colors.white,
                        side: BorderSide(color: Colors.white.withValues(alpha: 0.28)),
                      ),
                      icon: const Icon(Icons.add_rounded, size: 17),
                      label: const Text('新建规则'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              Row(
                children: [
                  const SectionHeading(title: '我的规则', subtitle: '内置规则可停用，自定义规则可编辑与分享'),
                  const Spacer(),
                  Text('XPath 5 行 · Kazumi 兼容', style: TextStyle(fontSize: 10.5, color: MikoColors.tertiary)),
                ],
              ),
              const SizedBox(height: 14),
              for (final rule in rules) ...[
                _RuleRow(
                  rule: rule,
                  onToggle: (value) => store.toggleRule(rule, value),
                  onEdit: () => _edit(context, rule),
                  onShare: () => _share(context, rule),
                  onDelete: () => _delete(context, rule),
                ),
                const SizedBox(height: 10),
              ],
              const SizedBox(height: 10),
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(16), border: Border.all(color: MikoColors.border)),
                child: Row(
                  children: [
                    const Icon(Icons.info_outline_rounded, color: MikoColors.primary, size: 18),
                    const SizedBox(width: 10),
                    const Expanded(
                      child: Text(
                        '规则来源为站点公开页面。若站点改版导致解析为空，请在规则编辑器里更新选择器；也可以导入来自 KazumiRules 的 JSON。',
                        style: TextStyle(fontSize: 10.5, color: MikoColors.secondary, height: 1.7),
                      ),
                    ),
                    const SizedBox(width: 12),
                    TextButton(
                      onPressed: HostBridge.openRuleFolder,
                      child: const Text('规则目录'),
                    ),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  Future<void> _edit(BuildContext context, AnimeRule? rule) async {
    final saved = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => RuleEditorPage(rule: rule)),
    );
    if (saved == true && context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('规则已保存')));
    }
  }

  Future<void> _import(BuildContext context) async {
    final controller = TextEditingController();
    final imported = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: Colors.white,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
        title: const Row(
          children: [
            Icon(Icons.file_open_outlined, color: MikoColors.primary, size: 20),
            SizedBox(width: 10),
            Text('导入规则 JSON', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
          ],
        ),
        content: SizedBox(
          width: 620,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('粘贴 Kazumi 规则 JSON，或从本地选择 .json 文件。', style: TextStyle(fontSize: 11.5, color: MikoColors.secondary)),
              const SizedBox(height: 12),
              TextField(
                controller: controller,
                maxLines: 10,
                style: const TextStyle(fontSize: 11, fontFamily: 'monospace'),
                decoration: const InputDecoration(hintText: '{\n  "name": "示例",\n  "baseURL": "https://...",\n  "searchURL": "...@keyword",\n  "searchList": "//...",\n  ...\n}'),
              ),
              const SizedBox(height: 10),
              Row(
                children: [
                  OutlinedButton.icon(
                    onPressed: () async {
                      final input = html.FileUploadInputElement()..accept = '.json,application/json';
                      input.click();
                      input.onChange.listen((_) {
                        final file = input.files?.first;
                        if (file == null) return;
                        final reader = html.FileReader();
                        reader.readAsText(file);
                        reader.onLoad.listen((_) {
                          controller.text = reader.result?.toString() ?? '';
                        });
                      });
                    },
                    icon: const Icon(Icons.folder_open_rounded, size: 16),
                    label: const Text('选择文件'),
                  ),
                  const SizedBox(width: 10),
                  const Text('文件名会忽略，规则名称以 JSON 内 name 为准。', style: TextStyle(fontSize: 10, color: MikoColors.tertiary)),
                ],
              ),
            ],
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('取消')),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(controller.text),
            child: const Text('导入'),
          ),
        ],
      ),
    );
    controller.dispose();
    if (imported == null || imported.trim().isEmpty) return;
    try {
      final name = await RuleStore.instance.importRule(imported);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('已导入规则「$name」')));
      }
    } catch (error) {
      if (context.mounted) {
        showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
            title: const Text('导入失败'),
            content: Text('$error', style: const TextStyle(fontSize: 12)),
            actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('知道了'))],
          ),
        );
      }
    }
  }

  Future<void> _share(BuildContext context, AnimeRule rule) async {
    final text = rule.toPrettyJson(includeRuntime: true);
    await HostBridge.copy(text);
    HostBridge.download('${rule.name}.json', text);
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('「${rule.title}」JSON 已复制，并导出为 ${rule.name}.json')),
      );
    }
  }

  Future<void> _delete(BuildContext context, AnimeRule rule) async {
    final isBuiltIn = rule.builtIn;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(isBuiltIn ? '停用内置规则？' : '删除自定义规则？'),
        content: Text(isBuiltIn
            ? '内置规则「${rule.title}」将不再参与搜索与首页采集，之后可以重新启用。'
            : '规则「${rule.title}」将从规则库中移除，已导出的 JSON 不受影响。'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('取消')),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            style: FilledButton.styleFrom(backgroundColor: isBuiltIn ? null : const Color(0xFFD66B6B)),
            child: Text(isBuiltIn ? '停用' : '删除'),
          ),
        ],
      ),
    );
    if (confirmed == true) await RuleStore.instance.deleteRule(rule);
  }
}

class _RuleRow extends StatelessWidget {
  const _RuleRow({
    required this.rule,
    required this.onToggle,
    required this.onEdit,
    required this.onShare,
    required this.onDelete,
  });

  final AnimeRule rule;
  final ValueChanged<bool> onToggle;
  final VoidCallback onEdit;
  final VoidCallback onShare;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 17, vertical: 15),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: rule.enabled ? MikoColors.borderStrong : MikoColors.border),
      ),
      child: Row(
        children: [
          Container(
            width: 42,
            height: 42,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: rule.enabled ? MikoColors.primarySoft : const Color(0xFFF1F5F8),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              rule.title.characters.take(1).toString(),
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w800,
                color: rule.enabled ? MikoColors.primaryDeep : MikoColors.tertiary,
              ),
            ),
          ),
          const SizedBox(width: 13),
          SizedBox(
            width: 170,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Flexible(
                      child: Text(
                        rule.title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700),
                      ),
                    ),
                    if (rule.builtIn) ...[
                      const SizedBox(width: 7),
                      const TagChip(text: '内置', color: Color(0xFFF1F7FB), foreground: MikoColors.secondary),
                    ],
                  ],
                ),
                const SizedBox(height: 3),
                Text(
                  '${rule.name} · v${rule.version}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 9.5, color: MikoColors.tertiary),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Icon(Icons.link_rounded, size: 12, color: MikoColors.tertiary),
                    const SizedBox(width: 5),
                    Expanded(
                      child: Text(
                        rule.host.isEmpty ? '未填写站点地址' : rule.host,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 10.5, color: MikoColors.secondary),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 5),
                Text(
                  rule.description.isEmpty ? 'SearchURL：${rule.searchURL.isEmpty ? '未设置' : rule.searchURL}' : rule.description,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 9.5, color: MikoColors.tertiary),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 9),
            decoration: BoxDecoration(
              color: const Color(0xFFF7FAFC),
              borderRadius: BorderRadius.circular(9),
              border: Border.all(color: MikoColors.border),
            ),
            child: DropdownButtonHideUnderline(
              child: DropdownButton<String>(
                value: '5',
                isDense: true,
                style: const TextStyle(fontSize: 10, color: MikoColors.secondary),
                items: const [DropdownMenuItem(value: '5', child: Text('XPath 5 行'))],
                onChanged: (_) {},
              ),
            ),
          ),
          const SizedBox(width: 10),
          Switch(value: rule.enabled, onChanged: onToggle),
          IconButton(
            onPressed: onEdit,
            icon: const Icon(Icons.edit_outlined, size: 17, color: MikoColors.secondary),
            tooltip: '编辑',
          ),
          IconButton(
            onPressed: onShare,
            icon: const Icon(Icons.share_outlined, size: 17, color: MikoColors.secondary),
            tooltip: '分享',
          ),
          IconButton(
            onPressed: onDelete,
            icon: const Icon(Icons.delete_outline_rounded, size: 17, color: MikoColors.tertiary),
            tooltip: rule.builtIn ? '停用' : '删除',
          ),
        ],
      ),
    );
  }
}
