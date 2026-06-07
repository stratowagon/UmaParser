using UmaBlobber.MasterData;

namespace UmaBlobber
{
    public partial class Form1
    {
        private void InitializeMasterDataUi()
        {
            UpdateMasterDataStatus();
        }

        private void UpdateMasterDataStatus()
        {
            SetStatus(GameMasterService.Current.GetStatusLine());
        }

        private void MasterDataStatusMenuItem_Click(object? sender, EventArgs e)
        {
            MessageBox.Show(
                this,
                GameMasterService.Current.GetDetailedStatus(),
                "Master data",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void MasterDataRefreshMenuItem_Click(object? sender, EventArgs e)
        {
            GameMasterService.Current.Refresh();
            UpdateMasterDataStatus();
        }

        private void MasterDataUseDefaultMenuItem_Click(object? sender, EventArgs e)
        {
            GameMasterService.Current.UseDefaultDatabaseLocation();
            UpdateMasterDataStatus();
        }

        private void MasterDataBrowseMenuItem_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select master.mdb",
                Filter = "Master database (master.mdb)|master.mdb|All files (*.*)|*.*",
                FileName = "master.mdb",
                InitialDirectory = Directory.Exists(MasterDataPaths.DefaultMasterFolder)
                    ? MasterDataPaths.DefaultMasterFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (!GameMasterService.Current.TrySetCustomDatabasePath(dialog.FileName))
            {
                MessageBox.Show(
                    this,
                    GameMasterService.Current.LastError ?? "Could not load the selected file.",
                    "Master data",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            UpdateMasterDataStatus();
        }

        private void MasterDataOpenFolderMenuItem_Click(object? sender, EventArgs e)
        {
            string folder = MasterDataPaths.DefaultMasterFolder;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Master data", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}