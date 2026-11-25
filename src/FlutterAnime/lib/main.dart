import 'package:flutter/material.dart';

import 'pages/home_page.dart';
import 'ui/theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MikoAnimeApp());
}

class MikoAnimeApp extends StatelessWidget {
  const MikoAnimeApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'MikoMiko · 追剧影视',
      debugShowCheckedModeBanner: false,
      theme: buildMikoTheme(),
      home: const AnimeShell(),
    );
  }
}

class AnimeShell extends StatelessWidget {
  const AnimeShell({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: MikoColors.background,
      body: Column(
        children: [
          Container(
            height: 58,
            decoration: const BoxDecoration(
              color: Colors.white,
              border: Border(bottom: BorderSide(color: Color(0xFFEDEFF3))),
            ),
            padding: const EdgeInsets.symmetric(horizontal: 24),
            child: Row(
              children: [
                Container(
                  width: 34,
                  height: 34,
                  decoration: BoxDecoration(
                    color: MikoColors.primarySoft,
                    borderRadius: BorderRadius.circular(9),
                  ),
                  child: const Icon(
                    Icons.live_tv_rounded,
                    size: 18,
                    color: MikoColors.primaryDeep,
                  ),
                ),
                const SizedBox(width: 11),
                const Text(
                  '追剧 / 影视',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                    color: MikoColors.ink,
                  ),
                ),
                const SizedBox(width: 10),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: const Color(0xFFFDEEF3),
                    borderRadius: BorderRadius.circular(5),
                  ),
                  child: const Text(
                    '哔哩哔哩',
                    style: TextStyle(
                      fontSize: 10,
                      color: Color(0xFFD45A83),
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
                const Spacer(),
                const Icon(
                  Icons.verified_rounded,
                  size: 14,
                  color: MikoColors.primary,
                ),
                const SizedBox(width: 5),
                const Text(
                  '官方片单 · 弹幕与评论同步',
                  style: TextStyle(
                    fontSize: 11.5,
                    color: MikoColors.tertiary,
                  ),
                ),
              ],
            ),
          ),
          const Expanded(child: HomePage()),
        ],
      ),
    );
  }
}
