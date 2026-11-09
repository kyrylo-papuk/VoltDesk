using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoltDesk.Desktop.ViewModels;

namespace VoltDesk.Desktop.Views;

/// <summary>Drag and drop is view mechanics; the view model decides what a drop means.</summary>
public partial class TaskBoardView : UserControl
{
    private Point _dragStart;

    public TaskBoardView() => InitializeComponent();

    private TaskBoardViewModel ViewModel => (TaskBoardViewModel)DataContext;

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        if (sender is FrameworkElement { DataContext: TaskCard card })
        {
            ViewModel.SelectCommand.Execute(card);
        }
    }

    private void OnCardMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { DataContext: TaskCard card } element)
        {
            return;
        }

        var moved = e.GetPosition(this) - _dragStart;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        DragDrop.DoDragDrop(element, new DataObject(typeof(TaskCard), card), DragDropEffects.Move);
    }

    private void OnColumnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TaskCard)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnColumnDrop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BoardColumn column } && e.Data.GetData(typeof(TaskCard)) is TaskCard card)
        {
            await ViewModel.MoveAsync(card, column.Status);
        }
    }
}
