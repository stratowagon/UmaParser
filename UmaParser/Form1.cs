using System.Drawing;
using UmaParser.Import;
using UmaParser.Ui;

namespace UmaParser
{
    public partial class Form1 : Form
    {
        private const int UmaPerTeam = 3;
        private const int TeamCount = 5;
        private const int RosterRowCount = UmaPerTeam * TeamCount;
        private const int StatsTotalRowIndex = RosterRowCount;
        private const float TeamGroupBorderWidth = 2f;

        // Legacy aliases kept for minimal diff in other files. Prefer AppColors.Severity* going forward.
        private static Color SeverityStable => AppColors.SeverityStableBack;
        private static Color SeverityNeedsWork => AppColors.SeverityNeedsWorkBack;
        private static Color SeverityCritical => AppColors.SeverityCriticalBack;
        private static Color SeverityForeground => AppColors.SeverityStableFore; // white in both modes for these backs

        private enum GridDisplayMode
        {
            None,
            RosterMismatch,
            RosterStats
        }

        private GridDisplayMode _gridDisplayMode = GridDisplayMode.None;
        private readonly AceNameFontCache _aceNameFontCache = new();

        // Empty state for Team Analysis tab (non-uniform TT or non-TT loads)
        private Panel panelAnalysisEmpty = null!;
        private Label labelAnalysisMessage = null!;

        public Form1()
        {
            InitializeComponent();
            InitializeStatusUi();
            labelResultsWelcome.Text = ResultsWelcomeMessage;
            RestoreWindowLayout();
            ClearGrid();
            InitializeAnalysisEmptyState();

            this.AllowDrop = true;

            // Wire up the events
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
            FormClosing += (_, _) =>
            {
                SaveWindowLayout();
                _aceNameFontCache.Dispose();
            };
            dataGridViewAnalysis.RowPostPaint += DataGridViewAnalysis_RowPostPaint;
            dataGridView1.RowPostPaint += DataGridView1_RowPostPaint;
            dataGridViewAnalysis.CellFormatting += DataGridViewAnalysis_CellFormatting;
            DataGridViewOptimizations.EnableDoubleBuffering(dataGridView1);
            DataGridViewOptimizations.EnableDoubleBuffering(dataGridViewAnalysis);
            comboBoxSkillsUma.SelectedIndexChanged += ComboBoxSkillsUma_SelectedIndexChanged;
            dataGridViewSkills.CellFormatting += DataGridViewSkills_CellFormatting;
            dataGridViewSkills.SortCompare += DataGridViewSkills_SortCompare;
            DataGridViewColumnHeaderPaint.EnableThemedSortGlyphs(dataGridViewSkills);
            comboBoxTracksUma.SelectedIndexChanged += ComboBoxTracksUma_SelectedIndexChanged;
            dataGridViewTracks.CellFormatting += DataGridViewTracks_CellFormatting;
            dataGridViewTracks.SortCompare += DataGridViewTracks_SortCompare;
            dataGridViewTracks.Sorted += DataGridViewTracks_Sorted;
            DataGridViewColumnHeaderPaint.EnableThemedSortGlyphs(dataGridViewTracks);
            DataGridViewColumnHeaderPaint.EnableThemedSortGlyphs(dataGridViewAnalysis);
            DataGridViewColumnHeaderPaint.EnableThemedSortGlyphs(dataGridView1);
            InitializeViewMenu();
            InitializeOptionsMenu();
            InitializeMasterDataUi();
            AppThemeApplier.Apply(this);
        }

        private void Form1_DragEnter(object? sender, DragEventArgs e)
        {
            // Check if the dragged data contains files
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                e.Effect = DragDropEffects.Copy;   // Show copy cursor
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Form1_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            {
                return;
            }

            var imports = files.Select(CaptureImportService.TryImportPath).ToList();
            var batch = TeamTrialBatchBuilder.Build(imports);

            if (!batch.HasAnyData)
            {
                SetStatus(batch.SkippedFileCount > 0
                    ? $"No supported race files loaded ({batch.SkippedFileCount} file(s) skipped)."
                    : "No supported race files loaded.");
                return;
            }

            BindTeamTrialBatch(batch);
        }


        //*************************************************
        // Helpers
        //*************************************************
        private void ClearGrid()
        {
            _gridDisplayMode = GridDisplayMode.None;
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();
            UpdateResultsEmptyState();
        }

        private void InitializeAnalysisEmptyState()
        {
            panelAnalysisEmpty = new Panel();
            labelAnalysisMessage = new Label();

            panelAnalysisEmpty.SuspendLayout();

            panelAnalysisEmpty.Controls.Add(labelAnalysisMessage);
            panelAnalysisEmpty.Dock = DockStyle.Fill;
            panelAnalysisEmpty.Location = new Point(3, 3);
            panelAnalysisEmpty.Name = "panelAnalysisEmpty";
            panelAnalysisEmpty.Size = new Size(798, 421);
            panelAnalysisEmpty.TabIndex = 1;
            panelAnalysisEmpty.Visible = false;

            labelAnalysisMessage.Dock = DockStyle.Fill;
            labelAnalysisMessage.ForeColor = SystemColors.GrayText;
            labelAnalysisMessage.Location = new Point(0, 0);
            labelAnalysisMessage.Name = "labelAnalysisMessage";
            labelAnalysisMessage.Padding = new Padding(24);
            labelAnalysisMessage.Size = new Size(798, 421);
            labelAnalysisMessage.TabIndex = 0;
            labelAnalysisMessage.TextAlign = ContentAlignment.MiddleCenter;

            panelAnalysisEmpty.ResumeLayout(false);

            tabPageAnalysis.Controls.Add(panelAnalysisEmpty);
            // Ensure grid is on top when visible; panel will be shown/hidden as needed
            dataGridViewAnalysis.BringToFront();
        }

        private void UpdateResultsEmptyState()
        {
            bool showGrid = _gridDisplayMode != GridDisplayMode.None;
            dataGridView1.Visible = showGrid;
            panelResultsEmpty.Visible = !showGrid;
        }

        private bool ShouldBoldAceName(DataGridView grid, int rowIndex, int columnIndex)
        {
            if (!IsAceRow(rowIndex))
            {
                return false;
            }

            if (grid == dataGridViewAnalysis)
            {
                return columnIndex >= 0
                    && columnIndex < grid.Columns.Count
                    && grid.Columns[columnIndex].Name == GridColumnIds.Analysis.Uma;
            }

            if (grid == dataGridView1)
            {
                return _gridDisplayMode == GridDisplayMode.RosterMismatch || columnIndex == 0;
            }

            return false;
        }

        private static bool IsAceRow(int rowIndex) =>
            rowIndex >= 0 && rowIndex < RosterRowCount && rowIndex % UmaPerTeam == 0;

        private void DataGridView1_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (_gridDisplayMode == GridDisplayMode.None)
            {
                return;
            }

            DataGridViewTeamGroupPaint.PaintDefaultRowTopBorders(dataGridView1, e, TeamGroupBorderWidth);

            if (_gridDisplayMode == GridDisplayMode.RosterStats)
            {
                DataGridViewTeamGroupPaint.PaintRowTopBorders(
                    dataGridView1,
                    e,
                    TeamGroupBorderWidth,
                    [StatsTotalRowIndex]);
            }
        }

        private void DataGridViewAnalysis_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridViewTeamGroupPaint.PaintDefaultRowTopBorders(dataGridViewAnalysis, e, TeamGroupBorderWidth);
        }

        private void SetGridSize(List<string> columnNames, int rows)
        {
            dataGridView1.Columns.Clear();
            for (int i = 0; i < columnNames.Count; i++)
            {
                var column = dataGridView1.Columns.Add(null, columnNames[i].ToString());
                dataGridView1.Columns[column].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            for (int i = 0; i < rows; i++)
            {
                dataGridView1.Rows.Add();
            }
        }

        private void SetGridSize(int columns, int rows)
        {
            dataGridView1.Columns.Clear();
            for (int i = 0; i < columns; i++)
            {
                var column = dataGridView1.Columns.Add(null, null);
                dataGridView1.Columns[column].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            for (int i = 0; i < rows; i++)
            {
                dataGridView1.Rows.Add();
            }
        }

        private void FinalizeGridDisplay()
        {
            UpdateResultsEmptyState();

            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            if (_gridDisplayMode is GridDisplayMode.RosterStats or GridDisplayMode.RosterMismatch)
            {
                dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader);
            }
            else
            {
                dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            }

            ClearGridSelection(dataGridView1);
        }

        private static void ClearGridSelection(DataGridView grid)
        {
            grid.ClearSelection();
            if (grid.RowCount > 0 && grid.ColumnCount > 0)
            {
                grid.CurrentCell = null;
            }
        }

        private void SetCellValue(int columnIndex, int rowIndex, object value)
        {
            if (columnIndex >= 0 && columnIndex < dataGridView1.ColumnCount &&
                rowIndex >= 0 && rowIndex < dataGridView1.RowCount)
            {
                dataGridView1[columnIndex, rowIndex].Value = value;
            }
        }
    }
}
