using NET.D365.TOOLS.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace NET.D365.TOOLS
{
    public partial class RestartServicesForm : Form
    {
        private Panel resultPanel;
        private Button testButton;
        private Button executeButton;
        private Panel warningPanel;
        public RestartServicesForm()
        {
            InitializeComponent();
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.BackColor = Color.White;
            this.Padding = new Padding(30);
            this.Dock = DockStyle.Fill;

            // 标题区域 - 固定在顶部
            var titlePanel = new Panel
            {
                Size = new Size(this.ClientSize.Width - 60, 80),
                Location = new Point(30, 30),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var titleLabel = new Label
            {
                Text = "🔄 重启服务器",
                Font = new Font("Microsoft YaHei UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 144, 255),
                Size = new Size(300, 40),
                Location = new Point(0, 0)
            };

            var descLabel = new Label
            {
                Text = "安全重启服务器服务",
                Font = new Font("Microsoft YaHei UI", 11),
                ForeColor = Color.FromArgb(102, 102, 102),
                Size = new Size(400, 30),
                Location = new Point(0, 40)
            };

            titlePanel.Controls.Add(titleLabel);
            titlePanel.Controls.Add(descLabel);

            // 结果面板 - 固定在中间偏上
            resultPanel = new Panel
            {
                Size = new Size(this.ClientSize.Width - 60, 100),
                BackColor = Color.FromArgb(246, 255, 237),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(20),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // 动态计算垂直位置（在标题下方）
            resultPanel.Location = new Point(30, titlePanel.Bottom + 40);



            var resultText = new Label
            {
                Name = "resultText",
                Font = new Font("Microsoft YaHei UI", 12),
                ForeColor = Color.FromArgb(82, 196, 26),
                Size = new Size(resultPanel.Width - 100, 60),
                Location = new Point(80, 20),
                Text = "操作完成！",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            resultPanel.Controls.Add(resultText);

            // 操作按钮区域 - 固定在中间
            var buttonPanel = new Panel
            {
                Size = new Size(this.ClientSize.Width - 60, 80),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // 计算按钮水平居中位置
            int buttonPanelTop = resultPanel.Bottom + 40;
            buttonPanel.Location = new Point(30, buttonPanelTop);

            // 测试按钮 - 在按钮面板中水平居中
            testButton = new Button
            {
                Text = "测试重启",
                Size = new Size(150, 45),
                BackColor = Color.FromArgb(250, 173, 20),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Anchor = AnchorStyles.None // 不使用Anchor，手动居中
            };

            // 正式按钮
            executeButton = new Button
            {
                Text = "正式重启",
                Size = new Size(150, 45),
                BackColor = Color.FromArgb(24, 144, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold),
                Anchor = AnchorStyles.None
            };

            // 设置按钮位置使其居中
            UpdateButtonPositions(buttonPanel);
            // 添加按钮点击事件
            testButton.Click += TestButton_Click;
            executeButton.Click += ExecuteButton_Click;

            buttonPanel.Controls.Add(testButton);
            buttonPanel.Controls.Add(executeButton);

            // 警告信息 - 固定在底部
            warningPanel = new Panel
            {
                Size = new Size(this.ClientSize.Width - 60, 80),
                BackColor = Color.FromArgb(255, 250, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // 动态计算位置（在窗体底部，留出边距）
            warningPanel.Location = new Point(30, this.ClientSize.Height - warningPanel.Height - 30);

            var warningIcon = new Label
            {
                Text = "⚠️",
                Font = new Font("Segoe UI Emoji", 20),
                Size = new Size(40, 40),
                Location = new Point(10, 10),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var warningText = new Label
            {
                Text = "警告：重启会导致用户无法使用系统，在非业务高峰期执行。",
                Font = new Font("Microsoft YaHei UI", 10),
                ForeColor = Color.FromArgb(250, 84, 28),
                Size = new Size(warningPanel.Width - 80, 40),
                Location = new Point(60, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            warningPanel.Controls.Add(warningIcon);
            warningPanel.Controls.Add(warningText);

            this.Controls.Add(titlePanel);
            this.Controls.Add(resultPanel);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(warningPanel);

            // 监听窗体大小改变事件
            this.Resize += RestartServicesForm_Resize;
        }

        private void UpdateButtonPositions(Panel buttonPanel)
        {
            if (testButton != null && executeButton != null && buttonPanel != null)
            {
                // 计算两个按钮的总宽度和间距
                int totalButtonsWidth = testButton.Width + executeButton.Width + 20; // 20是按钮间距

                // 计算起始位置使其居中
                int startX = (buttonPanel.Width - totalButtonsWidth) / 2;

                testButton.Location = new Point(startX, (buttonPanel.Height - testButton.Height) / 2);
                executeButton.Location = new Point(startX + testButton.Width + 20,
                                                   (buttonPanel.Height - executeButton.Height) / 2);
            }
        }

        private void RestartServicesForm_Resize(object sender, EventArgs e)
        {
            // 调整按钮位置使其保持居中
            foreach (Control control in this.Controls)
            {
                if (control is Panel panel)
                {
                    // 更新按钮面板中按钮的位置
                    if (panel.Controls.Contains(testButton))
                    {
                        UpdateButtonPositions(panel);
                    }

                    // 更新警告面板位置（固定在底部）
                    if (panel.BackColor == Color.FromArgb(255, 250, 230))
                    {
                        panel.Location = new Point(30, this.ClientSize.Height - panel.Height - 30);
                        panel.Width = this.ClientSize.Width - 60;
                    }

                    // 更新结果面板位置
                    if (panel.BackColor == Color.FromArgb(246, 255, 237))
                    {
                        // 找到标题面板的位置
                        Panel titlePanel = null;
                        foreach (Control c in this.Controls)
                        {
                            if (c is Panel p && c.BackColor == Color.Transparent &&
                                c.Controls.Count > 0 && c.Controls[0] is Label label &&
                                label.Text.Contains("重启服务器"))
                            {
                                titlePanel = p;
                                break;
                            }
                        }

                        if (titlePanel != null)
                        {
                            panel.Location = new Point(30, titlePanel.Bottom + 40);
                            panel.Width = this.ClientSize.Width - 60;
                        }
                    }
                }
                
            }
        }

        private void ExecuteButton_Click(object sender, EventArgs e)
        {
            ShowResult("暂时不开放", Color.FromArgb(250, 173, 20));
        }

        private void TestButton_Click(object sender, EventArgs e)
        {
            RestartServices restart = new(true);
            string message = restart.RestartAll();
            // 显示结果面板
            ShowResult($"{message}", Color.FromArgb(250, 173, 20));
        }

        private void ShowResult(string message, Color color)
        {
            resultPanel.Visible = true;

            // 更新结果文本
            foreach (Control control in resultPanel.Controls)
            {
                if (control is Label label && label.Name == "resultText")
                {
                    label.Text = message;
                    label.ForeColor = color;
                    break;
                }
            }

            // 调整结果图标（根据消息类型）
            foreach (Control control in resultPanel.Controls)
            {
                if (control is Label label && label.Text.Length == 1 &&
                    (label.Text.Contains("✅") || label.Text.Contains("🧪") || label.Text.Contains("⚠️")))
                {
                    if (message.Contains("✅"))
                        label.Text = "✅";
                    else if (message.Contains("🧪"))
                        label.Text = "🧪";
                    else if (message.Contains("⚠️"))
                        label.Text = "⚠️";
                    break;
                }
            }

            // 5秒后自动隐藏结果
            var timer = new Timer();
            timer.Interval = 5000;
            timer.Tick += (s, e) =>
            {
                resultPanel.Visible = false;
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }
    }
}
