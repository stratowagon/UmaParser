using System.Windows.Forms;
using UmaBlobber.DataModel.ResponseData;

namespace UmaBlobber
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Enable system-aware color mode (respects Windows light/dark setting)
            Application.SetColorMode(SystemColorMode.Dark);

            // Standard initialization
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            UmaApiResponse.RegisterAllKnownTypes();

            Application.Run(new Form1());
        }
    }
}