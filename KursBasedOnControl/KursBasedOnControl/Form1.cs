using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastColoredTextBoxNS;
using System.IO;

namespace KursBasedOnControl
{
    public partial class Form1 : Form
    {
        FastColoredTextBox tb = new FastColoredTextBox();
        string filename;
        public Form1()
        {
            InitializeComponent();
            
            tb.Parent = this;
            tb.Language = Language.C;
            tb.Left = 0;
            tb.Top = this.menuStrip1.Height + 2;
            tb.Width = this.Width;
            tb.Height = this.Height - this.menuStrip1.Height - 2;
            tb.Enabled = false;
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tb.Enabled = true;
            this.Text = string.Format("{0} - {1}", "Noname.c", Application.ProductName);
            filename = "Noname.c";
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                tb.OpenFile(openFileDialog1.FileName);
                filename = openFileDialog1.FileName;
                this.Text = string.Format("{0}-{1}", filename, Application.ProductName);
                tb.Enabled = true;
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filename = saveFileDialog1.FileName;
                this.Text = string.Format("{0} - {1}", filename, Application.ProductName);
                tb.SaveToFile(filename, Encoding.Default);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (filename == "Noname.c")
            {
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    filename = saveFileDialog1.FileName;
                    this.Text = string.Format("{0} - {1}", filename, Application.ProductName);
                    tb.SaveToFile(filename, Encoding.Default);
                    return;
                }
            }
            else
            {
                File.Delete(string.Format(filename));
                tb.SaveToFile(filename, Encoding.Default);
            }
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tb.Undo();
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tb.Redo();
        }

        private void searchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FindForm find = new FindForm(tb);
            find.Show();
        }
    }
}
