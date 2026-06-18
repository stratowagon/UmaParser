namespace UmaBlobber
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            menuStrip1 = new MenuStrip();
            viewToolStripMenuItem = new ToolStripMenuItem();
            viewLightMenuItem = new ToolStripMenuItem();
            viewDarkMenuItem = new ToolStripMenuItem();
            masterDataToolStripMenuItem = new ToolStripMenuItem();
            masterDataStatusMenuItem = new ToolStripMenuItem();
            masterDataRefreshMenuItem = new ToolStripMenuItem();
            masterDataSeparator1 = new ToolStripSeparator();
            masterDataUseDefaultMenuItem = new ToolStripMenuItem();
            masterDataBrowseMenuItem = new ToolStripMenuItem();
            masterDataOpenFolderMenuItem = new ToolStripMenuItem();
            mainSplitContainer = new SplitContainer();
            statusTextBox = new TextBox();
            mainTabControl = new Ui.ThemedTabControl();
            tabPageResults = new TabPage();
            panelResultsEmpty = new Panel();
            labelResultsWelcome = new Label();
            dataGridView1 = new DataGridView();
            tabPageAnalysis = new TabPage();
            dataGridViewAnalysis = new DataGridView();
            tabPageSkills = new TabPage();
            dataGridViewSkills = new DataGridView();
            labelSkillsSummary = new Label();
            panelSkillsTop = new Panel();
            comboBoxSkillsUma = new ComboBox();
            labelSkillsUma = new Label();
            tabPageTracks = new TabPage();
            dataGridViewTracks = new DataGridView();
            labelTracksSummary = new Label();
            panelTracksTop = new Panel();
            comboBoxTracksUma = new ComboBox();
            labelTracksUma = new Label();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).BeginInit();
            mainSplitContainer.Panel1.SuspendLayout();
            mainSplitContainer.Panel2.SuspendLayout();
            mainSplitContainer.SuspendLayout();
            mainTabControl.SuspendLayout();
            tabPageResults.SuspendLayout();
            panelResultsEmpty.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            tabPageAnalysis.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewAnalysis).BeginInit();
            tabPageSkills.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewSkills).BeginInit();
            panelSkillsTop.SuspendLayout();
            tabPageTracks.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewTracks).BeginInit();
            panelTracksTop.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { viewToolStripMenuItem, masterDataToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(812, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { viewLightMenuItem, viewDarkMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // viewLightMenuItem
            // 
            viewLightMenuItem.Name = "viewLightMenuItem";
            viewLightMenuItem.Size = new Size(180, 22);
            viewLightMenuItem.Text = "Light";
            viewLightMenuItem.Click += ViewLightMenuItem_Click;
            // 
            // viewDarkMenuItem
            // 
            viewDarkMenuItem.Name = "viewDarkMenuItem";
            viewDarkMenuItem.Size = new Size(180, 22);
            viewDarkMenuItem.Text = "Dark";
            viewDarkMenuItem.Click += ViewDarkMenuItem_Click;
            // 
            // masterDataToolStripMenuItem
            // 
            masterDataToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { masterDataStatusMenuItem, masterDataRefreshMenuItem, masterDataSeparator1, masterDataUseDefaultMenuItem, masterDataBrowseMenuItem, masterDataOpenFolderMenuItem });
            masterDataToolStripMenuItem.Name = "masterDataToolStripMenuItem";
            masterDataToolStripMenuItem.Size = new Size(82, 20);
            masterDataToolStripMenuItem.Text = "Master Data";
            // 
            // masterDataStatusMenuItem
            // 
            masterDataStatusMenuItem.Name = "masterDataStatusMenuItem";
            masterDataStatusMenuItem.Size = new Size(219, 22);
            masterDataStatusMenuItem.Text = "Status...";
            masterDataStatusMenuItem.Click += MasterDataStatusMenuItem_Click;
            // 
            // masterDataRefreshMenuItem
            // 
            masterDataRefreshMenuItem.Name = "masterDataRefreshMenuItem";
            masterDataRefreshMenuItem.Size = new Size(219, 22);
            masterDataRefreshMenuItem.Text = "Refresh";
            masterDataRefreshMenuItem.Click += MasterDataRefreshMenuItem_Click;
            // 
            // masterDataSeparator1
            // 
            masterDataSeparator1.Name = "masterDataSeparator1";
            masterDataSeparator1.Size = new Size(216, 6);
            // 
            // masterDataUseDefaultMenuItem
            // 
            masterDataUseDefaultMenuItem.Name = "masterDataUseDefaultMenuItem";
            masterDataUseDefaultMenuItem.Size = new Size(219, 22);
            masterDataUseDefaultMenuItem.Text = "Use Default Location";
            masterDataUseDefaultMenuItem.Click += MasterDataUseDefaultMenuItem_Click;
            // 
            // masterDataBrowseMenuItem
            // 
            masterDataBrowseMenuItem.Name = "masterDataBrowseMenuItem";
            masterDataBrowseMenuItem.Size = new Size(219, 22);
            masterDataBrowseMenuItem.Text = "Browse for master.mdb...";
            masterDataBrowseMenuItem.Click += MasterDataBrowseMenuItem_Click;
            // 
            // masterDataOpenFolderMenuItem
            // 
            masterDataOpenFolderMenuItem.Name = "masterDataOpenFolderMenuItem";
            masterDataOpenFolderMenuItem.Size = new Size(219, 22);
            masterDataOpenFolderMenuItem.Text = "Open Default Master Folder";
            masterDataOpenFolderMenuItem.Click += MasterDataOpenFolderMenuItem_Click;
            // 
            // mainSplitContainer
            // 
            mainSplitContainer.Dock = DockStyle.Fill;
            mainSplitContainer.Location = new Point(0, 24);
            mainSplitContainer.Name = "mainSplitContainer";
            mainSplitContainer.Orientation = Orientation.Horizontal;
            // 
            // mainSplitContainer.Panel1
            // 
            mainSplitContainer.Panel1.Controls.Add(statusTextBox);
            mainSplitContainer.Panel1MinSize = 48;
            // 
            // mainSplitContainer.Panel2
            // 
            mainSplitContainer.Panel2.Controls.Add(mainTabControl);
            mainSplitContainer.Panel2MinSize = 160;
            mainSplitContainer.Size = new Size(812, 533);
            mainSplitContainer.SplitterDistance = 72;
            mainSplitContainer.SplitterWidth = 6;
            mainSplitContainer.TabIndex = 1;
            // 
            // statusTextBox
            // 
            statusTextBox.Dock = DockStyle.Fill;
            statusTextBox.Location = new Point(0, 0);
            statusTextBox.Multiline = true;
            statusTextBox.Name = "statusTextBox";
            statusTextBox.ReadOnly = true;
            statusTextBox.ScrollBars = ScrollBars.Vertical;
            statusTextBox.Size = new Size(812, 72);
            statusTextBox.TabIndex = 0;
            // 
            // mainTabControl
            // 
            mainTabControl.Controls.Add(tabPageResults);
            mainTabControl.Controls.Add(tabPageAnalysis);
            mainTabControl.Controls.Add(tabPageSkills);
            mainTabControl.Controls.Add(tabPageTracks);
            mainTabControl.Dock = DockStyle.Fill;
            mainTabControl.Location = new Point(0, 0);
            mainTabControl.Name = "mainTabControl";
            mainTabControl.SelectedIndex = 0;
            mainTabControl.Size = new Size(812, 455);
            mainTabControl.TabIndex = 0;
            // 
            // tabPageResults
            // 
            tabPageResults.Controls.Add(panelResultsEmpty);
            tabPageResults.Controls.Add(dataGridView1);
            tabPageResults.Location = new Point(4, 24);
            tabPageResults.Name = "tabPageResults";
            tabPageResults.Padding = new Padding(3);
            tabPageResults.Size = new Size(804, 427);
            tabPageResults.TabIndex = 0;
            tabPageResults.Text = "Results";
            tabPageResults.UseVisualStyleBackColor = true;
            // 
            // panelResultsEmpty
            // 
            panelResultsEmpty.Controls.Add(labelResultsWelcome);
            panelResultsEmpty.Dock = DockStyle.Fill;
            panelResultsEmpty.Location = new Point(3, 3);
            panelResultsEmpty.Name = "panelResultsEmpty";
            panelResultsEmpty.Size = new Size(798, 421);
            panelResultsEmpty.TabIndex = 1;
            // 
            // labelResultsWelcome
            // 
            labelResultsWelcome.Dock = DockStyle.Fill;
            labelResultsWelcome.ForeColor = SystemColors.GrayText;
            labelResultsWelcome.Location = new Point(0, 0);
            labelResultsWelcome.Name = "labelResultsWelcome";
            labelResultsWelcome.Padding = new Padding(24);
            labelResultsWelcome.Size = new Size(798, 421);
            labelResultsWelcome.TabIndex = 0;
            labelResultsWelcome.Text = "Drop Team Trials JSON files here.";
            labelResultsWelcome.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.Location = new Point(3, 3);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new Size(798, 421);
            dataGridView1.TabIndex = 0;
            dataGridView1.Visible = false;
            // 
            // tabPageAnalysis
            // 
            tabPageAnalysis.Controls.Add(dataGridViewAnalysis);
            tabPageAnalysis.Location = new Point(4, 24);
            tabPageAnalysis.Name = "tabPageAnalysis";
            tabPageAnalysis.Padding = new Padding(3);
            tabPageAnalysis.Size = new Size(804, 427);
            tabPageAnalysis.TabIndex = 1;
            tabPageAnalysis.Text = "Team Analysis";
            tabPageAnalysis.UseVisualStyleBackColor = true;
            // 
            // dataGridViewAnalysis
            // 
            dataGridViewAnalysis.AllowUserToAddRows = false;
            dataGridViewAnalysis.AllowUserToDeleteRows = false;
            dataGridViewAnalysis.AllowUserToResizeRows = false;
            dataGridViewAnalysis.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewAnalysis.Dock = DockStyle.Fill;
            dataGridViewAnalysis.EnableHeadersVisualStyles = false;
            dataGridViewAnalysis.Location = new Point(3, 3);
            dataGridViewAnalysis.Name = "dataGridViewAnalysis";
            dataGridViewAnalysis.ReadOnly = true;
            dataGridViewAnalysis.RowHeadersVisible = false;
            dataGridViewAnalysis.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewAnalysis.Size = new Size(798, 421);
            dataGridViewAnalysis.TabIndex = 0;
            // 
            // tabPageSkills
            // 
            tabPageSkills.Controls.Add(dataGridViewSkills);
            tabPageSkills.Controls.Add(labelSkillsSummary);
            tabPageSkills.Controls.Add(panelSkillsTop);
            tabPageSkills.Location = new Point(4, 24);
            tabPageSkills.Name = "tabPageSkills";
            tabPageSkills.Padding = new Padding(3);
            tabPageSkills.Size = new Size(804, 427);
            tabPageSkills.TabIndex = 2;
            tabPageSkills.Text = "Skills";
            tabPageSkills.UseVisualStyleBackColor = true;
            // 
            // tabPageTracks
            // 
            tabPageTracks.Controls.Add(dataGridViewTracks);
            tabPageTracks.Controls.Add(labelTracksSummary);
            tabPageTracks.Controls.Add(panelTracksTop);
            tabPageTracks.Location = new Point(4, 24);
            tabPageTracks.Name = "tabPageTracks";
            tabPageTracks.Padding = new Padding(3);
            tabPageTracks.Size = new Size(804, 427);
            tabPageTracks.TabIndex = 3;
            tabPageTracks.Text = "Tracks";
            tabPageTracks.UseVisualStyleBackColor = true;
            // 
            // dataGridViewTracks
            // 
            dataGridViewTracks.AllowUserToAddRows = false;
            dataGridViewTracks.AllowUserToDeleteRows = false;
            dataGridViewTracks.AllowUserToResizeRows = false;
            dataGridViewTracks.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewTracks.Dock = DockStyle.Fill;
            dataGridViewTracks.EnableHeadersVisualStyles = false;
            dataGridViewTracks.Location = new Point(3, 54);
            dataGridViewTracks.Name = "dataGridViewTracks";
            dataGridViewTracks.ReadOnly = true;
            dataGridViewTracks.RowHeadersVisible = false;
            dataGridViewTracks.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewTracks.ShowCellToolTips = true;
            dataGridViewTracks.Size = new Size(798, 370);
            dataGridViewTracks.TabIndex = 2;
            // 
            // labelTracksSummary
            // 
            labelTracksSummary.Dock = DockStyle.Top;
            labelTracksSummary.Location = new Point(3, 35);
            labelTracksSummary.Name = "labelTracksSummary";
            labelTracksSummary.Padding = new Padding(0, 0, 0, 4);
            labelTracksSummary.Size = new Size(798, 19);
            labelTracksSummary.TabIndex = 1;
            // 
            // panelTracksTop
            // 
            panelTracksTop.Controls.Add(comboBoxTracksUma);
            panelTracksTop.Controls.Add(labelTracksUma);
            panelTracksTop.Dock = DockStyle.Top;
            panelTracksTop.Location = new Point(3, 3);
            panelTracksTop.Name = "panelTracksTop";
            panelTracksTop.Size = new Size(798, 32);
            panelTracksTop.TabIndex = 0;
            // 
            // comboBoxTracksUma
            // 
            comboBoxTracksUma.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxTracksUma.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxTracksUma.Location = new Point(44, 4);
            comboBoxTracksUma.Name = "comboBoxTracksUma";
            comboBoxTracksUma.Size = new Size(751, 23);
            comboBoxTracksUma.TabIndex = 1;
            // 
            // labelTracksUma
            // 
            labelTracksUma.AutoSize = true;
            labelTracksUma.Location = new Point(3, 8);
            labelTracksUma.Name = "labelTracksUma";
            labelTracksUma.Size = new Size(35, 15);
            labelTracksUma.TabIndex = 0;
            labelTracksUma.Text = "Uma:";
            // 
            // dataGridViewSkills
            // 
            dataGridViewSkills.AllowUserToAddRows = false;
            dataGridViewSkills.AllowUserToDeleteRows = false;
            dataGridViewSkills.AllowUserToResizeRows = false;
            dataGridViewSkills.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewSkills.Dock = DockStyle.Fill;
            dataGridViewSkills.EnableHeadersVisualStyles = false;
            dataGridViewSkills.Location = new Point(3, 54);
            dataGridViewSkills.Name = "dataGridViewSkills";
            dataGridViewSkills.ReadOnly = true;
            dataGridViewSkills.RowHeadersVisible = false;
            dataGridViewSkills.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewSkills.ShowCellToolTips = true;
            dataGridViewSkills.Size = new Size(798, 370);
            dataGridViewSkills.TabIndex = 2;
            // 
            // labelSkillsSummary
            // 
            labelSkillsSummary.Dock = DockStyle.Top;
            labelSkillsSummary.Location = new Point(3, 35);
            labelSkillsSummary.Name = "labelSkillsSummary";
            labelSkillsSummary.Padding = new Padding(0, 0, 0, 4);
            labelSkillsSummary.Size = new Size(798, 19);
            labelSkillsSummary.TabIndex = 1;
            // 
            // panelSkillsTop
            // 
            panelSkillsTop.Controls.Add(comboBoxSkillsUma);
            panelSkillsTop.Controls.Add(labelSkillsUma);
            panelSkillsTop.Dock = DockStyle.Top;
            panelSkillsTop.Location = new Point(3, 3);
            panelSkillsTop.Name = "panelSkillsTop";
            panelSkillsTop.Size = new Size(798, 32);
            panelSkillsTop.TabIndex = 0;
            // 
            // comboBoxSkillsUma
            // 
            comboBoxSkillsUma.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            comboBoxSkillsUma.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSkillsUma.Location = new Point(44, 4);
            comboBoxSkillsUma.Name = "comboBoxSkillsUma";
            comboBoxSkillsUma.Size = new Size(751, 23);
            comboBoxSkillsUma.TabIndex = 1;
            // 
            // labelSkillsUma
            // 
            labelSkillsUma.AutoSize = true;
            labelSkillsUma.Location = new Point(3, 8);
            labelSkillsUma.Name = "labelSkillsUma";
            labelSkillsUma.Size = new Size(35, 15);
            labelSkillsUma.TabIndex = 0;
            labelSkillsUma.Text = "Uma:";
            // 
            // Form1
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(812, 557);
            Controls.Add(mainSplitContainer);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "Uma Parser";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            mainSplitContainer.Panel1.ResumeLayout(false);
            mainSplitContainer.Panel1.PerformLayout();
            mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)mainSplitContainer).EndInit();
            mainSplitContainer.ResumeLayout(false);
            mainTabControl.ResumeLayout(false);
            tabPageResults.ResumeLayout(false);
            panelResultsEmpty.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            tabPageAnalysis.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewAnalysis).EndInit();
            tabPageSkills.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewSkills).EndInit();
            panelSkillsTop.ResumeLayout(false);
            panelSkillsTop.PerformLayout();
            tabPageTracks.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewTracks).EndInit();
            panelTracksTop.ResumeLayout(false);
            panelTracksTop.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem viewLightMenuItem;
        private ToolStripMenuItem viewDarkMenuItem;
        private ToolStripMenuItem masterDataToolStripMenuItem;
        private ToolStripMenuItem masterDataStatusMenuItem;
        private ToolStripMenuItem masterDataRefreshMenuItem;
        private ToolStripSeparator masterDataSeparator1;
        private ToolStripMenuItem masterDataUseDefaultMenuItem;
        private ToolStripMenuItem masterDataBrowseMenuItem;
        private ToolStripMenuItem masterDataOpenFolderMenuItem;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private SplitContainer mainSplitContainer;
        private TextBox statusTextBox;
        private Ui.ThemedTabControl mainTabControl;
        private TabPage tabPageResults;
        private Panel panelResultsEmpty;
        private Label labelResultsWelcome;
        private TabPage tabPageAnalysis;
        private TabPage tabPageSkills;
        private DataGridView dataGridViewAnalysis;
        private Panel panelSkillsTop;
        private Label labelSkillsUma;
        private ComboBox comboBoxSkillsUma;
        private Label labelSkillsSummary;
        private DataGridView dataGridViewSkills;
        private DataGridView dataGridView1;

        // Tracks tab
        private TabPage tabPageTracks;
        private DataGridView dataGridViewTracks;
        private Label labelTracksSummary;
        private Panel panelTracksTop;
        private ComboBox comboBoxTracksUma;
        private Label labelTracksUma;
    }
}