using AntdUI;
using Label = AntdUI.Label;
using Panel = AntdUI.Panel;

namespace NET.D365.TOOLS
{
    public partial class MenuForm : BaseForm
    {
        private TableLayoutPanel mainLayout;
        private FlowLayoutPanel menuPanel;
        private Panel contentPanel;
        private Dictionary<string, Form> openedForms = new Dictionary<string, Form>();


        public MenuForm()
        {
            InitializeComponent();

            this.Text = "D365 系统工具";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1200, 800);
            this.BackColor = Color.FromArgb(240, 242, 245);

            InitializeUIWithTableLayout();
            LoadMenuItems();
        }

        private void InitializeUIWithTableLayout()
        {
            // 使用 TableLayoutPanel 实现响应式布局
            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };

            // 设置列比例：左侧菜单占25%，右侧内容占75%
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));

            // 创建菜单面板
            menuPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5, 5, 5, 5),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建内容面板
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = (System.Drawing.Drawing2D.DashStyle)BorderStyle.FixedSingle
            };

            // 添加到主布局
            mainLayout.Controls.Add(menuPanel, 0, 0);
            mainLayout.Controls.Add(contentPanel, 1, 0);

            // 添加欢迎面板
            var welcomePanel = CreateWelcomePanel();
            contentPanel.Controls.Add(welcomePanel);

            this.Controls.Add(mainLayout);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // 如果使用 TableLayoutPanel，它会自动调整
            // 只需要确保子窗体会跟随调整
            UpdateOpenedFormsSize();
        }

        private void UpdateOpenedFormsSize()
        {
            foreach (var form in openedForms.Values)
            {
                if (form != null && !form.IsDisposed)
                {
                    form.Size = contentPanel.ClientSize;

                    // 如果有 DataGridView，也调整它
                    foreach (Control control in form.Controls)
                    {
                        if (control is DataGridView dgv)
                        {
                            dgv.Size = new Size(
                                contentPanel.ClientSize.Width - 60,
                                contentPanel.ClientSize.Height - 200
                            );
                        }
                    }
                }
            }
        }

        private Panel CreateWelcomePanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(40)
            };

            // 欢迎标题
            var welcomeLabel = new Label
            {
                Text = "欢迎使用D365工具",
                Font = new Font("Microsoft YaHei UI", 24, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 144, 255),
                Size = new Size(600, 60),
                Location = new Point(40, 40)
            };

            // 描述文本
            var descLabel = new Label
            {
                Text = "请从左侧菜单中选择需要的功能工具\n所有工具都经过精心设计，确保操作安全可靠",
                Font = new Font("Microsoft YaHei UI", 12),
                ForeColor = Color.FromArgb(102, 102, 102),
                Size = new Size(600, 80),
                Location = new Point(40, 120),
                AutoSize = false
            };

            // 功能卡片容器
            var cardsPanel = new FlowLayoutPanel
            {
                Location = new Point(40, 220),
                Size = new Size(700, 300),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };

            // 创建功能卡片
            var card1 = CreateFeatureCard("📊", "D365表查询", "快速查询和浏览Dynamics 365数据表", Color.FromArgb(87, 148, 242));
            var card2 = CreateFeatureCard("🗑️", "清除数据库日志", "清理数据库日志文件，释放存储空间", Color.FromArgb(82, 196, 26));
            var card3 = CreateFeatureCard("🔄", "重启服务器", "安全重启服务器服务", Color.FromArgb(250, 84, 28));

            cardsPanel.Controls.Add(card1);
            cardsPanel.Controls.Add(card2);
            cardsPanel.Controls.Add(card3);

            panel.Controls.Add(welcomeLabel);
            panel.Controls.Add(descLabel);
            panel.Controls.Add(cardsPanel);

            return panel;
        }

        private Panel CreateFeatureCard(string icon, string title, string desc, Color color)
        {
            var card = new Panel
            {
                Size = new Size(300, 120),
                BackColor = Color.White,
                Margin = new Padding(0, 0, 20, 20),
                BorderStyle = (System.Drawing.Drawing2D.DashStyle)BorderStyle.FixedSingle,
                Padding = new Padding(20)
            };

            // 图标
            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 24),
                Size = new Size(50, 50),
                Location = new Point(20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 标题
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold),
                ForeColor = color,
                Size = new Size(200, 30),
                Location = new Point(80, 20)
            };

            // 描述
            var descLabel = new Label
            {
                Text = desc,
                Font = new Font("Microsoft YaHei UI", 10),
                ForeColor = Color.FromArgb(102, 102, 102),
                Size = new Size(200, 40),
                Location = new Point(80, 50),
                AutoSize = false
            };

            // 添加悬停效果
            card.MouseEnter += (s, e) =>
            {
                card.BackColor = Color.FromArgb(250, 250, 250);
                card.BorderColor = color;
            };

            card.MouseLeave += (s, e) =>
            {
                card.BackColor = Color.White;
                card.BorderColor = Color.LightGray;
            };

            card.Controls.Add(iconLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(descLabel);

            return card;
        }

        private void LoadMenuItems()
        {
            // 菜单项数据
            var menuItems = new[]
            {
                new { Key = "d365", Icon = "📊", Title = "D365表查询", Desc = "查询和浏览D365数据表" },
                new { Key = "clearlog", Icon = "🗑️", Title = "清除数据库日志", Desc = "清理数据库日志文件" },
                new { Key = "restart", Icon = "🔄", Title = "重启服务器", Desc = "重启服务器服务" }
            };

            foreach (var item in menuItems)
            {
                var menuCard = CreateMenuCard(item.Icon, item.Title, item.Desc, item.Key);
                menuPanel.Controls.Add(menuCard);
            }
        }

        private Panel CreateMenuCard(string icon, string title, string desc, string key)
        {
            var card = new Panel
            {
                Size = new Size(240, 100),
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 15),
                Padding = new Padding(15),
                Cursor = Cursors.Hand,
                Tag = key
            };

            // 图标
            var iconLabel = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 20),
                Size = new Size(50, 50),
                Location = new Point(15, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 标题
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(38, 38, 38),
                Size = new Size(150, 25),
                Location = new Point(70, 25)
            };

            // 描述
            var descLabel = new Label
            {
                Text = desc,
                Font = new Font("Microsoft YaHei UI", 10),
                ForeColor = Color.FromArgb(140, 140, 140),
                Size = new Size(150, 20),
                Location = new Point(70, 50),
                AutoSize = false
            };

            // 添加悬停和点击效果
            card.MouseEnter += (s, e) =>
            {
                card.BackColor = Color.FromArgb(230, 247, 255);
                card.BorderStyle = (System.Drawing.Drawing2D.DashStyle)BorderStyle.FixedSingle;
                card.BorderColor = Color.FromArgb(24, 144, 255);
            };

            card.MouseLeave += (s, e) =>
            {
                card.BackColor = Color.White;
                card.BorderStyle = (System.Drawing.Drawing2D.DashStyle)BorderStyle.None;
            };

            card.Click += async (s, e) =>await OpenFormAsync(key);

            // 添加点击事件到所有子控件
            foreach (Control control in new Control[] { iconLabel, titleLabel, descLabel })
            {
                control.Click += async (s, e) =>await OpenFormAsync(key);
                control.Cursor = Cursors.Hand;
                control.MouseEnter += (s, e) => card_MouseEnter(card, e);
                control.MouseLeave += (s, e) => card_MouseLeave(card, e);
            }

            card.Controls.Add(iconLabel);
            card.Controls.Add(titleLabel);
            card.Controls.Add(descLabel);

            return card;
        }

        private void card_MouseEnter(Panel card, EventArgs e)
        {
            card.BackColor = Color.FromArgb(230, 247, 255);
            card.BorderStyle = (System.Drawing.Drawing2D.DashStyle)BorderStyle.FixedSingle;
            card.BorderColor = Color.FromArgb(24, 144, 255);
        }

        private void card_MouseLeave(Panel card, EventArgs e)
        {
            card.BackColor = Color.White;
            card.BorderStyle = (System.Drawing.Drawing2D.DashStyle)BorderStyle.None;
        }

        private async Task OpenFormAsync(string formKey)
        {
            // 清除内容面板
            contentPanel.Controls.Clear();

            Form formToShow = null;

            if (openedForms.ContainsKey(formKey))
            {
                formToShow = openedForms[formKey];
            }
            else
            {
                formToShow = CreateFormByKey(formKey);
                openedForms[formKey] = formToShow;
            }

            if (formToShow != null)
            {
                formToShow.TopLevel = false;
                formToShow.FormBorderStyle = FormBorderStyle.None;
                formToShow.Dock = DockStyle.Fill;
                contentPanel.Controls.Add(formToShow);
                await ShowFormWithAnimation(formToShow);
            }
        }

        private async Task ShowFormWithAnimation(Form form)
        {
            form.Opacity = 0;
            form.Show();

            // 淡入动画
            for (int i = 0; i <= 100; i += 10)
            {
                form.Opacity = i / 100.0;
                await Task.Delay(10);
            }
            form.Opacity = 1;
        }

        private Form CreateFormByKey(string key)
        {
            return key switch
            {
                "d365" => new D365QueryForm(),
                "clearlog" => new ClearLogForm(),
                "restart" => new RestartServicesForm(),
                _ => throw new NotImplementedException()
            };
        }

    }
}
