using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Socona.Fiveocks
{
    class TextBoxWriter:TextWriter
    {
        private TextBox _textBox = null;

        private StringBuilder sb = null;

        private Queue<int> queue = null;
        private int curLineLength = 0;
        public TextBoxWriter(TextBox tb) 
        {
            this._textBox = tb;
            sb = new StringBuilder();
            sb.Capacity = 2048000;
            queue= new Queue<int>(500);
            curLineLength = 0;
            var info = "> textbox logging start\r\n";
            _textBox.Text += info;
            queue.Enqueue(info.Length);
        }
        

        // nerver used.
        public override void Write(string value)
        {
            curLineLength += value.Length;
            sb.Append(value);
            _textBox.Dispatcher.Invoke(() =>
            {
                _textBox.Text = sb.ToString();
                _textBox.ScrollToEnd();
            });
          
        }

        public override void WriteLine(string value)
        {       
                string[] lines = value.Split(new[] {"\r\n"}, StringSplitOptions.None);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (i == 0)
                    {
                        queue.Enqueue(lines[i].Length + curLineLength);
                        curLineLength = 0;
                    }
                    else
                    {
                        queue.Enqueue(lines[i].Length);
                    }
                }

            _textBox.Dispatcher.Invoke(() =>
            {
                if (queue.Count > 400)
                {

                    var len = queue.Dequeue() + 4;
                    
                    _textBox.Text = _textBox.Text.Remove(0, len);
                }
                _textBox.Text +="> " +value+"\r\n";
               
                    _textBox.ScrollToEnd();              
            });
            curLineLength = 0;
          
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
