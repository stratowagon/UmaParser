using System.Windows.Forms;
using UmaBlobber.DataModel.ResponseData;
using UmaBlobber.MasterData;

namespace UmaBlobber
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            UmaApiResponse.RegisterAllKnownTypes();
            GameMasterService.Current.Initialize();

            Application.Run(new Form1());
        }
    }
}