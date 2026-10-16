using System;

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SourceGit.Views
{
    /// <summary>
    ///     铺满可用空间的图片控件：滚轮以鼠标为中心缩放，按住拖动平移，双击恢复完整显示。
    /// </summary>
    public class ZoomableImageView : ImageContainer
    {
        public static readonly DirectProperty<ZoomableImageView, Bitmap> ImageProperty =
            AvaloniaProperty.RegisterDirect<ZoomableImageView, Bitmap>(
                nameof(Image),
                static o => o.Image,
                static (o, v) => o.Image = v);

        public Bitmap Image
        {
            get => _image;
            set => SetAndRaise(ImageProperty, ref _image, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_image == null)
                return;

            if (_fitted)
                FitToView();

            var size = _image.Size;
            var dest = new Rect(_origin.X, _origin.Y, size.Width * _scale, size.Height * _scale);
            var interpolation = _scale >= PixelatedScale ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;

            using (context.PushClip(new Rect(Bounds.Size)))
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = interpolation }))
                context.DrawImage(_image, new Rect(size), dest);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ImageProperty)
            {
                _fitted = true;
                InvalidateVisual();
            }
            else if (change.Property == BoundsProperty)
            {
                if (_fitted)
                    InvalidateVisual();
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (_image == null || e.Delta.Y == 0)
                return;

            if (_fitted)
                FitToView();

            var factor = Math.Pow(ZoomStep, e.Delta.Y);
            var newScale = Math.Clamp(_scale * factor, MinScale, MaxScale);
            var ratio = newScale / _scale;
            var p = e.GetPosition(this);

            _origin = p - (p - _origin) * ratio;
            _scale = newScale;
            _fitted = false;

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_image == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (e.ClickCount == 2)
            {
                _fitted = true;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_fitted)
                FitToView();

            _dragging = true;
            _lastPointer = e.GetPosition(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (!_dragging)
                return;

            var p = e.GetPosition(this);
            _origin += p - _lastPointer;
            _lastPointer = p;
            _fitted = false;

            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!_dragging)
                return;

            _dragging = false;
            Cursor = null;
            e.Pointer.Capture(null);
        }

        private void FitToView()
        {
            var size = _image.Size;
            var w = Bounds.Width;
            var h = Bounds.Height;

            _scale = Math.Min(1, Math.Min(w / size.Width, h / size.Height));
            _origin = new Point((w - size.Width * _scale) * 0.5, (h - size.Height * _scale) * 0.5);
            _fitted = true;
        }

        private const double ZoomStep = 1.15;
        private const double MinScale = 0.02;
        private const double MaxScale = 64;
        private const double PixelatedScale = 4;

        private Bitmap _image = null;
        private double _scale = 1;
        private Point _origin = new Point();
        private bool _fitted = true;
        private bool _dragging = false;
        private Point _lastPointer = new Point();
    }
}
