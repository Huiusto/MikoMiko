import 'package:flutter/material.dart';

import '../services/host_bridge.dart';
import '../services/rule_store.dart';
import '../ui/theme.dart';
import '../ui/widgets.dart';

class ModuleSettingsPage extends StatelessWidget {
  const ModuleSettingsPage({super.key, required this.onExportRules});

  final VoidCallback onExportRules;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: RuleStore.instance,
      builder: (context, _) {
        final store = RuleStore.instance;
        return SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(26, 24, 26, 36),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SectionHeading(title: '模块设置', subtitle: 'Flutter 规则引擎 · 本机配置'),
              const SizedBox(height: 16),
              Container(
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18), border: Border.all(color: MikoColors.border)),
                child: Column(
                  children: [
                    Row(
                      children: [
                        Container(
                          width: 44,
                          height: 44,
                          decoration: BoxDecoration(color: const Color(0xFFEAF4FF), borderRadius: BorderRadius.circular(12)),
                          child: const Icon(Icons.auto_awesome_rounded, color: Color(0xFF3D7BD0), size: 22),
                        ),
                        const SizedBox(width: 14),
                        const Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Anime4K 实时超分', style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
                              SizedBox(height: 4),
                              Text('播放时开启 WebGL 边缘锐化与自适应放大，默认启用。', style: TextStyle(fontSize: 10.5, color: MikoColors.tertiary)),
                            ],
                          ),
                        ),
                        Switch(value: store.anime4k, onChanged: (value) => store.setAnime4k(value)),
                      ],
                    ),
                    const SizedBox(height: 14),
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(color: const Color(0xFFF8FBFD), borderRadius: BorderRadius.circular(10)),
                      child: const Text(
                        '实时超分由播放器着色器处理，不修改源文件。低配显卡出现掉帧时可在设置里关闭。',
                        style: TextStyle(fontSize: 10.5, color: MikoColors.secondary, height: 1.7),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 14),
              _SettingsCard(
                icon: Icons.rule_rounded,
                title: '规则数据',
                description: '${store.rules.length} 条规则，${store.enabledRules.length} 条启用。可导出全部规则做备份，或把 JSON 分享给朋友。',
                action: OutlinedButton.icon(
                  onPressed: onExportRules,
                  icon: const Icon(Icons.archive_outlined, size: 16),
                  label: const Text('导出全部规则'),
                ),
              ),
              const SizedBox(height: 14),
              _SettingsCard(
                icon: Icons.storage_rounded,
                title: '本机规则目录',
                description: '打开 MikoMiko 的模块数据目录，可直接查看、备份规则与追剧缓存。',
                action: OutlinedButton.icon(
                  onPressed: HostBridge.openRuleFolder,
                  icon: const Icon(Icons.folder_open_rounded, size: 16),
                  label: const Text('打开目录'),
                ),
              ),
              const SizedBox(height: 14),
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(color: const Color(0xFF102B3D), borderRadius: BorderRadius.circular(18)),
                child: const Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('MIKOMIKO · FLUTTER ANIME MODULE', style: TextStyle(color: Color(0xFF9DD5F4), fontSize: 9.5, letterSpacing: 1.1, fontWeight: FontWeight.w700)),
                    SizedBox(height: 12),
                    Text('Flutter 3.35.3 · Dart 3.9.2 · CanvasKit', style: TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.w600)),
                    SizedBox(height: 5),
                    Text('规则格式参考 KazumiRules 社区规范，播放由 WPF 内置播放器承接。', style: TextStyle(color: Color(0xFF7FA6BE), fontSize: 10.5, height: 1.7)),
                  ],
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _SettingsCard extends StatelessWidget {
  const _SettingsCard({required this.icon, required this.title, required this.description, required this.action});
  final IconData icon;
  final String title;
  final String description;
  final Widget action;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18), border: Border.all(color: MikoColors.border)),
      child: Row(
        children: [
          Icon(icon, color: MikoColors.primary, size: 22),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
                const SizedBox(height: 4),
                Text(description, style: const TextStyle(fontSize: 10.5, height: 1.7, color: MikoColors.tertiary)),
              ],
            ),
          ),
          const SizedBox(width: 14),
          action,
        ],
      ),
    );
  }
}
