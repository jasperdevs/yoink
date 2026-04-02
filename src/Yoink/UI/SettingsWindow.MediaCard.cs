using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Point = System.Windows.Point;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using Cursors = System.Windows.Input.Cursors;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Yoink.Models;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private sealed record MediaCardShell(Border Card, Grid ImageContainer, StackPanel InfoPanel, Border CopyButton, System.Windows.Controls.Image Image);

    private static bool IsDraggableFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static void AttachFileDragHandlers(Border card, FrameworkElement dragSource, string filePath, Func<bool> canDrag, Action<bool> setDragging)
    {
        Point dragStart = default;
        bool pressed = false;
        bool dragging = false;

        dragSource.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!canDrag())
                return;
            pressed = true;
            dragging = false;
            setDragging(false);
            dragStart = e.GetPosition(card);
        };

        dragSource.PreviewMouseMove += (_, e) =>
        {
            if (!pressed || dragging || e.LeftButton != MouseButtonState.Pressed || !canDrag())
                return;

            var current = e.GetPosition(card);
            if (Math.Abs(current.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            dragging = true;
            pressed = false;
            setDragging(true);
            var data = new DataObject(DataFormats.FileDrop, new[] { filePath });
            DragDrop.DoDragDrop(card, data, DragDropEffects.Copy);
            setDragging(false);
        };

        dragSource.PreviewMouseLeftButtonUp += (_, _) =>
        {
            pressed = false;
            dragging = false;
            setDragging(false);
        };
    }

    private MediaCardShell BuildMediaCardShell(HistoryItemVM vm, Action copyAction)
    {
        bool isDraggingFile = false;
        var img = new System.Windows.Controls.Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

        img.Loaded += (_, _) =>
        {
            LoadThumbAsync(img, vm.ThumbPath);
            img.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
        };

        var copyBtn = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 6, 0),
            Cursor = Cursors.Hand,
            Opacity = 0,
            IsHitTestVisible = true,
            ToolTip = "Copy to clipboard",
            Child = new System.Windows.Shapes.Path
            {
                Data = System.Windows.Media.Geometry.Parse("M16,1H4C2.9,1,2,1.9,2,3v10h2V3h12V1z M19,5H8C6.9,5,6,5.9,6,7v10c0,1.1,0.9,2,2,2h11c1.1,0,2-0.9,2-2V7C21,5.9,20.1,5,19,5z M19,17H8V7h11V17z"),
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                Width = 13,
                Height = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };
        copyBtn.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            copyAction();
        };

        Border? fileLocationBtn = null;
        if (IsDraggableFile(vm.Entry.FilePath))
            fileLocationBtn = CreateFileLocationButton(vm.Entry.FilePath);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var imgContainer = new Grid();
        imgContainer.Children.Add(img);
        if (fileLocationBtn != null)
            imgContainer.Children.Add(fileLocationBtn);
        imgContainer.Children.Add(copyBtn);
        Grid.SetRow(imgContainer, 0);
        root.Children.Add(imgContainer);

        var info = new StackPanel { Margin = new Thickness(10, 6, 10, 8) };
        Grid.SetRow(info, 1);
        root.Children.Add(info);

        var card = new Border
        {
            Width = 168,
            Margin = new Thickness(3),
            CornerRadius = new CornerRadius(8),
            Background = Theme.Brush(Theme.BgCard),
            BorderBrush = Theme.Brush(Theme.BorderSubtle),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = root,
            Tag = vm,
            RenderTransform = new ScaleTransform(1, 1),
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
        };

        if (IsDraggableFile(vm.Entry.FilePath))
            AttachFileDragHandlers(card, card, vm.Entry.FilePath, () => !_selectMode, v => isDraggingFile = v);

        card.SizeChanged += (s, _) =>
        {
            var b = (Border)s!;
            b.Clip = new System.Windows.Media.RectangleGeometry(
                new System.Windows.Rect(0, 0, b.ActualWidth, b.ActualHeight), 10, 10);
        };

        card.MouseEnter += (s, _) =>
        {
            var b = (Border)s!;
            b.BorderBrush = Theme.Brush(Theme.Border);
            var st = (ScaleTransform)b.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.03, TimeSpan.FromMilliseconds(120)));
            st.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.03, TimeSpan.FromMilliseconds(120)));
            copyBtn.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            if (fileLocationBtn != null)
                fileLocationBtn.BeginAnimation(OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        };
        card.MouseLeave += (s, _) =>
        {
            var b = (Border)s!;
            if (vm.IsSelected)
                b.BorderBrush = Theme.StrokeBrush();
            else
                b.BorderBrush = Theme.Brush(Theme.BorderSubtle);
            var st = (ScaleTransform)b.RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            st.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            copyBtn.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
            if (fileLocationBtn != null)
                fileLocationBtn.BeginAnimation(OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
        };

        card.MouseLeftButtonUp += (s, e) =>
        {
            if (_selectMode)
            {
                vm.IsSelected = !vm.IsSelected;
                UpdateCardSelection(card, vm);
                return;
            }

            if (isDraggingFile)
                return;

            if (!string.IsNullOrEmpty(vm.Entry.UploadUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = vm.Entry.UploadUrl,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    if (File.Exists(vm.Entry.FilePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = vm.Entry.FilePath,
                            UseShellExecute = true
                        });
                    }
                }
            }
            else if (File.Exists(vm.Entry.FilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = vm.Entry.FilePath,
                    UseShellExecute = true
                });
            }
        };

        card.MouseRightButtonDown += (s, e) =>
        {
            if (!_selectMode)
            {
                _selectMode = true;
                SelectBtn.Content = "Done";
                DeleteSelectedBtn.Visibility = Visibility.Visible;
            }
            vm.IsSelected = !vm.IsSelected;
            UpdateCardSelection(card, vm);
        };

        return new MediaCardShell(card, imgContainer, info, copyBtn, img);
    }

    private Border CreateFileLocationButton(string filePath)
    {
        var btn = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6, 6, 0, 0),
            Cursor = Cursors.Hand,
            Opacity = 0,
            IsHitTestVisible = true,
            ToolTip = "Show in folder",
            Child = new TextBlock
            {
                Text = "\uE838",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = Theme.Brush(Theme.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
        };
        btn.MouseLeftButtonDown += (s, e) =>
        {
            e.Handled = true;
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
        };
        btn.MouseEnter += (s, _) => ((Border)s!).BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(100)));
        btn.MouseLeave += (s, _) => ((Border)s!).BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(100)));
        return btn;
    }
}
