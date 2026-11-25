import 'package:flutter/material.dart';

class MikoColors {
  static const primary = Color(0xFF4EA4DD);
  static const primaryDeep = Color(0xFF2F86B9);
  static const primarySoft = Color(0xFFDFF2FE);
  static const background = Color(0xFFF5FAFD);
  static const surface = Color(0xFFFFFFFF);
  static const ink = Color(0xFF173A50);
  static const secondary = Color(0xFF617687);
  static const tertiary = Color(0xFF8CA1B0);
  static const border = Color(0xFFE6F0F7);
  static const borderStrong = Color(0xFFD3E5F1);
  static const success = Color(0xFF3BAE8A);
  static const warning = Color(0xFFE6A23C);
}

ThemeData buildMikoTheme() {
  final base = ThemeData(
    useMaterial3: true,
    brightness: Brightness.light,
    fontFamily: 'MikoSans',
    scaffoldBackgroundColor: MikoColors.background,
    colorScheme: ColorScheme.fromSeed(
      seedColor: MikoColors.primary,
      brightness: Brightness.light,
      surface: MikoColors.surface,
      primary: MikoColors.primary,
    ),
  );
  return base.copyWith(
    textTheme: base.textTheme.apply(
      fontFamily: 'MikoSans',
      bodyColor: MikoColors.ink,
      displayColor: MikoColors.ink,
    ),
    dividerTheme: const DividerThemeData(color: MikoColors.border, thickness: 1),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: const Color(0xFFF7FAFC),
      hintStyle: const TextStyle(color: MikoColors.tertiary, fontSize: 13),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: MikoColors.border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: MikoColors.border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(12),
        borderSide: const BorderSide(color: MikoColors.primary, width: 1.4),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: MikoColors.primary,
        foregroundColor: Colors.white,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: MikoColors.primaryDeep,
        side: const BorderSide(color: MikoColors.borderStrong),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12.5),
      ),
    ),
    snackBarTheme: const SnackBarThemeData(
      backgroundColor: MikoColors.ink,
      contentTextStyle: TextStyle(color: Colors.white, fontSize: 13),
      behavior: SnackBarBehavior.floating,
    ),
    scrollbarTheme: ScrollbarThemeData(
      thumbColor: WidgetStatePropertyAll(MikoColors.primary.withValues(alpha: 0.35)),
      radius: const Radius.circular(8),
      thickness: const WidgetStatePropertyAll(7),
    ),
  );
}
