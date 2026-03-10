using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using SignalWeave.Desktop.ViewModels;

namespace SignalWeave.Desktop.Views;

public partial class WeightDisplayWindow : Window
{
    private WeightDisplayWindowViewModel? _attachedViewModel;

    public WeightDisplayWindow()
        : this(new WeightDisplaySession(
            "Weights",
            () => new SignalWeave.Core.WeightSet(
                new double[,] { { -0.8, -0.7 }, { 0.9, 0.2 }, { -0.1, 0.6 } },
                new double[,] { { 0.8 }, { -0.4 }, { 0.1 } })))
    {
    }

    public WeightDisplayWindow(WeightDisplaySession session)
    {
        InitializeComponent();
        DataContextChanged += HandleDataContextChanged;
        DataContext = new WeightDisplayWindowViewModel(session);
        AttachViewModel(DataContext as WeightDisplayWindowViewModel);
    }

    private void Dismiss_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        AttachViewModel(DataContext as WeightDisplayWindowViewModel);
    }

    private void AttachViewModel(WeightDisplayWindowViewModel? viewModel)
    {
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.WeightGlyphs.CollectionChanged -= HandleGlyphsChanged;
        }

        _attachedViewModel = viewModel;

        if (_attachedViewModel is not null)
        {
            _attachedViewModel.WeightGlyphs.CollectionChanged += HandleGlyphsChanged;
        }

        RenderGlyphs();
    }

    private void HandleGlyphsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderGlyphs();
    }

    private void RenderGlyphs()
    {
        if (WeightGlyphCanvas is null)
        {
            return;
        }

        WeightGlyphCanvas.Children.Clear();

        if (_attachedViewModel is null)
        {
            return;
        }

        foreach (var glyph in _attachedViewModel.WeightGlyphs)
        {
            var cell = new Border
            {
                Width = glyph.CellWidth,
                Height = glyph.CellHeight,
                Background = Brush.Parse(glyph.CellFill)
            };
            Canvas.SetLeft(cell, glyph.X);
            Canvas.SetTop(cell, glyph.Y);
            WeightGlyphCanvas.Children.Add(cell);

            var ellipse = new Ellipse
            {
                Width = glyph.EllipseWidth,
                Height = glyph.EllipseHeight,
                Fill = Brush.Parse(glyph.Fill)
            };
            ToolTip.SetTip(ellipse, new TextBlock { Text = glyph.Tooltip });
            Canvas.SetLeft(ellipse, glyph.EllipseX);
            Canvas.SetTop(ellipse, glyph.EllipseY);
            WeightGlyphCanvas.Children.Add(ellipse);
        }
    }
}
