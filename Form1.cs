using MsgPack;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using UmaBlobber.DataModel.ResponseData;
using UmaBlobber.ObjectModel;

namespace UmaBlobber
{
    public partial class Form1 : Form
    {
        private readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new UmaApiResponseConverter() }
        };

        public Form1()
        {
            InitializeComponent();

            // Enable drag-and-drop on the form
            this.AllowDrop = true;

            // Wire up the events
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
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
            Dictionary<string, TeamTrialResult> TTResults = new();

            // list of dropped file paths
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files != null && files.Length > 0)
            {
                // Process each file
                foreach (string file in files)
                {
                    object? deserializedObject = null;
                    string json = String.Empty;
                    string extension = Path.GetExtension(file);
                    if (extension == ".bin")
                    {
                        // try to unpack .bin msgpack and save as json (from CarrotJuicer or raw response data)
                        (deserializedObject, json) = MsgPack.MsgPackToJsonConverter.ConvertToJson(file);
                        string outputJsonPath = Path.ChangeExtension(file, ".json");
                        File.WriteAllText(outputJsonPath!, json);
                    }
                    else if (extension == ".json")
                    {
                        json = File.ReadAllText(file);
                    }

                    if (!String.IsNullOrEmpty(json))
                    {
                        UmaApiResponse? response = null;
                        try
                        {
                            response = JsonSerializer.Deserialize<UmaApiResponse>(json, JsonOptions);
                        }
                        catch
                        {
                            // Not a valid JSON file, skip it
                            continue;
                        }

                        // For now we only care about team trial results.
                        if (response != null && response is TeamTrialResult ttResult)
                        {
                            TTResults.Add(Path.GetFileName(file), ttResult);
                        }
                    }
                }

                if (TTResults.Count > 0)
                {
                    if (!TTResults.All(t => t.Value.RosterNames.SequenceEqual(TTResults.First().Value.RosterNames)))
                    {
                        // Not all of the rosters matched.  Use the grid to display each file's roster so we can identify the outliers.
                        ClearGrid();
                        SetGridSize(TTResults.Count, 15);
                        for (int i = 0; i < TTResults.Count; i++)
                        {
                            dataGridView1.Columns[i].HeaderText = TTResults.ElementAt(i).Key;
                            for (int j = 0; j < TTResults.ElementAt(i).Value.RosterNames.Count; j++)
                            {
                                SetCellValue(i, j, TTResults.ElementAt(i).Value.RosterNames[j]);
                            }
                        }
                    }
                    else
                    {
                        // All results have the same roster, lay out the grid with uma stats so we can paste into a spreadsheet.
                        ClearGrid();

                        // Name + 1 column per file
                        List<string> columnNames = new() { "Name" };
                        for (int i = 0; i < TTResults.Count; i++)
                        {
                            columnNames.Add((i + 1).ToString());
                        }
                        // 
                        SetGridSize(columnNames, 16);

                        // Names column, plus a "total" row at the bottom
                        var names = TTResults.First().Value.RosterNames;
                        for (int i = 0; i < names.Count; i++)
                        {
                            SetCellValue(0, i, names[i]);
                        }
                        SetCellValue(0,15, "Total");

                        // Scores
                        for (int r = 0; r < TTResults.Count; r++)
                        {
                            var scores = TTResults.ElementAt(r).Value.RosterScores;
                            for (int i = 0; i < scores.Count; i++)
                            {
                                SetCellValue(r + 1, i, scores[i]);
                            }
                            SetCellValue(r + 1, 15, TTResults.ElementAt(r).Value.TotalScore);
                        }
                    }
                }
            }
        }


        //*************************************************
        // Helpers
        //*************************************************
        private void ClearGrid()
        {
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();
        }

        private void SetGridSize(List<string> columnNames, int rows)
        {
                dataGridView1.Columns.Clear();
                for (int i = 0; i < columnNames.Count; i++)
                {
                    dataGridView1.Columns.Add(null, columnNames[i].ToString());
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
                dataGridView1.Columns.Add(null, null);
            }
            for (int i = 0; i < rows; i++)
            {
                dataGridView1.Rows.Add();
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
