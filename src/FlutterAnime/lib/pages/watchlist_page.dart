import 'package:flutter/material.dart';

import '../services/rule_store.dart';
import '../ui/theme.dart';
import '../ui/widgets.dart';
import 'detail_page.dart';

class WatchlistPage extends StatelessWidget {
  const WatchlistPage({super.key});

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: RuleStore.instance,
      builder: (context, _) {
        final list = RuleStore.instance.watchlist;
        return SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(26, 24, 26, 36),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const SectionHeading(title: '我的追剧', subtitle: '保存在本机 Flutter 模块中'),
              const SizedBox(height: 16),
              if (list.isEmpty)
                const EmptyPanel(
                  title: '还没有追剧',
                  message: '在作品详情页点击「追剧」，收藏的作品会出现在这里。',
                  icon: Icons.favorite_border_rounded,
                )
              else
                GridView.builder(
                  shrinkWrap: true,
                  physics: const NeverScrollableScrollPhysics(),
                  itemCount: list.length,
                  gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                    maxCrossAxisExtent: 168,
                    mainAxisSpacing: 20,
                    crossAxisSpacing: 16,
                    childAspectRatio: 0.56,
                  ),
                  itemBuilder: (context, index) {
                    final card = list[index];
                    return AnimePosterCard(
                      card: card,
                      onOpen: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => AnimeDetailPage(card: card))),
                      watching: true,
                      onWatch: () => RuleStore.instance.toggleWatch(card),
                    );
                  },
                ),
              const SizedBox(height: 28),
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(16), border: Border.all(color: MikoColors.border)),
                child: const Row(
                  children: [
                    Icon(Icons.info_outline_rounded, size: 18, color: MikoColors.primary),
                    SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        '追剧记录只保存在本机浏览器存储（localStorage），不会上传服务器。重新安装或清理缓存会清空记录。',
                        style: TextStyle(fontSize: 10.5, color: MikoColors.secondary, height: 1.7),
                      ),
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
}
