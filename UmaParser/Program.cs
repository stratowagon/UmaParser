using System.Windows.Forms;
using UmaBlobber.DataModel.ResponseData;
using UmaBlobber.MasterData;
using UmaBlobber.Ui;

namespace UmaBlobber
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Must be set before any UI is created (see View menu to change; saved in settings.json).
            AppColorMode.ApplyStartupPreference();

            // Standard initialization
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            UmaApiResponse.RegisterAllKnownTypes();
            GameMasterService.Current.Initialize();

            Application.Run(new Form1());
        }
    }
}