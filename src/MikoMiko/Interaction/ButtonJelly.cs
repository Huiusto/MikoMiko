using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseEventHandler = System.Windows.Input.MouseEventHandler;

namespace MikoMiko.Interaction
{
    /// <summary>
    /// 全局按钮果冻动效：按下轻微压缩，松开回弹过冲，鼠标悬停有轻微呼吸感。
    /// 通过类级路由事件统一挂到所有 Button，无需逐个页面改样式。
    /// </summary>
    internal static class ButtonJelly
    {
        private static bool _installed;
        private static readonly Duration PressDuration = new Duration(TimeSpan.FromMilliseconds(90));
        private static readonly Duration ReleaseDuration = new Duration(TimeSpan.FromMilliseconds(430));

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnMouseDown));
            EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewMouseLeftButtonUpEvent,
                new MouseButtonEventHandler(OnMouseUp));
            EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent,
                new MouseEventHandler(OnMouseEnter));
            EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseLeaveEvent,
                new MouseEventHandler(OnMouseLeave));
            EventManager.RegisterClassHandler(typeof(Button), ButtonBase.ClickEvent,
                new RoutedEventHandler(OnClick));
        }

        private static ScaleTransform? EnsureScale(Button button)
        {
            if (button.RenderTransform is ScaleTransform scale) return scale;

            // 已经使用其它变换的按钮不强行覆盖，避免影响页面原有动画。
            if (button.RenderTransform is not null && button.RenderTransform is not TransformGroup) return null;

            scale = new ScaleTransform(1, 1);
            button.RenderTransform = scale;
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            return scale;
        }

        private static void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button || !button.IsEnabled) return;
            var scale = EnsureScale(button);
            if (scale is null) return;

            Animate(scale, 0.9, 0.9, PressDuration, new QuadraticEase { EasingMode = EasingMode.EaseOut });
        }

        private static void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button || !button.IsEnabled) return;
            Jelly(button);
        }

        private static void OnClick(object sender, RoutedEventArgs e)
        {
            // 键盘（空格/回车）触发时没有 MouseUp，用 Click 兜底回弹。
            if (sender is Button button && Mouse.LeftButton != MouseButtonState.Pressed)
                Jelly(button);
        }

        private static void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not Button button || !button.IsEnabled) return;
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;
            var scale = EnsureScale(button);
            if (scale is null) return;
            Animate(scale, 1.035, 1.035, new Duration(TimeSpan.FromMilliseconds(180)),
                new QuadraticEase { EasingMode = EasingMode.EaseOut });
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not Button button || !button.IsEnabled) return;
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;
            Jelly(button);
        }

        private static void Jelly(Button button)
        {
            var scale = EnsureScale(button);
            if (scale is null) return;

            var ease = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 2, Springiness = 5 };
            var x = new DoubleAnimationUsingKeyFrames { Duration = ReleaseDuration };
            x.KeyFrames.Add(new EasingDoubleKeyFrame(1.10, KeyTime.FromPercent(0.28), ease));
            x.KeyFrames.Add(new EasingDoubleKeyFrame(0.96, KeyTime.FromPercent(0.58), ease));
            x.KeyFrames.Add(new EasingDoubleKeyFrame(1.02, KeyTime.FromPercent(0.82), ease));
            x.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0), ease));

            var y = (DoubleAnimationUsingKeyFrames)x.Clone();
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, x, HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, y, HandoffBehavior.SnapshotAndReplace);
        }

        private static void Animate(ScaleTransform scale, double value, double valueY, Duration duration, IEasingFunction easing)
        {
            var x = new DoubleAnimation(value, duration) { EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
            var y = new DoubleAnimation(valueY, duration) { EasingFunction = easing, FillBehavior = FillBehavior.HoldEnd };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, x, HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, y, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
