using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace _5ocks
{
    class FixBufferTextBox:TextBox
    {
        

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
           
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {

            e.Handled = true;
            var rem= e.Changes.Where(t => t.RemovedLength > 0);
            
            var newevent = e.Changes.Except(rem);
            TextChangedEventArgs tce = new TextChangedEventArgs(e.RoutedEvent, e.UndoAction, newevent.ToArray());
            base.OnTextChanged(tce);         
        }
    }
}
