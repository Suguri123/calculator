using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartCalculatorApp
{
    public class MainForm : Form
    {
        // 색상 테마
        private readonly Color BgMain = Color.FromArgb(15, 23, 42);
        private readonly Color BgCard = Color.FromArgb(30, 41, 59);
        private readonly Color BgPanel = Color.FromArgb(21, 30, 48);
        private readonly Color BgInput = Color.FromArgb(11, 17, 32);
        private readonly Color TextMain = Color.FromArgb(248, 250, 252);
        private readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        private readonly Color Accent = Color.FromArgb(99, 102, 241);
        private readonly Color OpBg = Color.FromArgb(49, 46, 129);
        private readonly Color UtilBg = Color.FromArgb(51, 65, 85);

        private Panel panelCalc;
        private Panel panelCurrency;
        private Panel panelUnit;
        private Button btnTabCalc;
        private Button btnTabCurrency;
        private Button btnTabUnit;

        // 계산기 상태
        private Label lblExpression;
        private Label lblDisplay;
        private ListBox lstHistory;
        private string currentInput = "0";
        private string expression = "";
        private bool justEvaluated = false;

        // 환율 상태
        private ComboBox cmbFromCurr;
        private ComboBox cmbToCurr;
        private TextBox txtFromAmt;
        private TextBox txtToAmt;
        private Label lblRateStatus;
        private Label lblRateSummary;
        private ListView lvPopularRates;
        private bool suppressCurrencyEvent = false;
        private Dictionary<string, double> exchangeRates = new Dictionary<string, double>();
        private Dictionary<string, string> currencyNames = new Dictionary<string, string>();

        // 단위 변환 상태
        private ComboBox cmbUnitCategory;
        private ComboBox cmbFromUnit;
        private ComboBox cmbToUnit;
        private TextBox txtFromUnit;
        private TextBox txtToUnit;
        private Label lblUnitSummary;
        private ListView lvAllUnits;
        private bool suppressUnitEvent = false;

        public MainForm()
        {
            this.Text = "스마트 올인원 계산기 (계산 · 실시간 환율 · 단위변환)";
            this.Size = new Size(860, 680);
            this.MinimumSize = new Size(820, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = BgMain;
            this.ForeColor = TextMain;
            this.Font = new Font("Malgun Gothic", 10f, FontStyle.Regular);
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

            InitCurrencyData();
            BuildHeaderAndTabs();
            BuildCalculatorPanel();
            BuildCurrencyPanel();
            BuildUnitPanel();

            SwitchTab("calc");
            var unusedTask = FetchLiveRatesAsync();
        }

        private void BuildHeaderAndTabs()
        {
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 108,
                BackColor = BgPanel,
                Padding = new Padding(20, 12, 20, 0)
            };

            Label title = new Label
            {
                Text = "∑ 스마트 올인원 계산기",
                Font = new Font("Malgun Gothic", 15f, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(20, 14),
                AutoSize = true
            };

            Label sub = new Label
            {
                Text = "일반/공학 계산 · 실시간 환율 · 8종 단위 변환",
                Font = new Font("Malgun Gothic", 9f, FontStyle.Regular),
                ForeColor = TextMuted,
                Location = new Point(22, 42),
                AutoSize = true
            };

            Button btnOpenWeb = new Button
            {
                Text = "🌐 브라우저(웹 버전) 열기",
                Font = new Font("Malgun Gothic", 9f, FontStyle.Bold),
                BackColor = BgCard,
                ForeColor = TextMain,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(180, 34),
                Location = new Point(640, 16),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnOpenWeb.FlatAppearance.BorderColor = Accent;
            btnOpenWeb.Click += (s, e) =>
            {
                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
                if (File.Exists(htmlPath))
                {
                    Process.Start(new ProcessStartInfo(htmlPath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("같은 폴더에 index.html 파일이 없습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnTabCalc = CreateTabButton("🧮 계산기", 20);
            btnTabCurrency = CreateTabButton("💱 실시간 환율", 210);
            btnTabUnit = CreateTabButton("📏 단위 변환", 400);

            btnTabCalc.Click += (s, e) => SwitchTab("calc");
            btnTabCurrency.Click += (s, e) => SwitchTab("currency");
            btnTabUnit.Click += (s, e) => SwitchTab("unit");

            header.Controls.Add(title);
            header.Controls.Add(sub);
            header.Controls.Add(btnOpenWeb);
            header.Controls.Add(btnTabCalc);
            header.Controls.Add(btnTabCurrency);
            header.Controls.Add(btnTabUnit);

            this.Controls.Add(header);
        }

        private Button CreateTabButton(string text, int x)
        {
            Button btn = new Button
            {
                Text = text,
                Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold),
                Size = new Size(180, 36),
                Location = new Point(x, 68),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void SwitchTab(string tab)
        {
            panelCalc.Visible = (tab == "calc");
            panelCurrency.Visible = (tab == "currency");
            panelUnit.Visible = (tab == "unit");

            StyleTabBtn(btnTabCalc, tab == "calc");
            StyleTabBtn(btnTabCurrency, tab == "currency");
            StyleTabBtn(btnTabUnit, tab == "unit");
        }

        private void StyleTabBtn(Button btn, bool active)
        {
            btn.BackColor = active ? Accent : BgCard;
            btn.ForeColor = active ? Color.White : TextMuted;
        }

        // =====================================================
        // 1. 계산기 패널
        // =====================================================
        private void BuildCalculatorPanel()
        {
            panelCalc = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = BgMain
            };

            // 디스플레이 박스
            Panel displayBox = new Panel
            {
                Location = new Point(20, 120),
                Size = new Size(530, 100),
                BackColor = BgInput
            };

            lblExpression = new Label
            {
                Text = "",
                Font = new Font("Malgun Gothic", 11f),
                ForeColor = TextMuted,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(10, 10),
                Size = new Size(510, 26)
            };

            lblDisplay = new Label
            {
                Text = "0",
                Font = new Font("Malgun Gothic", 24f, FontStyle.Bold),
                ForeColor = TextMain,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(10, 40),
                Size = new Size(510, 50)
            };

            displayBox.Controls.Add(lblExpression);
            displayBox.Controls.Add(lblDisplay);
            panelCalc.Controls.Add(displayBox);

            // 공학용 버튼 줄
            string[,] sciButtons = new string[,]
            {
                { "sin", "cos", "tan", "√x", "x²", "xʸ" },
                { "log", "ln", "π", "1/x", "(", ")" }
            };

            int sciStartY = 230;
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    string label = sciButtons[r, c];
                    Button b = CreateCalcButton(label, 20 + c * 89, sciStartY + r * 42, 84, 36, BgPanel, TextMuted, 9.5f);
                    b.Click += (s, e) => HandleSciButton(label);
                    panelCalc.Controls.Add(b);
                }
            }

            // 기본 키패드
            string[,] mainButtons = new string[,]
            {
                { "AC", "⌫", "%", "÷" },
                { "7", "8", "9", "×" },
                { "4", "5", "6", "−" },
                { "1", "2", "3", "+" },
                { "±", "0", ".", "=" }
            };

            int gridStartY = 322;
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    string label = mainButtons[r, c];
                    Color bg = BgCard;
                    if (label == "÷" || label == "×" || label == "−" || label == "+") bg = OpBg;
                    else if (label == "=") bg = Accent;
                    else if (label == "AC" || label == "⌫" || label == "%" || label == "±") bg = UtilBg;

                    Button b = CreateCalcButton(label, 20 + c * 134, gridStartY + r * 58, 128, 52, bg, TextMain, 13f);
                    b.Click += (s, e) => HandleCalcButton(label);
                    panelCalc.Controls.Add(b);
                }
            }

            // 우측 계산 기록 패널
            Panel historyPanel = new Panel
            {
                Location = new Point(568, 120),
                Size = new Size(255, 490),
                BackColor = BgPanel
            };

            Label lblHistTitle = new Label
            {
                Text = "📋 계산 기록 (더블클릭 시 불러오기)",
                Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(12, 12),
                AutoSize = true
            };

            lstHistory = new ListBox
            {
                Location = new Point(12, 40),
                Size = new Size(231, 395),
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Malgun Gothic", 10f)
            };
            lstHistory.DoubleClick += (s, e) =>
            {
                if (lstHistory.SelectedItem != null)
                {
                    string item = lstHistory.SelectedItem.ToString();
                    int idx = item.LastIndexOf('=');
                    if (idx >= 0)
                    {
                        currentInput = item.Substring(idx + 1).Trim().Replace(",", "");
                        expression = "";
                        justEvaluated = true;
                        UpdateCalcDisplay();
                    }
                }
            };

            Button btnCopyRes = CreateCalcButton("결과 복사", 12, 442, 110, 36, Accent, Color.White, 9.5f);
            btnCopyRes.Click += (s, e) =>
            {
                Clipboard.SetText(currentInput);
                MessageBox.Show("현재 값(" + FormatNumber(currentInput) + ")이 클립보드에 복사되었습니다.", "복사 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnClearHist = CreateCalcButton("기록 비우기", 132, 442, 110, 36, UtilBg, TextMain, 9.5f);
            btnClearHist.Click += (s, e) => lstHistory.Items.Clear();

            historyPanel.Controls.Add(lblHistTitle);
            historyPanel.Controls.Add(lstHistory);
            historyPanel.Controls.Add(btnCopyRes);
            historyPanel.Controls.Add(btnClearHist);
            panelCalc.Controls.Add(historyPanel);

            this.Controls.Add(panelCalc);
        }

        private Button CreateCalcButton(string text, int x, int y, int w, int h, Color bg, Color fg, float fontSize)
        {
            Button b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = bg,
                ForeColor = fg,
                Font = new Font("Malgun Gothic", fontSize, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            return b;
        }

        private void HandleSciButton(string op)
        {
            if (op == "(" || op == ")")
            {
                if (op == "(")
                {
                    if (currentInput != "0")
                    {
                        expression += currentInput + "*(";
                        currentInput = "0";
                    }
                    else expression += "(";
                }
                else
                {
                    expression += currentInput + ")";
                    currentInput = "0";
                }
                UpdateCalcDisplay();
                return;
            }

            double x;
            if (!double.TryParse(currentInput, NumberStyles.Any, CultureInfo.InvariantCulture, out x)) return;

            double res = x;
            double rad = x * Math.PI / 180.0;
            switch (op)
            {
                case "sin": res = Math.Sin(rad); break;
                case "cos": res = Math.Cos(rad); break;
                case "tan": res = Math.Tan(rad); break;
                case "√x": res = Math.Sqrt(x); break;
                case "x²": res = x * x; break;
                case "xʸ":
                    expression += currentInput + "^";
                    currentInput = "0";
                    justEvaluated = false;
                    UpdateCalcDisplay();
                    return;
                case "log": res = Math.Log10(x); break;
                case "ln": res = Math.Log(x); break;
                case "π": res = Math.PI; break;
                case "1/x": res = 1.0 / x; break;
            }

            currentInput = (double.IsNaN(res) || double.IsInfinity(res))
                ? "오류"
                : Math.Round(res, 10).ToString(CultureInfo.InvariantCulture);
            justEvaluated = true;
            UpdateCalcDisplay();
        }

        private void HandleCalcButton(string label)
        {
            if (label == "AC")
            {
                currentInput = "0";
                expression = "";
                justEvaluated = false;
            }
            else if (label == "⌫")
            {
                if (justEvaluated || currentInput == "오류")
                {
                    currentInput = "0";
                    justEvaluated = false;
                }
                else if (currentInput.Length > 1)
                    currentInput = currentInput.Substring(0, currentInput.Length - 1);
                else
                    currentInput = "0";
            }
            else if (label == "±")
            {
                if (currentInput != "0" && currentInput != "오류")
                {
                    currentInput = currentInput.StartsWith("-")
                        ? currentInput.Substring(1)
                        : "-" + currentInput;
                }
            }
            else if (label == "%")
            {
                double v;
                if (double.TryParse(currentInput, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                    currentInput = (v / 100.0).ToString(CultureInfo.InvariantCulture);
            }
            else if (label == "=")
            {
                EvaluateCalculation();
                return;
            }
            else if (label == "+" || label == "−" || label == "×" || label == "÷")
            {
                string op = label == "×" ? "*" : label == "÷" ? "/" : label == "−" ? "-" : "+";
                if (justEvaluated)
                {
                    expression = currentInput + op;
                    currentInput = "0";
                    justEvaluated = false;
                }
                else
                {
                    expression += currentInput + op;
                    currentInput = "0";
                }
            }
            else if (label == ".")
            {
                if (justEvaluated)
                {
                    expression = "";
                    currentInput = "0.";
                    justEvaluated = false;
                }
                else if (!currentInput.Contains("."))
                {
                    currentInput += ".";
                }
            }
            else // 숫자
            {
                if (justEvaluated || currentInput == "0" || currentInput == "오류")
                {
                    if (justEvaluated) expression = "";
                    currentInput = label;
                    justEvaluated = false;
                }
                else
                {
                    currentInput += label;
                }
            }
            UpdateCalcDisplay();
        }

        private void EvaluateCalculation()
        {
            string full = expression + currentInput;
            try
            {
                // 거듭제곱(^) 처리
                while (full.Contains("^"))
                {
                    Match m = Regex.Match(full, @"([\-0-9.]+)\^([\-0-9.]+)");
                    if (!m.Success) break;
                    double b = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    double e = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    double p = Math.Pow(b, e);
                    full = full.Substring(0, m.Index) + p.ToString(CultureInfo.InvariantCulture) + full.Substring(m.Index + m.Length);
                }

                DataTable dt = new DataTable();
                object raw = dt.Compute(full, "");
                double res = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                if (double.IsInfinity(res) || double.IsNaN(res))
                {
                    currentInput = "오류";
                }
                else
                {
                    string clean = Math.Round(res, 10).ToString(CultureInfo.InvariantCulture);
                    string pretty = full.Replace("*", "×").Replace("/", "÷");
                    lstHistory.Items.Insert(0, pretty + " = " + FormatNumber(clean));
                    expression = pretty + " =";
                    currentInput = clean;
                    justEvaluated = true;
                }
            }
            catch
            {
                currentInput = "오류";
            }
            UpdateCalcDisplay();
        }

        private string FormatNumber(string val)
        {
            double d;
            if (!double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return val;
            string[] parts = val.Split('.');
            long intPart;
            if (long.TryParse(parts[0], out intPart))
            {
                string formattedInt = intPart.ToString("N0", CultureInfo.InvariantCulture);
                if (parts.Length > 1) return formattedInt + "." + parts[1];
                return formattedInt;
            }
            return d.ToString("G12", CultureInfo.InvariantCulture);
        }

        private void UpdateCalcDisplay()
        {
            lblExpression.Text = expression.Replace("*", "×").Replace("/", "÷");
            lblDisplay.Text = FormatNumber(currentInput);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!panelCalc.Visible) return;

            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 && !e.Shift)
                HandleCalcButton((e.KeyCode - Keys.D0).ToString());
            else if (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9)
                HandleCalcButton((e.KeyCode - Keys.NumPad0).ToString());
            else if (e.KeyCode == Keys.Add || (e.KeyCode == Keys.Oemplus && e.Shift))
                HandleCalcButton("+");
            else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)
                HandleCalcButton("−");
            else if (e.KeyCode == Keys.Multiply || (e.KeyCode == Keys.D8 && e.Shift))
                HandleCalcButton("×");
            else if (e.KeyCode == Keys.Divide || e.KeyCode == Keys.OemQuestion)
                HandleCalcButton("÷");
            else if (e.KeyCode == Keys.Decimal || e.KeyCode == Keys.OemPeriod)
                HandleCalcButton(".");
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Oemplus)
            {
                EvaluateCalculation();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Back)
                HandleCalcButton("⌫");
            else if (e.KeyCode == Keys.Escape)
                HandleCalcButton("AC");
        }

        // =====================================================
        // 2. 실시간 환율 패널
        // =====================================================
        private void InitCurrencyData()
        {
            currencyNames["USD"] = "미국 달러 ($)";
            currencyNames["KRW"] = "대한민국 원 (₩)";
            currencyNames["JPY"] = "일본 엔 (¥)";
            currencyNames["EUR"] = "유럽 유로 (€)";
            currencyNames["CNY"] = "중국 위안 (¥)";
            currencyNames["GBP"] = "영국 파운드 (£)";
            currencyNames["AUD"] = "호주 달러 (A$)";
            currencyNames["CAD"] = "캐나다 달러 (C$)";
            currencyNames["CHF"] = "스위스 프랑 (CHF)";
            currencyNames["HKD"] = "홍콩 달러 (HK$)";
            currencyNames["SGD"] = "싱가포르 달러 (S$)";
            currencyNames["TWD"] = "대만 달러 (NT$)";
            currencyNames["THB"] = "태국 바트 (฿)";
            currencyNames["VND"] = "베트남 동 (₫)";

            exchangeRates["USD"] = 1.0;
            exchangeRates["KRW"] = 1385.5;
            exchangeRates["JPY"] = 153.2;
            exchangeRates["EUR"] = 0.924;
            exchangeRates["CNY"] = 7.24;
            exchangeRates["GBP"] = 0.772;
            exchangeRates["AUD"] = 1.51;
            exchangeRates["CAD"] = 1.388;
            exchangeRates["CHF"] = 0.866;
            exchangeRates["HKD"] = 7.77;
            exchangeRates["SGD"] = 1.322;
            exchangeRates["TWD"] = 32.08;
            exchangeRates["THB"] = 33.75;
            exchangeRates["VND"] = 25280.0;
        }

        private void BuildCurrencyPanel()
        {
            panelCurrency = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = BgMain
            };

            lblRateStatus = new Label
            {
                Text = "🔄 실시간 환율 정보를 불러오는 중...",
                Font = new Font("Malgun Gothic", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(16, 185, 129),
                Location = new Point(24, 125),
                AutoSize = true
            };

            Button btnRefreshRates = CreateCalcButton("🔄 최신 환율 새로고침", 640, 118, 180, 34, BgCard, TextMain, 9.5f);
            btnRefreshRates.Click += async (s, e) => await FetchLiveRatesAsync();

            Panel convCard = new Panel
            {
                Location = new Point(20, 162),
                Size = new Size(800, 115),
                BackColor = BgPanel
            };

            Label l1 = new Label { Text = "보내는 통화 (기준)", ForeColor = TextMuted, Location = new Point(18, 12), AutoSize = true };
            cmbFromCurr = new ComboBox
            {
                Location = new Point(18, 36),
                Size = new Size(320, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextMain,
                Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold)
            };
            txtFromAmt = new TextBox
            {
                Location = new Point(18, 72),
                Size = new Size(320, 32),
                Text = "1",
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Malgun Gothic", 13f, FontStyle.Bold)
            };

            Button btnSwapCurr = CreateCalcButton("⇄", 368, 48, 60, 44, Accent, Color.White, 14f);
            btnSwapCurr.Click += (s, e) =>
            {
                int idx = cmbFromCurr.SelectedIndex;
                cmbFromCurr.SelectedIndex = cmbToCurr.SelectedIndex;
                cmbToCurr.SelectedIndex = idx;
                UpdateCurrencyConversion(true);
            };

            Label l2 = new Label { Text = "받는 통화 (변환 결과)", ForeColor = TextMuted, Location = new Point(458, 12), AutoSize = true };
            cmbToCurr = new ComboBox
            {
                Location = new Point(458, 36),
                Size = new Size(320, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextMain,
                Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold)
            };
            txtToAmt = new TextBox
            {
                Location = new Point(458, 72),
                Size = new Size(320, 32),
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Malgun Gothic", 13f, FontStyle.Bold)
            };

            foreach (var kv in currencyNames)
            {
                string item = kv.Key + " - " + kv.Value;
                cmbFromCurr.Items.Add(item);
                cmbToCurr.Items.Add(item);
            }
            cmbFromCurr.SelectedIndex = 0; // USD
            cmbToCurr.SelectedIndex = 1;   // KRW

            cmbFromCurr.SelectedIndexChanged += (s, e) => UpdateCurrencyConversion(true);
            cmbToCurr.SelectedIndexChanged += (s, e) => UpdateCurrencyConversion(true);
            txtFromAmt.TextChanged += (s, e) => { if (!suppressCurrencyEvent) UpdateCurrencyConversion(true); };
            txtToAmt.TextChanged += (s, e) => { if (!suppressCurrencyEvent) UpdateCurrencyConversion(false); };

            convCard.Controls.AddRange(new Control[] { l1, cmbFromCurr, txtFromAmt, btnSwapCurr, l2, cmbToCurr, txtToAmt });

            lblRateSummary = new Label
            {
                Location = new Point(20, 286),
                Size = new Size(800, 34),
                BackColor = OpBg,
                ForeColor = Color.White,
                Font = new Font("Malgun Gothic", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblListHeader = new Label
            {
                Text = "🌍 주요 통화 실시간 환율표 (항목 더블클릭 시 받는 통화로 선택)",
                Font = new Font("Malgun Gothic", 10f, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(20, 330),
                AutoSize = true
            };

            lvPopularRates = new ListView
            {
                Location = new Point(20, 358),
                Size = new Size(800, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Malgun Gothic", 10f)
            };
            lvPopularRates.Columns.Add("통화 코드", 110);
            lvPopularRates.Columns.Add("통화명", 200);
            lvPopularRates.Columns.Add("입력 금액 환산", 230);
            lvPopularRates.Columns.Add("원화(KRW) 기준 환율", 240);
            lvPopularRates.DoubleClick += (s, e) =>
            {
                if (lvPopularRates.SelectedItems.Count > 0)
                {
                    string code = lvPopularRates.SelectedItems[0].Text;
                    for (int i = 0; i < cmbToCurr.Items.Count; i++)
                    {
                        if (cmbToCurr.Items[i].ToString().StartsWith(code))
                        {
                            cmbToCurr.SelectedIndex = i;
                            break;
                        }
                    }
                }
            };

            panelCurrency.Controls.Add(lblRateStatus);
            panelCurrency.Controls.Add(btnRefreshRates);
            panelCurrency.Controls.Add(convCard);
            panelCurrency.Controls.Add(lblRateSummary);
            panelCurrency.Controls.Add(lblListHeader);
            panelCurrency.Controls.Add(lvPopularRates);

            this.Controls.Add(panelCurrency);
            UpdateCurrencyConversion(true);
        }

        private string GetSelectedCode(ComboBox cmb)
        {
            if (cmb.SelectedItem == null) return "USD";
            return cmb.SelectedItem.ToString().Substring(0, 3);
        }

        private void UpdateCurrencyConversion(bool forward)
        {
            suppressCurrencyEvent = true;
            try
            {
                string fromCode = GetSelectedCode(cmbFromCurr);
                string toCode = GetSelectedCode(cmbToCurr);
                double rFrom = exchangeRates.ContainsKey(fromCode) ? exchangeRates[fromCode] : 1.0;
                double rTo = exchangeRates.ContainsKey(toCode) ? exchangeRates[toCode] : 1.0;

                if (forward)
                {
                    double val;
                    if (double.TryParse(txtFromAmt.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                    {
                        double res = (val / rFrom) * rTo;
                        txtToAmt.Text = Math.Round(res, (toCode == "KRW" || toCode == "JPY" || toCode == "VND") ? 2 : 4).ToString(CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    double val;
                    if (double.TryParse(txtToAmt.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                    {
                        double res = (val / rTo) * rFrom;
                        txtFromAmt.Text = Math.Round(res, (fromCode == "KRW" || fromCode == "JPY" || fromCode == "VND") ? 2 : 4).ToString(CultureInfo.InvariantCulture);
                    }
                }

                double unitRate = (1.0 / rFrom) * rTo;
                lblRateSummary.Text = string.Format("기준 환율: 1 {0} = {1:N4} {2}", fromCode, unitRate, toCode);

                // 하단 표 업데이트
                double baseAmt = 1.0;
                double.TryParse(txtFromAmt.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out baseAmt);
                double krwRate = exchangeRates["KRW"];

                lvPopularRates.BeginUpdate();
                lvPopularRates.Items.Clear();
                foreach (var kv in currencyNames)
                {
                    string code = kv.Key;
                    double r = exchangeRates[code];
                    double converted = (baseAmt / rFrom) * r;
                    string krwInfo = (code == "JPY" || code == "VND")
                        ? string.Format("100 {0} = {1:N2} KRW", code, (100.0 / r) * krwRate)
                        : string.Format("1 {0} = {1:N2} KRW", code, (1.0 / r) * krwRate);

                    ListViewItem lvi = new ListViewItem(code);
                    lvi.SubItems.Add(kv.Value);
                    lvi.SubItems.Add(string.Format("{0:N2} {1}", converted, code));
                    lvi.SubItems.Add(krwInfo);
                    lvPopularRates.Items.Add(lvi);
                }
                lvPopularRates.EndUpdate();
            }
            finally
            {
                suppressCurrencyEvent = false;
            }
        }

        private async Task FetchLiveRatesAsync()
        {
            lblRateStatus.Text = "🔄 실시간 환율 서버 조회 중...";
            lblRateStatus.ForeColor = Color.FromArgb(99, 102, 241);

            await Task.Run(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        string json = wc.DownloadString("https://open.er-api.com/v6/latest/USD");
                        var keys = new List<string>(exchangeRates.Keys);
                        foreach (string code in keys)
                        {
                            Match m = Regex.Match(json, "\"" + code + "\"\\s*:\\s*([0-9.]+)");
                            if (m.Success)
                            {
                                double val;
                                if (double.TryParse(m.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                                {
                                    exchangeRates[code] = val;
                                }
                            }
                        }
                    }

                    this.Invoke((Action)(() =>
                    {
                        lblRateStatus.Text = "● 실시간 환율 연동 완료 (업데이트: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")";
                        lblRateStatus.ForeColor = Color.FromArgb(16, 185, 129);
                        UpdateCurrencyConversion(true);
                    }));
                }
                catch
                {
                    this.Invoke((Action)(() =>
                    {
                        lblRateStatus.Text = "● 오프라인 기본 환율 모드 (인터넷 연결 시 실시간 갱신 가능)";
                        lblRateStatus.ForeColor = Color.FromArgb(245, 158, 11);
                        UpdateCurrencyConversion(true);
                    }));
                }
            });
        }

        // =====================================================
        // 3. 단위 변환 패널
        // =====================================================
        private class UnitDef
        {
            public string Code;
            public string Name;
            public double Factor;
            public UnitDef(string code, string name, double factor)
            {
                Code = code; Name = name; Factor = factor;
            }
            public override string ToString() { return Name; }
        }

        private Dictionary<string, List<UnitDef>> unitCategories = new Dictionary<string, List<UnitDef>>();

        private void BuildUnitPanel()
        {
            unitCategories["📏 길이 (Length)"] = new List<UnitDef>
            {
                new UnitDef("mm", "밀리미터 (mm)", 0.001),
                new UnitDef("cm", "센티미터 (cm)", 0.01),
                new UnitDef("m", "미터 (m)", 1.0),
                new UnitDef("km", "킬로미터 (km)", 1000.0),
                new UnitDef("in", "인치 (inch)", 0.0254),
                new UnitDef("ft", "피트 (ft)", 0.3048),
                new UnitDef("yd", "야드 (yd)", 0.9144),
                new UnitDef("mile", "마일 (mile)", 1609.344),
                new UnitDef("ja", "자 / 척 (尺)", 0.30303),
                new UnitDef("ri", "리 (里)", 392.727)
            };

            unitCategories["⚖️ 무게 (Weight)"] = new List<UnitDef>
            {
                new UnitDef("mg", "밀리그램 (mg)", 0.000001),
                new UnitDef("g", "그램 (g)", 0.001),
                new UnitDef("kg", "킬로그램 (kg)", 1.0),
                new UnitDef("t", "톤 (t)", 1000.0),
                new UnitDef("oz", "온스 (oz)", 0.02834952),
                new UnitDef("lb", "파운드 (lb)", 0.45359237),
                new UnitDef("geun", "근 (斤, 600g)", 0.6),
                new UnitDef("don", "돈 (錢, 3.75g)", 0.00375)
            };

            unitCategories["📐 넓이 / 평수 (Area)"] = new List<UnitDef>
            {
                new UnitDef("pyeong", "평 (坪, 3.3058m²)", 3.305785),
                new UnitDef("m2", "제곱미터 (m²)", 1.0),
                new UnitDef("cm2", "제곱센티미터 (cm²)", 0.0001),
                new UnitDef("ha", "헥타르 (ha)", 10000.0),
                new UnitDef("km2", "제곱킬로미터 (km²)", 1000000.0),
                new UnitDef("ft2", "제곱피트 (ft²)", 0.092903),
                new UnitDef("acre", "에이커 (acre)", 4046.856)
            };

            unitCategories["🧪 부피 (Volume)"] = new List<UnitDef>
            {
                new UnitDef("mL", "밀리리터 (mL / cc)", 0.001),
                new UnitDef("L", "리터 (L)", 1.0),
                new UnitDef("m3", "세제곱미터 (m³)", 1000.0),
                new UnitDef("cup", "컵 (US cup)", 0.236588),
                new UnitDef("gal", "갤런 (US gal)", 3.78541),
                new UnitDef("doe", "되 (약 1.8L)", 1.8039),
                new UnitDef("mal", "말 (약 18L)", 18.039)
            };

            unitCategories["🌡️ 온도 (Temperature)"] = new List<UnitDef>
            {
                new UnitDef("C", "섭씨 (°C)", 1.0),
                new UnitDef("F", "화씨 (°F)", 1.0),
                new UnitDef("K", "절대온도 (K)", 1.0)
            };

            unitCategories["💾 데이터 용량 (Data)"] = new List<UnitDef>
            {
                new UnitDef("B", "바이트 (B)", 1.0),
                new UnitDef("KB", "킬로바이트 (KB)", 1024.0),
                new UnitDef("MB", "메가바이트 (MB)", 1048576.0),
                new UnitDef("GB", "기가바이트 (GB)", 1073741824.0),
                new UnitDef("TB", "테라바이트 (TB)", 1099511627776.0)
            };

            unitCategories["🚀 속도 (Speed)"] = new List<UnitDef>
            {
                new UnitDef("kmh", "시속 (km/h)", 0.27777778),
                new UnitDef("ms", "초속 (m/s)", 1.0),
                new UnitDef("mph", "마일 시속 (mph)", 0.44704),
                new UnitDef("knot", "노트 (knot)", 0.514444),
                new UnitDef("mach", "마하 (Mach)", 340.29)
            };

            unitCategories["⏱️ 시간 (Time)"] = new List<UnitDef>
            {
                new UnitDef("sec", "초 (sec)", 1.0),
                new UnitDef("min", "분 (min)", 60.0),
                new UnitDef("hr", "시간 (hr)", 3600.0),
                new UnitDef("day", "일 (day)", 86400.0),
                new UnitDef("week", "주 (week)", 604800.0),
                new UnitDef("year", "년 (365일)", 31536000.0)
            };

            panelUnit = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = BgMain
            };

            Label lblCat = new Label
            {
                Text = "변환 카테고리 선택:",
                Font = new Font("Malgun Gothic", 10f, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(20, 124),
                AutoSize = true
            };

            cmbUnitCategory = new ComboBox
            {
                Location = new Point(165, 120),
                Size = new Size(260, 32),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextMain,
                Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold)
            };
            foreach (string k in unitCategories.Keys) cmbUnitCategory.Items.Add(k);

            Panel convCard = new Panel
            {
                Location = new Point(20, 162),
                Size = new Size(800, 115),
                BackColor = BgPanel
            };

            Label l1 = new Label { Text = "변환할 단위 (입력)", ForeColor = TextMuted, Location = new Point(18, 12), AutoSize = true };
            cmbFromUnit = new ComboBox
            {
                Location = new Point(18, 36),
                Size = new Size(320, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextMain,
                Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold)
            };
            txtFromUnit = new TextBox
            {
                Location = new Point(18, 72),
                Size = new Size(320, 32),
                Text = "1",
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Malgun Gothic", 13f, FontStyle.Bold)
            };

            Button btnSwapUnit = CreateCalcButton("⇄", 368, 48, 60, 44, Accent, Color.White, 14f);
            btnSwapUnit.Click += (s, e) =>
            {
                int idx = cmbFromUnit.SelectedIndex;
                cmbFromUnit.SelectedIndex = cmbToUnit.SelectedIndex;
                cmbToUnit.SelectedIndex = idx;
                UpdateUnitConversion(true);
            };

            Label l2 = new Label { Text = "변환 결과 단위", ForeColor = TextMuted, Location = new Point(458, 12), AutoSize = true };
            cmbToUnit = new ComboBox
            {
                Location = new Point(458, 36),
                Size = new Size(320, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgInput,
                ForeColor = TextMain,
                Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold)
            };
            txtToUnit = new TextBox
            {
                Location = new Point(458, 72),
                Size = new Size(320, 32),
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Malgun Gothic", 13f, FontStyle.Bold)
            };

            convCard.Controls.AddRange(new Control[] { l1, cmbFromUnit, txtFromUnit, btnSwapUnit, l2, cmbToUnit, txtToUnit });

            lblUnitSummary = new Label
            {
                Location = new Point(20, 286),
                Size = new Size(800, 34),
                BackColor = OpBg,
                ForeColor = Color.White,
                Font = new Font("Malgun Gothic", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            Label lblAllUnits = new Label
            {
                Text = "📊 카테고리 내 전체 단위 한눈에 비교 (더블클릭 시 값 복사)",
                Font = new Font("Malgun Gothic", 10f, FontStyle.Bold),
                ForeColor = TextMain,
                Location = new Point(20, 330),
                AutoSize = true
            };

            lvAllUnits = new ListView
            {
                Location = new Point(20, 358),
                Size = new Size(800, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = BgInput,
                ForeColor = TextMain,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Malgun Gothic", 10f)
            };
            lvAllUnits.Columns.Add("단위명", 350);
            lvAllUnits.Columns.Add("변환된 결과 값", 420);
            lvAllUnits.DoubleClick += (s, e) =>
            {
                if (lvAllUnits.SelectedItems.Count > 0)
                {
                    string v = lvAllUnits.SelectedItems[0].SubItems[1].Text;
                    Clipboard.SetText(v);
                    MessageBox.Show(v + " 값이 클립보드에 복사되었습니다.", "복사 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            cmbUnitCategory.SelectedIndexChanged += (s, e) =>
            {
                string cat = cmbUnitCategory.SelectedItem.ToString();
                cmbFromUnit.Items.Clear();
                cmbToUnit.Items.Clear();
                foreach (var u in unitCategories[cat])
                {
                    cmbFromUnit.Items.Add(u);
                    cmbToUnit.Items.Add(u);
                }
                cmbFromUnit.SelectedIndex = 0;
                cmbToUnit.SelectedIndex = cmbToUnit.Items.Count > 1 ? 1 : 0;
                UpdateUnitConversion(true);
            };

            cmbFromUnit.SelectedIndexChanged += (s, e) => UpdateUnitConversion(true);
            cmbToUnit.SelectedIndexChanged += (s, e) => UpdateUnitConversion(true);
            txtFromUnit.TextChanged += (s, e) => { if (!suppressUnitEvent) UpdateUnitConversion(true); };
            txtToUnit.TextChanged += (s, e) => { if (!suppressUnitEvent) UpdateUnitConversion(false); };

            panelUnit.Controls.Add(lblCat);
            panelUnit.Controls.Add(cmbUnitCategory);
            panelUnit.Controls.Add(convCard);
            panelUnit.Controls.Add(lblUnitSummary);
            panelUnit.Controls.Add(lblAllUnits);
            panelUnit.Controls.Add(lvAllUnits);

            this.Controls.Add(panelUnit);
            cmbUnitCategory.SelectedIndex = 0;
        }

        private double ConvertUnitValue(double val, UnitDef fromU, UnitDef toU, bool isTemp)
        {
            if (isTemp)
            {
                double c = val;
                if (fromU.Code == "F") c = (val - 32.0) * 5.0 / 9.0;
                else if (fromU.Code == "K") c = val - 273.15;

                if (toU.Code == "C") return c;
                if (toU.Code == "F") return c * 9.0 / 5.0 + 32.0;
                if (toU.Code == "K") return c + 273.15;
                return val;
            }
            return (val * fromU.Factor) / toU.Factor;
        }

        private void UpdateUnitConversion(bool forward)
        {
            if (cmbFromUnit.SelectedItem == null || cmbToUnit.SelectedItem == null) return;
            suppressUnitEvent = true;
            try
            {
                string cat = cmbUnitCategory.SelectedItem.ToString();
                bool isTemp = cat.Contains("온도");
                UnitDef fU = (UnitDef)cmbFromUnit.SelectedItem;
                UnitDef tU = (UnitDef)cmbToUnit.SelectedItem;

                if (forward)
                {
                    double v;
                    if (double.TryParse(txtFromUnit.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                    {
                        double res = ConvertUnitValue(v, fU, tU, isTemp);
                        txtToUnit.Text = Math.Round(res, 6).ToString(CultureInfo.InvariantCulture);
                    }
                }
                else
                {
                    double v;
                    if (double.TryParse(txtToUnit.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                    {
                        double res = ConvertUnitValue(v, tU, fU, isTemp);
                        txtFromUnit.Text = Math.Round(res, 6).ToString(CultureInfo.InvariantCulture);
                    }
                }

                double baseVal = 0;
                double.TryParse(txtFromUnit.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out baseVal);
                lblUnitSummary.Text = string.Format("{0} {1} = {2} {3}", txtFromUnit.Text, fU.Name, txtToUnit.Text, tU.Name);

                lvAllUnits.BeginUpdate();
                lvAllUnits.Items.Clear();
                foreach (var u in unitCategories[cat])
                {
                    double conv = ConvertUnitValue(baseVal, fU, u, isTemp);
                    ListViewItem lvi = new ListViewItem(u.Name);
                    lvi.SubItems.Add(Math.Round(conv, 6).ToString("G12", CultureInfo.InvariantCulture));
                    lvAllUnits.Items.Add(lvi);
                }
                lvAllUnits.EndUpdate();
            }
            finally
            {
                suppressUnitEvent = false;
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
