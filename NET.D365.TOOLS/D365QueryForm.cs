
using AntdUI;
using Dapper;
using Microsoft.Data.SqlClient;
using NET.D365.TOOLS.Models;
using NET.D365.TOOLS.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Button = System.Windows.Forms.Button;
using Label = System.Windows.Forms.Label;
using Panel = System.Windows.Forms.Panel;



namespace NET.D365.TOOLS
{
    public partial class D365QueryForm : Form
    {
        private AntdUI.Input searchBox; // 使用 AntdUI 的 Input 增强视觉
        private AntdUI.Input filterBox; // 表内字段/中文搜索框
        private AntdUI.Button searchButton;
        private System.Windows.Forms.DataGridView tableGrid;
        private AntdUI.Button refreshBtn;
        private Dictionary<string, string> _labelCache = new Dictionary<string, string>();
        private readonly string remotePath = @"\\172.31.10.54\PackagesLocalDirectory";
        private readonly string _connString = "Server=172.31.10.54;Database=AXDB;Uid=sa;Pwd=9PxhRs5LyxjHTg;Encrypt=False;Integrated Security=False;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private TableMetadataHelper _metaHelper;

        private System.Windows.Forms.Panel statusPanel;
        private AntdUI.Badge statusIcon;
        private AntdUI.Label statusLabel;

        // 用于存放当前查出来的全量字段数据，方便在内存中快速过滤
        private List<TableFieldModel> _allFieldsData = new List<TableFieldModel>();

        public D365QueryForm()
        {
            InitializeComponent();
            InitializeUI();
            LoadLabelCache();
        }

        private void InitializeUI()
        {
            this.BackColor = Color.White;
            this.Text = "D365 元数据查询工具";
            this.Size = new Size(1150, 800);


            // 1. 顶部搜索区域
            var searchPanel = new System.Windows.Forms.Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                Padding = new Padding(20)
            };

            searchBox = new AntdUI.Input
            {
                PlaceholderText = "输入 D365 表名（如：SalesTable、CustTable）",
                Size = new Size(300, 40),
                Location = new Point(20, 20),
                Font = new Font("Microsoft YaHei UI", 10)
            };

            searchButton = new AntdUI.Button
            {
                Text = "查询字段",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(110, 40),
                Location = new Point(330, 20),
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold)
            };
            searchButton.Click += btnSearch_Click;
            searchBox.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // 禁止回车嘀声
                    btnSearch_Click(null, null);
                }
            };

            // 新增：表内过滤器（搜索字段或中文名）
            filterBox = new AntdUI.Input
            {
                PlaceholderText = "在结果中搜索字段名或中文...",
                Size = new Size(300, 40),
                Location = new Point(460, 20),
                AllowClear = true,
                Enabled = false // 初始禁用，查出数据后再启用
            };
            // 绑定实时过滤事件
            filterBox.TextChanged += FilterBox_TextChanged;

            refreshBtn = new AntdUI.Button
            {
                Text = "更新索引",
                Type = AntdUI.TTypeMini.Default,
                Size = new Size(100, 40),
                Location = new Point(780, 20), // 放在 filterBox 后面
            };
            refreshBtn.Click += async (s, e) => {
                refreshBtn.Loading = true;
                UpdateStatus("正在强制刷新全量元数据索引，请稍候...", TState.Processing);
                try
                {
                    // 1. 同时执行两个耗时任务
                    var task1 = Task.Run(() => _metaHelper.BuildPathIndex(forceRefresh: true));
                    var task2 = LabelHelper.LoadAllLabelsAsync(remotePath, forceRefresh: true);

                    await Task.WhenAll(task1, task2);

                    // 2. 更新内存中的 Label 引用
                    _labelCache = await task2;

                    UpdateStatus("全量缓存更新成功！下次启动将自动秒开。", TState.Success);
                    AntdUI.Message.success(this, "缓存已更新至本地文件");
                }
                catch (Exception ex)
                {
                    UpdateStatus("更新失败: " + ex.Message, TState.Error);
                }
                finally
                {
                    refreshBtn.Loading = false;
                }
            };
            searchPanel.Controls.Add(refreshBtn);

            searchPanel.Controls.Add(searchBox);
            searchPanel.Controls.Add(searchButton);
            searchPanel.Controls.Add(filterBox);


            // 2. 底部状态栏方案 
            statusPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 35,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(10, 0, 10, 0)
            };

            statusIcon = new AntdUI.Badge
            {
                State = TState.Processing,
                Dock = DockStyle.Left,
                Size = new Size(20, 35)
            };

            statusLabel = new AntdUI.Label
            {
                Text = "正在初始化系统...",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9F),
                Margin = new Padding(5, 0, 0, 0)
            };

            statusPanel.Controls.Add(statusLabel);
            statusPanel.Controls.Add(statusIcon);


            // AntdUI 表格区域
            tableGrid = new System.Windows.Forms.DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Microsoft YaHei UI", 10),
                GridColor = Color.FromArgb(240, 240, 240),
                AutoGenerateColumns = false,
                EnableHeadersVisualStyles = false // 必须设为 false 才能自定义表头颜色
            };

            // --- 表头美化 ---
            tableGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            tableGrid.ColumnHeadersHeight = 40; // 调高表头
            tableGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            tableGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(250, 250, 250), // 浅灰背景（Antd 风格）
                ForeColor = Color.FromArgb(80, 80, 80),     // 深灰文字
                SelectionBackColor = Color.FromArgb(250, 250, 250),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold) // 表头加粗
            };

            // --- 行与单元格美化 ---
            tableGrid.RowTemplate.Height = 35; // 调高行间距，不显得拥挤
            tableGrid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.FromArgb(31, 31, 31),
                SelectionBackColor = Color.FromArgb(230, 244, 255), // 选中行浅蓝色
                SelectionForeColor = Color.FromArgb(22, 119, 255),  // 选中行文字变蓝
                Padding = new Padding(5, 0, 0, 0)                   // 文字左侧留白
            };

            tableGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "FieldName", DataPropertyName = "FieldName", HeaderText = "字段名", FillWeight = 35 });
            tableGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ChineseName", DataPropertyName = "ChineseName", HeaderText = "中文名/标签", FillWeight = 25 });
            tableGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataType", DataPropertyName = "DataType", HeaderText = "数据类型", FillWeight = 10 });
            tableGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Length", DataPropertyName = "Length", HeaderText = "长度", FillWeight = 10 });
            tableGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "RelatedTable", DataPropertyName = "RelatedTable", HeaderText = "关联表", FillWeight = 20 });

            tableGrid.CellFormatting += (s, e) => {
                if (e.RowIndex >= 0 && tableGrid.Columns[e.ColumnIndex].Name == "RelatedTable")
                {
                    if (e.Value != null && !string.IsNullOrEmpty(e.Value.ToString()))
                    {
                        e.CellStyle.ForeColor = Color.FromArgb(22, 119, 255); // 设置为蓝色
                        e.CellStyle.Font = new Font(tableGrid.Font, FontStyle.Underline); // 增加下划线
                    }
                }
            };

            tableGrid.CellClick += (s, e) => {
                if (e.RowIndex < 0) return; // 忽略点击标题

                var field = tableGrid.Rows[e.RowIndex].DataBoundItem as TableFieldModel;
                if (field == null) return;

                // 处理“关联表”跳转
                if (tableGrid.Columns[e.ColumnIndex].Name == "RelatedTable" && !string.IsNullOrEmpty(field.RelatedTable))
                {
                    searchBox.Text = field.RelatedTable;
                    btnSearch_Click(null, null); 
                }

            };

            tableGrid.CellMouseEnter += (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && tableGrid.Columns[e.ColumnIndex].Name == "RelatedTable")
                {
                    if (tableGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value != null)
                        tableGrid.Cursor = Cursors.Hand;
                }
            };
            tableGrid.CellMouseLeave += (s, e) => tableGrid.Cursor = Cursors.Default;

            tableGrid.CellDoubleClick += (s, e) => {
                if (e.RowIndex >= 0 && tableGrid.Rows[e.RowIndex].DataBoundItem is TableFieldModel field)
                {
                    if (!string.IsNullOrEmpty(field.EnumType)) ShowEnumModal(field.EnumType);
                }
            };

            

            // 添加到窗体
            this.Controls.Add(tableGrid);
            this.Controls.Add(searchPanel);
            this.Controls.Add(statusPanel);
        }

        private void ShowEnumModal(string enumName)
        {
            // 1. 准备枚举数据
            var enumItems = _metaHelper.GetEnumDetails(enumName,_connString).Select(x => new EnumDisplayModel
            {
                Value = x.Value,
                Key = x.Name,
                Label = LabelHelper.GetText(x.Label, _labelCache)
            }).ToList();

            // 2. 动态创建 AntdUI Table
            var enumGrid = new System.Windows.Forms.DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Microsoft YaHei UI", 10),
                AutoGenerateColumns = false,
                GridColor = Color.FromArgb(240, 240, 240), // 浅灰色网格线
                EnableHeadersVisualStyles = false,

            };

            // --- 表头美化 ---
            enumGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            enumGrid.ColumnHeadersHeight = 40; // 调高表头
            enumGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            enumGrid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(250, 250, 250), // 浅灰背景（Antd 风格）
                ForeColor = Color.FromArgb(80, 80, 80),     // 深灰文字
                SelectionBackColor = Color.FromArgb(250, 250, 250),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold) // 表头加粗
            };

            // --- 行与单元格美化 ---
            enumGrid.RowTemplate.Height = 35; // 调高行间距，不显得拥挤
            enumGrid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.FromArgb(31, 31, 31),
                SelectionBackColor = Color.FromArgb(230, 244, 255), // 选中行浅蓝色
                SelectionForeColor = Color.FromArgb(22, 119, 255),  // 选中行文字变蓝
                Padding = new Padding(5, 0, 0, 0)                   // 文字左侧留白
            };


            enumGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Value", HeaderText = "值", FillWeight = 20 });
            enumGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Key", HeaderText = "键", FillWeight = 40 });
            enumGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Label", HeaderText = "中文", FillWeight = 40 });

            enumGrid.DataSource = enumItems;

            // 3. 创建带圆角的 Panel 容器来控制整体大小
            var container = new System.Windows.Forms.Panel // 改用原生 Panel 减少冲突
            {
                Size = new Size(520, 420),
                Padding = new Padding(10),
                BackColor = Color.White
            };
            container.Controls.Add(enumGrid);

            // 3. 弹出 Modal
            AntdUI.Modal.open(new AntdUI.Modal.Config(this, $"枚举明细: {enumName}", container)
            {
                Width = 500
            });


        }

        // 实时过滤逻辑
        private void FilterBox_TextChanged(object sender, EventArgs e)
        {
            string keyword = filterBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(keyword))
            {
                tableGrid.DataSource = _allFieldsData; // 关键字为空时显示全量数据
                return;
            }

            // 在内存备份中搜索包含关键字的字段
            var filtered = _allFieldsData.Where(x =>
                (x.FieldName != null && x.FieldName.ToLower().Contains(keyword)) ||
                (x.ChineseName != null && x.ChineseName.ToLower().Contains(keyword))
            ).ToList();

            tableGrid.DataSource = new BindingList<TableFieldModel>(filtered);
            UpdateStatus($"已过滤，显示 {filtered.Count} / {_allFieldsData.Count} 个字段", TState.Success);
        }

        private async void LoadLabelCache()
        {
            UpdateStatus("正在扫描 Packages 目录并构建元数据索引...", TState.Processing);
            _metaHelper = new TableMetadataHelper(remotePath);

            _labelCache = await Task.Run(async () => {
                
                if (System.IO.File.Exists("label_cache.json"))
                {
                    try
                    {
                        string json = File.ReadAllText("label_cache.json");
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    }
                    catch { return null; }
                }
                return await LabelHelper.LoadAllLabelsAsync(remotePath);
            }) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); 
            UpdateStatus("系统就绪，元数据及 Label 缓存已加载。", TState.Success);
        }

        private void UpdateStatus(string text, TState state)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateStatus(text, state)));
                return;
            }
            statusLabel.Text = text;
            statusIcon.State = state;
        }
        
        private async void btnSearch_Click(object sender, EventArgs e)
        {
            string tableName = searchBox.Text.Trim();

            if (string.IsNullOrEmpty(tableName)) return;

            searchButton.Loading = true;
            filterBox.Enabled = false;
            
            UpdateStatus($"正在查询表 {tableName} 的字段数据...", TState.Processing);
            try
            {
                using (IDbConnection conn = new SqlConnection(_connString))
                {
                    string sql = @"SELECT f.name AS FieldName, t.name AS DataType, f.max_length AS Length 
                                   FROM sys.columns f 
                                   INNER JOIN sys.tables tb ON f.object_id = tb.object_id 
                                   INNER JOIN sys.types t ON f.user_type_id = t.user_type_id 
                                   WHERE tb.name = @tableName
                                   ORDER BY f.name ";

                    var dbFields = await conn.QueryAsync<TableFieldModel>(sql, new { tableName });

                    _allFieldsData = await Task.Run(() => {
                        var meta = _metaHelper.GetMetadataFromXml(tableName);

                        return dbFields.Select(f => {
                            if (!meta.Labels.TryGetValue(f.FieldName, out string labelId) || string.IsNullOrEmpty(labelId))
                            {
                                if (meta.FieldEdts.TryGetValue(f.FieldName, out string edtName))
                                {
                                    if (!string.IsNullOrEmpty(edtName))
                                    {
                                        labelId = _metaHelper.GetEdtLabel(edtName);
                                    }
                                    else
                                    {
                                        if (meta.FieldEnums.TryGetValue(f.FieldName, out string enumName))
                                        {
                                            labelId = _metaHelper.GetEnumLabel(enumName);
                                        }
                                    }
                                }
                            }

                            string originalCaseFieldName = f.FieldName;

                            var actualKey = meta.Labels.Keys.FirstOrDefault(k => k.Equals(f.FieldName, StringComparison.OrdinalIgnoreCase));

                            if (actualKey != null)
                            {
                                originalCaseFieldName = actualKey;
                            }

                            string currentEnum = meta.FieldEnums.ContainsKey(f.FieldName) ? meta.FieldEnums[f.FieldName] : "";
                            return new TableFieldModel
                            {
                                EnumType = currentEnum,
                                FieldName = !string.IsNullOrEmpty(currentEnum) ? $"{originalCaseFieldName}  [Enum]" : originalCaseFieldName,
                                ChineseName = LabelHelper.GetText(labelId, _labelCache),
                                DataType = f.DataType,
                                Length = f.Length,
                                RelatedTable = meta.Relations.ContainsKey(f.FieldName) ? meta.Relations[f.FieldName] : ""
                            };
                        }).ToList();
                    });
                    tableGrid.DataSource = new BindingList<TableFieldModel>(_allFieldsData); 
                    filterBox.Text = string.Empty;
                    filterBox.Enabled = true; // 查出数据后开启表内搜索
                    UpdateStatus($"查询完成。表 {tableName} 共有 {_allFieldsData.Count} 个字段。", TState.Success);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"查询失败: {ex.Message}", TState.Error);
                AntdUI.Message.error(this, "查询失败: " + ex.Message, autoClose: 3);
            }
            finally
            {
                searchButton.Loading = false;
            }
        }
    }
}
