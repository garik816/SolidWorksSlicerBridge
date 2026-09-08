using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SolidWorksSlicerBridge
{
    internal sealed class SettingsForm : Form
    {
        private TextBox orca;
        private TextBox bambu;
        private TextBox prusa;

        public SettingsForm()
        {
            Text = "SolidWorks Slicer Bridge";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(680, 205);

            BuildRow("OrcaSlicer", 20, SlicerKind.Orca, out orca);
            BuildRow("Bambu Studio", 70, SlicerKind.Bambu, out bambu);
            BuildRow("PrusaSlicer", 120, SlicerKind.Prusa, out prusa);

            Button save = new Button();
            save.Text = "Сохранить";
            save.Size = new Size(100, 30);
            save.Location = new Point(455, 165);
            save.Click += delegate
            {
                SlicerPaths.Save(SlicerKind.Orca, orca.Text.Trim());
                SlicerPaths.Save(SlicerKind.Bambu, bambu.Text.Trim());
                SlicerPaths.Save(SlicerKind.Prusa, prusa.Text.Trim());
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(save);

            Button cancel = new Button();
            cancel.Text = "Отмена";
            cancel.Size = new Size(100, 30);
            cancel.Location = new Point(565, 165);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = save;
            CancelButton = cancel;
        }

        private void BuildRow(string title, int y, SlicerKind kind, out TextBox box)
        {
            Label label = new Label();
            label.Text = title;
            label.AutoSize = true;
            label.Location = new Point(15, y + 6);
            Controls.Add(label);

            box = new TextBox();
            box.Location = new Point(115, y);
            box.Size = new Size(455, 25);
            box.Text = SlicerPaths.Resolve(kind, false);
            Controls.Add(box);

            Button browse = new Button();
            browse.Text = "...";
            browse.Size = new Size(80, 26);
            browse.Location = new Point(580, y - 1);
            TextBox target = box;
            browse.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Укажи " + title + ".exe";
                    dialog.Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*";
                    dialog.CheckFileExists = true;
                    if (File.Exists(target.Text))
                        dialog.InitialDirectory = Path.GetDirectoryName(target.Text);
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        target.Text = dialog.FileName;
                }
            };
            Controls.Add(browse);
        }
    }
}
