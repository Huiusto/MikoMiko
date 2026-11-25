import 'package:flutter/material.dart';

import '../models/anime_rule.dart';
import '../services/rule_store.dart';
import '../ui/theme.dart';
import '../ui/widgets.dart';
import 'detail_page.dart';

class SearchPage extends StatelessWidget {
  const SearchPage({
    super.key,
    required this.keyword,
    required this.cards,
    required this.searching,
    required this.errors,
    required this.onRetry,
  });

  final String keyword;
  final List<AnimeCard> cards;
  final bool searching;
  final List<String> errors;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final groups = <String, List<AnimeCard>>{};
    for (final card in cards) {
      groups.putIfAbsent(card.source, () => <AnimeCard>[]).add(card);
    }
    final enabled = RuleStore.instance.enabledRules.length;

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(26, 22, 26, 36),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(color: MikoColors.primarySoft, borderRadius: BorderRadius.circular(12)),
                child: const Icon(Icons.search_rounded, color: MikoColors.primaryDeep, size: 21),
              ),
              const SizedBox(width: 12),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('“$keyword” 的搜索结果', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                  const SizedBox(height: 4),
                  Text(
                    searching ? '正在并行请求 $enabled 条已启用规则…' : '共 ${cards.length} 部作品 · ${groups.length} 个站点命中',
                    style: const TextStyle(fontSize: 10.5, color: MikoColors.tertiary),
                  ),
                ],
              ),
              const Spacer(),
              if (!searching)
                OutlinedButton.icon(
                  onPressed: onRetry,
                  icon: const Icon(Icons.refresh_rounded, size: 16),
                  label: const Text('重新搜索'),
                ),
            ],
          ),
          const SizedBox(height: 20),
          if (searching)
            Container(
              height: 240,
              decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(18), border: Border.all(color: MikoColors.border)),
              child: const Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircularProgressIndicator(color: MikoColors.primary),
                    SizedBox(height: 16),
                    Text('Flutter 正在解析各站点页面…', style: TextStyle(fontSize: 11.5, color: MikoColors.secondary)),
                  ],
                ),
              ),
            )
          else if (cards.isEmpty)
            EmptyPanel(
              title: '没有找到匹配作品',
              message: errors.isEmpty
                  ? '换一个关键词试试，或在规则库中启用更多站点。'
                  : '所有站点都没有返回结果。${errors.first}',
              icon: Icons.search_off_rounded,
              action: FilledButton(onPressed: onRetry, child: const Text('重新搜索')),
            )
          else
            for (final entry in groups.entries) ...[
              Row(
                children: [
                  TagChip(text: entry.key),
                  const SizedBox(width: 9),
                  Text('${entry.value.length} 部', style: const TextStyle(fontSize: 10, color: MikoColors.tertiary)),
                  const SizedBox(width: 12),
                  Expanded(child: Container(height: 1, color: MikoColors.border)),
                ],
              ),
              const SizedBox(height: 14),
              GridView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: entry.value.length,
                gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                  maxCrossAxisExtent: 168,
                  mainAxisSpacing: 20,
                  crossAxisSpacing: 16,
                  childAspectRatio: 0.56,
                ),
                itemBuilder: (context, index) {
                  final card = entry.value[index];
                  return AnimePosterCard(
                    card: card,
                    onOpen: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => AnimeDetailPage(card: card))),
                    watching: RuleStore.instance.isWatching(card.url),
                    onWatch: () => RuleStore.instance.toggleWatch(card),
                  );
                },
              ),
              const SizedBox(height: 26),
            ],
          if (errors.isNotEmpty && cards.isNotEmpty)
            Container(
              margin: const EdgeInsets.only(top: 8),
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(color: const Color(0xFFF9FBFC), borderRadius: BorderRadius.circular(12), border: Border.all(color: MikoColors.border)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      Icon(Icons.warning_amber_rounded, size: 16, color: MikoColors.warning),
                      SizedBox(width: 8),
                      Text('部分规则请求失败', style: TextStyle(fontSize: 11.5, fontWeight: FontWeight.w600)),
                    ],
                  ),
                  const SizedBox(height: 8),
                  for (final error in errors.take(4))
                    Text('· $error', style: const TextStyle(fontSize: 10, color: MikoColors.tertiary, height: 1.7)),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
