using System.Drawing;
using UmaBlobber.Import;
using UmaBlobber.Ui;

namespace UmaBlobber
{
    public partial class Form1 : Form
    {
        private const int UmaPerTeam = 3;
        private const int TeamCount = 5;
        private const int RosterRowCount = UmaPerTeam * TeamCount;
        private const int StatsTotalRowIndex = RosterRowCount;
        private const float TeamGroupBorderWidth = 2f;

        private static readonly Color SeverityStable = Color.FromArgb(46, 125, 50);
        private static readonly Color SeverityNeedsWork = Color.FromArgb(180, 134, 11);
        private static readonly Color SeverityCritical = Color.FromArgb(183, 28, 28);
        private static readonly Color SeverityForeground = Color.White;

        private enum GridDisplayMode
        {
            None,
            RosterMismatch,
            RosterStats
        }

        private GridDisplayMode _gridDisplayMode = GridDisplayMode.None;
        private readonly AceNameFontCache _aceNameFontCache = new();

        public Form1()
        {
            InitializeComponent();
            InitializeStatusUi();
            labelResultsWelcome.Text = ResultsWelcomeMessage;
            RestoreWindowLayout();
            ClearGrid();

            this.AllowDrop = true;

            // Wire up the events
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
            FormClosing += (_, _) =>
            {
                SaveWindowLayout();
                _aceNameFontCache.Dispose();
            };
            dataGridViewAnalysis.CellPainting += DataGridViewAnalysis_CellPainting;
            dataGridView1.CellFormatting += DataGridView1_CellFormatting;
            dataGridView1.CellPainting += DataGridView1_CellPainting;
            dataGridViewAnalysis.CellFormatting += DataGridViewAnalysis_CellFormatting;
            comboBoxSkillsUma.SelectedIndexChanged += ComboBoxSkillsUma_SelectedIndexChanged;
            dataGridViewSkills.CellFormatting += DataGridViewSkills_CellFormatting;
            dataGridViewSkills.SortCompare += DataGridViewSkills_SortCompare;
            DataGridViewColumnHeaderPaint.EnableThemedSortGlyphs(dataGridViewSkills);
            DataGridViewColumnHeaderPaint.EnableThemedSortGlyphs(dataGridViewAnalysis);
            DataGridViewColumnHeaderPaint.EnableThemedSortGlyphs(dataGridView1);
            InitializeViewMenu();
            InitializeMasterDataUi();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            // Check if the dragged data contains files
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;   // Show copy cursor
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            {
                return;
            }

            var imports = files.Select(CaptureImportService.TryImportPath).ToList();
            var batch = TeamTrialBatchBuilder.Build(imports);

            if (batch.Kind == TeamTrialBatchKind.Empty)
            {
                SetStatus(batch.SkippedFileCount > 0
                    ? $"No team trial files loaded ({batch.SkippedFileCount} file(s) skipped)."
                    : "No team trial files loaded.");
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

        private void UpdateResultsEmptyState()
        {
            bool showGrid = _gridDisplayMode != GridDisplayMode.None;
            dataGridView1.Visible = showGrid;
            panelResultsEmpty.Visible = !showGrid;
        }

        private void DataGridView1_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_gridDisplayMode == GridDisplayMode.None || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (_gridDisplayMode == GridDisplayMode.RosterStats && e.RowIndex == StatsTotalRowIndex)
            {
                ApplyHeaderLikeStyle(e.CellStyle);
                return;
            }

            if (ShouldBoldAceName(dataGridView1, e.RowIndex, e.ColumnIndex))
            {
                e.CellStyle.Font = _aceNameFontCache.GetBoldFont(dataGridView1);
            }
        }

        private bool ShouldBoldAceName(DataGridView grid, int rowIndex, int columnIndex)
        {
            if (!IsAceRow(rowIndex))
            {
                return false;
            }

            if (grid == dataGridViewAnalysis)
            {
                return columnIndex == 0;
            }

            if (grid == dataGridView1)
            {
                return _gridDisplayMode == GridDisplayMode.RosterMismatch || columnIndex == 0;
            }

            return false;
        }

        private static bool IsAceRow(int rowIndex) =>
            rowIndex >= 0 && rowIndex < RosterRowCount && rowIndex % UmaPerTeam == 0;

        private void DataGridView1_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (_gridDisplayMode == GridDisplayMode.None || e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            DataGridViewTeamGroupPaint.PaintDefaultTeamBoundaries(dataGridView1, e, TeamGroupBorderWidth);

            if (e.Handled)
            {
                return;
            }

            if (_gridDisplayMode == GridDisplayMode.RosterStats && e.RowIndex == StatsTotalRowIndex)
            {
                DataGridViewTeamGroupPaint.PaintTopBorder(
                    dataGridView1,
                    e,
                    TeamGroupBorderWidth,
                    [StatsTotalRowIndex]);
            }
        }

        private void DataGridViewAnalysis_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            DataGridViewTeamGroupPaint.PaintDefaultTeamBoundaries(dataGridViewAnalysis, e, TeamGroupBorderWidth);
        }

        private void ApplyHeaderLikeStyle(DataGridViewCellStyle cellStyle)
        {
            var headerStyle = dataGridView1.ColumnHeadersDefaultCellStyle;
            cellStyle.BackColor = headerStyle.BackColor;
            cellStyle.ForeColor = headerStyle.ForeColor;
            cellStyle.Font = headerStyle.Font;
            cellStyle.SelectionBackColor = ControlPaint.Dark(headerStyle.BackColor);
            cellStyle.SelectionForeColor = headerStyle.ForeColor;
        }

        private static Color BlendColors(Color from, Color to, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            int r = (int)(from.R + (to.R - from.R) * amount);
            int g = (int)(from.G + (to.G - from.G) * amount);
            int b = (int)(from.B + (to.B - from.B) * amount);
            return Color.FromArgb(from.A, r, g, b);
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

            dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
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
