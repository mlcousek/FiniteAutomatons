using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FiniteAutomatons.Core.Models.DoMain;

namespace FiniteAutomatons.GUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Automaton? _automaton;
    private int _stateCounter = 0;
    private Dictionary<int, Ellipse> _stateVisuals = new();
    private List<Line> _transitionVisuals = new();

    public MainWindow()
    {
        InitializeComponent();
        SwitchToDfa();
    }

    private void SwitchToDfa()
    {
        _automaton = new DFA();
        Redraw();
    }

    private void SwitchToNfa()
    {
        _automaton = new NFA();
        Redraw();
    }

    private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_automaton == null) return;
        var pos = e.GetPosition(MainCanvas);
        var state = new State { Id = _stateCounter++, IsStart = _automaton.States.Count == 0, IsAccepting = false };
        _automaton.AddState(state);
        DrawState(state, pos);
    }

    private void DrawState(State state, Point pos)
    {
        var ellipse = new Ellipse
        {
            Width = 40,
            Height = 40,
            Stroke = Brushes.Black,
            StrokeThickness = 2,
            Fill = state.IsStart ? Brushes.LightGreen : Brushes.White
        };
        Canvas.SetLeft(ellipse, pos.X - 20);
        Canvas.SetTop(ellipse, pos.Y - 20);
        MainCanvas.Children.Add(ellipse);
        _stateVisuals[state.Id] = ellipse;
    }

    private void Redraw()
    {
        MainCanvas.Children.Clear();
        _stateVisuals.Clear();
        _transitionVisuals.Clear();
        // TODO: Redraw all states and transitions
    }
}