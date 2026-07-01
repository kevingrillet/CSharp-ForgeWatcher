using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;

namespace CSharp_AzureDevopsNotifier
{
    [ExcludeFromCodeCoverage]
    partial class EditForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditForm));
            menuStrip1 = new MenuStrip();
            saveToolStripMenuItem = new ToolStripMenuItem();
            tabControl1 = new TabControl();
            tabPageSettings = new TabPage();
            tableLayoutPanelSettings = new TableLayoutPanel();
            labelOrganizationUrl = new Label();
            labelDelay = new Label();
            labelProjectName = new Label();
            labelPersonalAccessToken = new Label();
            textBoxOrganizationUrl = new TextBox();
            textBoxProjectName = new TextBox();
            textBoxPersonalAccessToken = new TextBox();
            numericUpDownDelay = new NumericUpDown();
            tabPageQueries = new TabPage();
            tableLayoutPanelQueries = new TableLayoutPanel();
            flowLayoutPanelQueriesDetails = new FlowLayoutPanel();
            buttonQueriesDetailsSave = new Button();
            dataGridViewQueries = new DataGridView();
            flowLayoutPanelQueries = new FlowLayoutPanel();
            buttonQueriesAdd = new Button();
            buttonQueriesDelete = new Button();
            tableLayoutPanelQueriesDetails = new TableLayoutPanel();
            labelName = new Label();
            labelRunning = new Label();
            labelType = new Label();
            labelRepositoryName = new Label();
            labelFilters = new Label();
            tableLayoutPanelQueriesFilters = new TableLayoutPanel();
            dataGridViewQueriesFilters = new DataGridView();
            flowLayoutPanelQueriesFilters = new FlowLayoutPanel();
            buttonQueriesFiltersAdd = new Button();
            buttonQueriesFiltersDelete = new Button();
            textBoxName = new TextBox();
            textBoxRepositoryName = new TextBox();
            comboBoxType = new ComboBox();
            checkBoxRunning = new CheckBox();
            menuStrip1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPageSettings.SuspendLayout();
            tableLayoutPanelSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDownDelay).BeginInit();
            tabPageQueries.SuspendLayout();
            tableLayoutPanelQueries.SuspendLayout();
            flowLayoutPanelQueriesDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewQueries).BeginInit();
            flowLayoutPanelQueries.SuspendLayout();
            tableLayoutPanelQueriesDetails.SuspendLayout();
            tableLayoutPanelQueriesFilters.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewQueriesFilters).BeginInit();
            flowLayoutPanelQueriesFilters.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { saveToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1264, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new Size(43, 20);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += SaveToolStripMenuItem_Click;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPageSettings);
            tabControl1.Controls.Add(tabPageQueries);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 24);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1264, 657);
            tabControl1.TabIndex = 1;
            // 
            // tabPageSettings
            // 
            tabPageSettings.Controls.Add(tableLayoutPanelSettings);
            tabPageSettings.Location = new Point(4, 24);
            tabPageSettings.Name = "tabPageSettings";
            tabPageSettings.Padding = new Padding(3);
            tabPageSettings.Size = new Size(1256, 629);
            tabPageSettings.TabIndex = 0;
            tabPageSettings.Text = "Settings";
            tabPageSettings.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanelSettings
            // 
            tableLayoutPanelSettings.ColumnCount = 4;
            tableLayoutPanelSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanelSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanelSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            tableLayoutPanelSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanelSettings.Controls.Add(labelOrganizationUrl, 1, 1);
            tableLayoutPanelSettings.Controls.Add(labelDelay, 1, 4);
            tableLayoutPanelSettings.Controls.Add(labelProjectName, 1, 3);
            tableLayoutPanelSettings.Controls.Add(labelPersonalAccessToken, 1, 2);
            tableLayoutPanelSettings.Controls.Add(textBoxOrganizationUrl, 2, 1);
            tableLayoutPanelSettings.Controls.Add(textBoxProjectName, 2, 3);
            tableLayoutPanelSettings.Controls.Add(textBoxPersonalAccessToken, 2, 2);
            tableLayoutPanelSettings.Controls.Add(numericUpDownDelay, 2, 4);
            tableLayoutPanelSettings.Dock = DockStyle.Fill;
            tableLayoutPanelSettings.Location = new Point(3, 3);
            tableLayoutPanelSettings.Name = "tableLayoutPanelSettings";
            tableLayoutPanelSettings.RowCount = 6;
            tableLayoutPanelSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tableLayoutPanelSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanelSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanelSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanelSettings.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanelSettings.RowStyles.Add(new RowStyle());
            tableLayoutPanelSettings.Size = new Size(1250, 623);
            tableLayoutPanelSettings.TabIndex = 0;
            // 
            // labelOrganizationUrl
            // 
            labelOrganizationUrl.AutoSize = true;
            labelOrganizationUrl.Dock = DockStyle.Right;
            labelOrganizationUrl.Location = new Point(407, 20);
            labelOrganizationUrl.Name = "labelOrganizationUrl";
            labelOrganizationUrl.Size = new Size(90, 50);
            labelOrganizationUrl.TabIndex = 0;
            labelOrganizationUrl.Text = "OrganizationUrl";
            // 
            // labelDelay
            // 
            labelDelay.AutoSize = true;
            labelDelay.Dock = DockStyle.Right;
            labelDelay.Location = new Point(461, 170);
            labelDelay.Name = "labelDelay";
            labelDelay.Size = new Size(36, 50);
            labelDelay.TabIndex = 3;
            labelDelay.Text = "Delay";
            // 
            // labelProjectName
            // 
            labelProjectName.AutoSize = true;
            labelProjectName.Dock = DockStyle.Right;
            labelProjectName.Location = new Point(421, 120);
            labelProjectName.Name = "labelProjectName";
            labelProjectName.Size = new Size(76, 50);
            labelProjectName.TabIndex = 2;
            labelProjectName.Text = "ProjectName";
            // 
            // labelPersonalAccessToken
            // 
            labelPersonalAccessToken.AutoSize = true;
            labelPersonalAccessToken.Dock = DockStyle.Right;
            labelPersonalAccessToken.Location = new Point(378, 70);
            labelPersonalAccessToken.Name = "labelPersonalAccessToken";
            labelPersonalAccessToken.Size = new Size(119, 50);
            labelPersonalAccessToken.TabIndex = 1;
            labelPersonalAccessToken.Text = "PersonalAccessToken";
            // 
            // textBoxOrganizationUrl
            // 
            textBoxOrganizationUrl.Dock = DockStyle.Top;
            textBoxOrganizationUrl.Location = new Point(503, 23);
            textBoxOrganizationUrl.Name = "textBoxOrganizationUrl";
            textBoxOrganizationUrl.Size = new Size(494, 23);
            textBoxOrganizationUrl.TabIndex = 4;
            // 
            // textBoxProjectName
            // 
            textBoxProjectName.Dock = DockStyle.Top;
            textBoxProjectName.Location = new Point(503, 123);
            textBoxProjectName.Name = "textBoxProjectName";
            textBoxProjectName.Size = new Size(494, 23);
            textBoxProjectName.TabIndex = 6;
            // 
            // textBoxPersonalAccessToken
            // 
            textBoxPersonalAccessToken.Dock = DockStyle.Top;
            textBoxPersonalAccessToken.Location = new Point(503, 73);
            textBoxPersonalAccessToken.Name = "textBoxPersonalAccessToken";
            textBoxPersonalAccessToken.Size = new Size(494, 23);
            textBoxPersonalAccessToken.TabIndex = 5;
            // 
            // numericUpDownDelay
            // 
            numericUpDownDelay.Dock = DockStyle.Top;
            numericUpDownDelay.Location = new Point(503, 173);
            numericUpDownDelay.Name = "numericUpDownDelay";
            numericUpDownDelay.Size = new Size(494, 23);
            numericUpDownDelay.TabIndex = 8;
            // 
            // tabPageQueries
            // 
            tabPageQueries.Controls.Add(tableLayoutPanelQueries);
            tabPageQueries.Location = new Point(4, 24);
            tabPageQueries.Name = "tabPageQueries";
            tabPageQueries.Padding = new Padding(3);
            tabPageQueries.Size = new Size(1256, 629);
            tabPageQueries.TabIndex = 1;
            tabPageQueries.Text = "Queries";
            tabPageQueries.UseVisualStyleBackColor = true;
            // 
            // tableLayoutPanelQueries
            // 
            tableLayoutPanelQueries.ColumnCount = 2;
            tableLayoutPanelQueries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            tableLayoutPanelQueries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            tableLayoutPanelQueries.Controls.Add(flowLayoutPanelQueriesDetails, 1, 0);
            tableLayoutPanelQueries.Controls.Add(dataGridViewQueries, 0, 1);
            tableLayoutPanelQueries.Controls.Add(flowLayoutPanelQueries, 0, 0);
            tableLayoutPanelQueries.Controls.Add(tableLayoutPanelQueriesDetails, 1, 1);
            tableLayoutPanelQueries.Dock = DockStyle.Fill;
            tableLayoutPanelQueries.Location = new Point(3, 3);
            tableLayoutPanelQueries.Name = "tableLayoutPanelQueries";
            tableLayoutPanelQueries.RowCount = 2;
            tableLayoutPanelQueries.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            tableLayoutPanelQueries.RowStyles.Add(new RowStyle());
            tableLayoutPanelQueries.Size = new Size(1250, 623);
            tableLayoutPanelQueries.TabIndex = 0;
            // 
            // flowLayoutPanelQueriesDetails
            // 
            flowLayoutPanelQueriesDetails.Controls.Add(buttonQueriesDetailsSave);
            flowLayoutPanelQueriesDetails.Dock = DockStyle.Fill;
            flowLayoutPanelQueriesDetails.Location = new Point(378, 3);
            flowLayoutPanelQueriesDetails.Name = "flowLayoutPanelQueriesDetails";
            flowLayoutPanelQueriesDetails.Size = new Size(869, 44);
            flowLayoutPanelQueriesDetails.TabIndex = 2;
            // 
            // buttonQueriesDetailsSave
            // 
            buttonQueriesDetailsSave.Location = new Point(3, 3);
            buttonQueriesDetailsSave.Name = "buttonQueriesDetailsSave";
            buttonQueriesDetailsSave.Size = new Size(118, 41);
            buttonQueriesDetailsSave.TabIndex = 0;
            buttonQueriesDetailsSave.Text = "Save";
            buttonQueriesDetailsSave.UseVisualStyleBackColor = true;
            buttonQueriesDetailsSave.Click += ButtonQueriesDetailsSave_Click;
            // 
            // dataGridViewQueries
            // 
            dataGridViewQueries.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewQueries.Dock = DockStyle.Fill;
            dataGridViewQueries.Location = new Point(3, 53);
            dataGridViewQueries.Name = "dataGridViewQueries";
            dataGridViewQueries.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewQueries.Size = new Size(369, 567);
            dataGridViewQueries.TabIndex = 0;
            dataGridViewQueries.SelectionChanged += DataGridViewQueries_SelectionChanged;
            // 
            // flowLayoutPanelQueries
            // 
            flowLayoutPanelQueries.Controls.Add(buttonQueriesAdd);
            flowLayoutPanelQueries.Controls.Add(buttonQueriesDelete);
            flowLayoutPanelQueries.Dock = DockStyle.Fill;
            flowLayoutPanelQueries.Location = new Point(3, 3);
            flowLayoutPanelQueries.Name = "flowLayoutPanelQueries";
            flowLayoutPanelQueries.Size = new Size(369, 44);
            flowLayoutPanelQueries.TabIndex = 1;
            // 
            // buttonQueriesAdd
            // 
            buttonQueriesAdd.Location = new Point(3, 3);
            buttonQueriesAdd.Name = "buttonQueriesAdd";
            buttonQueriesAdd.Size = new Size(118, 41);
            buttonQueriesAdd.TabIndex = 0;
            buttonQueriesAdd.Text = "Add";
            buttonQueriesAdd.UseVisualStyleBackColor = true;
            buttonQueriesAdd.Click += ButtonQueriesAdd_Click;
            // 
            // buttonQueriesDelete
            // 
            buttonQueriesDelete.Location = new Point(127, 3);
            buttonQueriesDelete.Name = "buttonQueriesDelete";
            buttonQueriesDelete.Size = new Size(118, 41);
            buttonQueriesDelete.TabIndex = 1;
            buttonQueriesDelete.Text = "Delete";
            buttonQueriesDelete.UseVisualStyleBackColor = true;
            buttonQueriesDelete.Click += ButtonQueriesDelete_Click;
            // 
            // tableLayoutPanelQueriesDetails
            // 
            tableLayoutPanelQueriesDetails.ColumnCount = 4;
            tableLayoutPanelQueriesDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelQueriesDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tableLayoutPanelQueriesDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            tableLayoutPanelQueriesDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
            tableLayoutPanelQueriesDetails.Controls.Add(labelName, 1, 1);
            tableLayoutPanelQueriesDetails.Controls.Add(labelRunning, 1, 2);
            tableLayoutPanelQueriesDetails.Controls.Add(labelType, 1, 3);
            tableLayoutPanelQueriesDetails.Controls.Add(labelRepositoryName, 1, 4);
            tableLayoutPanelQueriesDetails.Controls.Add(labelFilters, 1, 5);
            tableLayoutPanelQueriesDetails.Controls.Add(tableLayoutPanelQueriesFilters, 2, 5);
            tableLayoutPanelQueriesDetails.Controls.Add(textBoxName, 2, 1);
            tableLayoutPanelQueriesDetails.Controls.Add(textBoxRepositoryName, 2, 4);
            tableLayoutPanelQueriesDetails.Controls.Add(comboBoxType, 2, 3);
            tableLayoutPanelQueriesDetails.Controls.Add(checkBoxRunning, 2, 2);
            tableLayoutPanelQueriesDetails.Dock = DockStyle.Fill;
            tableLayoutPanelQueriesDetails.Location = new Point(378, 53);
            tableLayoutPanelQueriesDetails.Name = "tableLayoutPanelQueriesDetails";
            tableLayoutPanelQueriesDetails.RowCount = 6;
            tableLayoutPanelQueriesDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
            tableLayoutPanelQueriesDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanelQueriesDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanelQueriesDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanelQueriesDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanelQueriesDetails.RowStyles.Add(new RowStyle());
            tableLayoutPanelQueriesDetails.Size = new Size(869, 567);
            tableLayoutPanelQueriesDetails.TabIndex = 3;
            // 
            // labelName
            // 
            labelName.AutoSize = true;
            labelName.Dock = DockStyle.Right;
            labelName.Location = new Point(217, 10);
            labelName.Name = "labelName";
            labelName.Size = new Size(39, 30);
            labelName.TabIndex = 0;
            labelName.Text = "Name";
            // 
            // labelRunning
            // 
            labelRunning.AutoSize = true;
            labelRunning.Dock = DockStyle.Right;
            labelRunning.Location = new Point(204, 40);
            labelRunning.Name = "labelRunning";
            labelRunning.Size = new Size(52, 30);
            labelRunning.TabIndex = 1;
            labelRunning.Text = "Running";
            // 
            // labelType
            // 
            labelType.AutoSize = true;
            labelType.Dock = DockStyle.Right;
            labelType.Location = new Point(225, 70);
            labelType.Name = "labelType";
            labelType.Size = new Size(31, 30);
            labelType.TabIndex = 2;
            labelType.Text = "Type";
            // 
            // labelRepositoryName
            // 
            labelRepositoryName.AutoSize = true;
            labelRepositoryName.Dock = DockStyle.Right;
            labelRepositoryName.Location = new Point(161, 100);
            labelRepositoryName.Name = "labelRepositoryName";
            labelRepositoryName.Size = new Size(95, 30);
            labelRepositoryName.TabIndex = 3;
            labelRepositoryName.Text = "RepositoryName";
            // 
            // labelFilters
            // 
            labelFilters.AutoSize = true;
            labelFilters.Dock = DockStyle.Right;
            labelFilters.Location = new Point(218, 130);
            labelFilters.Name = "labelFilters";
            labelFilters.Size = new Size(38, 437);
            labelFilters.TabIndex = 4;
            labelFilters.Text = "Filters";
            // 
            // tableLayoutPanelQueriesFilters
            // 
            tableLayoutPanelQueriesFilters.ColumnCount = 1;
            tableLayoutPanelQueriesFilters.ColumnStyles.Add(new ColumnStyle());
            tableLayoutPanelQueriesFilters.Controls.Add(dataGridViewQueriesFilters, 0, 1);
            tableLayoutPanelQueriesFilters.Controls.Add(flowLayoutPanelQueriesFilters, 0, 0);
            tableLayoutPanelQueriesFilters.Dock = DockStyle.Fill;
            tableLayoutPanelQueriesFilters.Location = new Point(262, 133);
            tableLayoutPanelQueriesFilters.Name = "tableLayoutPanelQueriesFilters";
            tableLayoutPanelQueriesFilters.RowCount = 2;
            tableLayoutPanelQueriesFilters.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            tableLayoutPanelQueriesFilters.RowStyles.Add(new RowStyle());
            tableLayoutPanelQueriesFilters.Size = new Size(515, 431);
            tableLayoutPanelQueriesFilters.TabIndex = 5;
            // 
            // dataGridViewQueriesFilters
            // 
            dataGridViewQueriesFilters.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewQueriesFilters.Dock = DockStyle.Fill;
            dataGridViewQueriesFilters.Location = new Point(3, 33);
            dataGridViewQueriesFilters.Name = "dataGridViewQueriesFilters";
            dataGridViewQueriesFilters.Size = new Size(509, 567);
            dataGridViewQueriesFilters.TabIndex = 3;
            // 
            // flowLayoutPanelQueriesFilters
            // 
            flowLayoutPanelQueriesFilters.Controls.Add(buttonQueriesFiltersAdd);
            flowLayoutPanelQueriesFilters.Controls.Add(buttonQueriesFiltersDelete);
            flowLayoutPanelQueriesFilters.Dock = DockStyle.Fill;
            flowLayoutPanelQueriesFilters.Location = new Point(3, 3);
            flowLayoutPanelQueriesFilters.Name = "flowLayoutPanelQueriesFilters";
            flowLayoutPanelQueriesFilters.Size = new Size(509, 24);
            flowLayoutPanelQueriesFilters.TabIndex = 2;
            // 
            // buttonQueriesFiltersAdd
            // 
            buttonQueriesFiltersAdd.Location = new Point(3, 3);
            buttonQueriesFiltersAdd.Name = "buttonQueriesFiltersAdd";
            buttonQueriesFiltersAdd.Size = new Size(118, 21);
            buttonQueriesFiltersAdd.TabIndex = 0;
            buttonQueriesFiltersAdd.Text = "Add";
            buttonQueriesFiltersAdd.UseVisualStyleBackColor = true;
            buttonQueriesFiltersAdd.Click += ButtonQueriesFiltersAdd_Click;
            // 
            // buttonQueriesFiltersDelete
            // 
            buttonQueriesFiltersDelete.Location = new Point(127, 3);
            buttonQueriesFiltersDelete.Name = "buttonQueriesFiltersDelete";
            buttonQueriesFiltersDelete.Size = new Size(118, 21);
            buttonQueriesFiltersDelete.TabIndex = 1;
            buttonQueriesFiltersDelete.Text = "Delete";
            buttonQueriesFiltersDelete.UseVisualStyleBackColor = true;
            buttonQueriesFiltersDelete.Click += ButtonQueriesFiltersDelete_Click;
            // 
            // textBoxName
            // 
            textBoxName.Dock = DockStyle.Top;
            textBoxName.Location = new Point(262, 13);
            textBoxName.Name = "textBoxName";
            textBoxName.Size = new Size(515, 23);
            textBoxName.TabIndex = 6;
            // 
            // textBoxRepositoryName
            // 
            textBoxRepositoryName.Dock = DockStyle.Top;
            textBoxRepositoryName.Location = new Point(262, 103);
            textBoxRepositoryName.Name = "textBoxRepositoryName";
            textBoxRepositoryName.Size = new Size(515, 23);
            textBoxRepositoryName.TabIndex = 7;
            // 
            // comboBoxType
            // 
            comboBoxType.Dock = DockStyle.Top;
            comboBoxType.FormattingEnabled = true;
            comboBoxType.Location = new Point(262, 73);
            comboBoxType.Name = "comboBoxType";
            comboBoxType.Size = new Size(515, 23);
            comboBoxType.TabIndex = 8;
            // 
            // checkBoxRunning
            // 
            checkBoxRunning.AutoSize = true;
            checkBoxRunning.Dock = DockStyle.Top;
            checkBoxRunning.Location = new Point(262, 43);
            checkBoxRunning.Name = "checkBoxRunning";
            checkBoxRunning.Size = new Size(515, 14);
            checkBoxRunning.TabIndex = 9;
            checkBoxRunning.UseVisualStyleBackColor = true;
            // 
            // EditForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1264, 681);
            Controls.Add(tabControl1);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(1280, 720);
            Name = "EditForm";
            Text = "CSharp AzureDevopsNotifier";
            Load += EditForm_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPageSettings.ResumeLayout(false);
            tableLayoutPanelSettings.ResumeLayout(false);
            tableLayoutPanelSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDownDelay).EndInit();
            tabPageQueries.ResumeLayout(false);
            tableLayoutPanelQueries.ResumeLayout(false);
            flowLayoutPanelQueriesDetails.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewQueries).EndInit();
            flowLayoutPanelQueries.ResumeLayout(false);
            tableLayoutPanelQueriesDetails.ResumeLayout(false);
            tableLayoutPanelQueriesDetails.PerformLayout();
            tableLayoutPanelQueriesFilters.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewQueriesFilters).EndInit();
            flowLayoutPanelQueriesFilters.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button buttonQueriesAdd;
        private Button buttonQueriesDelete;
        private Button buttonQueriesDetailsSave;
        private Button buttonQueriesFiltersAdd;
        private Button buttonQueriesFiltersDelete;
        private CheckBox checkBoxRunning;
        private ComboBox comboBoxType;
        private DataGridView dataGridViewQueries;
        private DataGridView dataGridViewQueriesFilters;
        private FlowLayoutPanel flowLayoutPanelQueries;
        private FlowLayoutPanel flowLayoutPanelQueriesDetails;
        private FlowLayoutPanel flowLayoutPanelQueriesFilters;
        private Label labelDelay;
        private Label labelFilters;
        private Label labelName;
        private Label labelOrganizationUrl;
        private Label labelPersonalAccessToken;
        private Label labelProjectName;
        private Label labelRepositoryName;
        private Label labelRunning;
        private Label labelType;
        private MenuStrip menuStrip1;
        private NumericUpDown numericUpDownDelay;
        private TabControl tabControl1;
        private TableLayoutPanel tableLayoutPanelQueries;
        private TableLayoutPanel tableLayoutPanelQueriesDetails;
        private TableLayoutPanel tableLayoutPanelQueriesFilters;
        private TableLayoutPanel tableLayoutPanelSettings;
        private TabPage tabPageQueries;
        private TabPage tabPageSettings;
        private TextBox textBoxName;
        private TextBox textBoxOrganizationUrl;
        private TextBox textBoxPersonalAccessToken;
        private TextBox textBoxProjectName;
        private TextBox textBoxRepositoryName;
        private ToolStripMenuItem saveToolStripMenuItem;
    }
}
