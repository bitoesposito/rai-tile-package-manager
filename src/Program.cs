using System;
using System.Windows.Forms;

namespace RaiTilePackageManager
{
    static class Program
    {
        /// <summary>Packages and project folders dropped on the .exe arrive as arguments.</summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) =>
                MessageBox.Show(e.Exception.Message, "Rai - Package tile manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm(args));
        }
    }
}
