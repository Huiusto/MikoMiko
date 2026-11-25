import 'package:flutter/material.dart';

import '../models/anime_rule.dart';
import '../services/miko_api.dart';
import 'theme.dart';

class MikoPoster extends StatelessWidget {
  const MikoPoster({
    super.key,
    required this.url,
    required this.title,
    this.referer = '',
    this.fit = BoxFit.cover,
  });

  final String url;
  final String title;
  final String referer;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    if (url.isEmpty) return PosterFallback(title: title);
    return Image.network(
      MikoApi.instance.proxied(url, referer: referer, asImage: true),
      fit: fit,
      gaplessPlayback: true,
      errorBuilder: (_, __, ___) => PosterFallback(title: title),
      loadingBuilder: (context, child, progress) {
        if (progress == null) return child;
        return const ColoredBox(
          color: Color(0xFFEAF5FC),
          child: Center(
            child: SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: MikoColors.primary,
              ),
            ),
          ),
        );
      },
    );
  }
}

class PosterFallback extends StatelessWidget {
  const PosterFallback({super.key, required this.title});
  final String title;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFFE9F6FE), Color(0xFFCDE9F8)],
        ),
      ),
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Text(
            title,
            textAlign: TextAlign.center,
            maxLines: 3,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: MikoColors.primaryDeep,
              fontSize: 13,
              fontWeight: FontWeight.w600,
              height: 1.6,
            ),
          ),
        ),
      ),
    );
  }
}

class AnimePosterCard extends StatefulWidget {
  const AnimePosterCard({
    super.key,
    required this.card,
    required this.onOpen,
    this.onWatch,
    this.watching = false,
    this.trailing,
    this.showSource = true,
    this.showHeart = true,
    this.badgeText = '',
    this.radius = 14,
  });

  final AnimeCard card;
  final VoidCallback onOpen;
  final VoidCallback? onWatch;
  final bool watching;
  final Widget? trailing;
  final bool showSource;
  final bool showHeart;
  final String badgeText;
  final double radius;

  @override
  State<AnimePosterCard> createState() => _AnimePosterCardState();
}

class _AnimePosterCardState extends State<AnimePosterCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final card = widget.card;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onOpen,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 160),
          curve: Curves.easeOut,
          transform: Matrix4.translationValues(0, _hover ? -3 : 0, 0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 160),
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(widget.radius),
                    color: const Color(0xFFE8F3FB),
                    boxShadow: [
                      BoxShadow(
                        color: MikoColors.primaryDeep.withValues(
                          alpha: _hover ? 0.20 : 0.08,
                        ),
                        blurRadius: _hover ? 22 : 12,
                        offset: Offset(0, _hover ? 9 : 4),
                      ),
                    ],
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      MikoPoster(
                        url: card.poster,
                        title: card.title,
                        referer: card.rule?.baseURL ?? '',
                      ),
                      Positioned(
                        left: 0,
                        right: 0,
                        bottom: 0,
                        height: 64,
                        child: DecoratedBox(
                          decoration: BoxDecoration(
                            gradient: LinearGradient(
                              begin: Alignment.topCenter,
                              end: Alignment.bottomCenter,
                              colors: [
                                Colors.transparent,
                                const Color(0xFF03121C).withValues(alpha: 0.80),
                              ],
                            ),
                          ),
                        ),
                      ),
                      if (card.note.isNotEmpty)
                        Positioned(
                          left: 9,
                          right: 9,
                          bottom: 8,
                          child: Text(
                            card.note,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: Color(0xEFFFFFFF),
                              fontSize: 10,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                      if (widget.showSource)
                        Positioned(
                          left: 8,
                          top: 8,
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 7,
                              vertical: 3,
                            ),
                            decoration: BoxDecoration(
                              color: const Color(
                                0xFF06151F,
                              ).withValues(alpha: 0.72),
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: Text(
                              card.source,
                              style: const TextStyle(
                                color: Color(0xF2FFFFFF),
                                fontSize: 9.5,
                              ),
                            ),
                          ),
                        ),
                      if (widget.onWatch != null && widget.showHeart)
                        Positioned(
                          right: 6,
                          top: 6,
                          child: _HeartButton(
                            active: widget.watching,
                            onTap: widget.onWatch!,
                          ),
                        ),
                      if (widget.badgeText.isNotEmpty)
                        Positioned(
                          right: 7,
                          bottom: 7,
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 7,
                              vertical: 3,
                            ),
                            decoration: BoxDecoration(
                              color: const Color(
                                0xFF1B2430,
                              ).withValues(alpha: 0.80),
                              borderRadius: BorderRadius.circular(5),
                            ),
                            child: Text(
                              widget.badgeText,
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: 11,
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                        ),
                      if (widget.trailing != null)
                        Positioned(
                          right: 8,
                          bottom: 8,
                          child: widget.trailing!,
                        ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 9),
              Text(
                card.title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: MikoColors.ink,
                  fontSize: 13.5,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 3),
              Text(
                card.note.isEmpty
                    ? card.source
                    : '${card.source} · ${card.note}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: MikoColors.tertiary,
                  fontSize: 10.5,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _HeartButton extends StatelessWidget {
  const _HeartButton({required this.active, required this.onTap});
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 28,
        height: 28,
        decoration: BoxDecoration(
          color: const Color(
            0xFF06151F,
          ).withValues(alpha: active ? 0.82 : 0.46),
          borderRadius: BorderRadius.circular(14),
        ),
        child: Icon(
          active ? Icons.favorite_rounded : Icons.favorite_border_rounded,
          size: 15,
          color: active ? const Color(0xFFFF8FB1) : const Color(0xEFFFFFFF),
        ),
      ),
    );
  }
}

class SectionHeading extends StatelessWidget {
  const SectionHeading({
    super.key,
    required this.title,
    this.subtitle = '',
    this.action,
  });
  final String title;
  final String subtitle;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Container(
          width: 4,
          height: 18,
          margin: const EdgeInsets.only(right: 9, bottom: 2),
          decoration: BoxDecoration(
            color: MikoColors.primary,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        Text(
          title,
          style: const TextStyle(
            fontSize: 17,
            fontWeight: FontWeight.w700,
            color: MikoColors.ink,
          ),
        ),
        if (subtitle.isNotEmpty) ...[
          const SizedBox(width: 10),
          Padding(
            padding: const EdgeInsets.only(bottom: 1),
            child: Text(
              subtitle,
              style: const TextStyle(fontSize: 11, color: MikoColors.tertiary),
            ),
          ),
        ],
        const Spacer(),
        if (action != null) action!,
      ],
    );
  }
}

class EmptyPanel extends StatelessWidget {
  const EmptyPanel({
    super.key,
    required this.title,
    required this.message,
    this.icon = Icons.travel_explore_rounded,
    this.action,
  });

  final String title;
  final String message;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 42),
      decoration: BoxDecoration(
        color: MikoColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: MikoColors.border),
      ),
      child: Column(
        children: [
          Container(
            width: 58,
            height: 58,
            decoration: BoxDecoration(
              color: MikoColors.primarySoft,
              borderRadius: BorderRadius.circular(18),
            ),
            child: Icon(icon, color: MikoColors.primaryDeep, size: 28),
          ),
          const SizedBox(height: 16),
          Text(
            title,
            style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 7),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 12,
              height: 1.7,
              color: MikoColors.secondary,
            ),
          ),
          if (action != null) ...[const SizedBox(height: 18), action!],
        ],
      ),
    );
  }
}

class TagChip extends StatelessWidget {
  const TagChip({
    super.key,
    required this.text,
    this.color = MikoColors.primarySoft,
    this.foreground = MikoColors.primaryDeep,
  });
  final String text;
  final Color color;
  final Color foreground;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        text,
        style: TextStyle(
          color: foreground,
          fontSize: 10.5,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
