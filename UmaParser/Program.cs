using System.Windows.Forms;
using UmaParser.MasterData;

namespace UmaParser
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            GameMasterService.Current.Initialize();

            Application.Run(new Form1());
        }
    }
}