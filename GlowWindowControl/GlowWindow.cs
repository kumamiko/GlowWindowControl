using GlowWindowControl.Glow;
using System;
using System.Windows;
using System.Windows.Media;

namespace GlowWindowControl
{
    public class GlowWindow : Window
    {
        private GlowDecorator _glowDecorator;
        public GlowDecorator Glow => _glowDecorator;
        public SolidColorBrush ActiveBrush
        {
            get { return (SolidColorBrush)GetValue(ActiveBrushProperty); }
            set { SetValue(ActiveBrushProperty, value); }
        }

        public static readonly DependencyProperty ActiveBrushProperty =
            DependencyProperty.Register("ActiveBrush", typeof(SolidColorBrush), typeof(GlowWindow), new PropertyMetadata(Brushes.Transparent, new PropertyChangedCallback(OnActiveBrushChanged)));

        private static void OnActiveBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlowWindow window && window.Glow != null)
            {
                window.Glow.ActiveColor = (e.NewValue as SolidColorBrush).Color;
            }
        }

        public SolidColorBrush InActiveBrush
        {
            get { return (SolidColorBrush)GetValue(InActiveBrushProperty); }
            set { SetValue(InActiveBrushProperty, value); }
        }

        public static readonly DependencyProperty InActiveBrushProperty =
            DependencyProperty.Register("InActiveBrush", typeof(SolidColorBrush), typeof(GlowWindow), new PropertyMetadata(Brushes.Transparent));

        public bool UseGlow
        {
            get { return (bool)GetValue(UseGlowProperty); }
            set { SetValue(UseGlowProperty, value); }
        }

        public static readonly DependencyProperty UseGlowProperty =
            DependencyProperty.Register("UseGlow", typeof(bool), typeof(GlowWindow), new PropertyMetadata(false, new PropertyChangedCallback(OnUseGlowChanged)));

        private static void OnUseGlowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GlowWindow window && window.Glow != null)
            {
                window.Glow.Enable((bool)e.NewValue);
            }
        }

        public GlowWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GlowWindow), new FrameworkPropertyMetadata(typeof(GlowWindow)));

            Activated += (_, __) => BorderBrush = ActiveBrush;
            Deactivated += (_, __) => BorderBrush = InActiveBrush;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            ApplyGlowDecorator();
        }

        private void ApplyGlowDecorator()
        {
            _glowDecorator = new GlowDecorator();
            _glowDecorator.Attach(this);

            _glowDecorator.ActiveColor = ActiveBrush.Color;
            _glowDecorator.InactiveColor = InActiveBrush.Color;
            _glowDecorator.Activate(true);
            _glowDecorator.EnableResize(true);
        }
    }
}
