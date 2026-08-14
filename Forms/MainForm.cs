using System.Diagnostics;
using GovernmentMiningApp.Models;
using GovernmentMiningApp.Services;
using GovernmentMiningApp.Ui;

namespace GovernmentMiningApp.Forms;

public sealed class MainForm : Form
{
    private readonly DatabaseService _db;
    private readonly TrackingService _tracker;
    private readonly ReportService _reports;

    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(12) };
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private CancellationTokenSource? _scanCts;

    private DataGridView? _opsGrid;
    private DataGridView? _detGrid;
    private DataGridView? _persGrid;
    private ComboBox? _opFilter;
    private ComboBox? _statusFilter;
    private TextBox? _ipList;
    private CheckBox? _simMode;
    private TextBox? _scanRegion;
    private TextBox? _scanOpCode;

    public MainForm(DatabaseService db)
    {
        _db = db;
        _tracker = new TrackingService(db);
        _reports = new ReportService(db);
        _tracker.ProgressChanged += OnScanProgress;

        Text = "برنامه قانونی و مجوزدار سفارشی دولت — سیستم ردیابی ماینر";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 700);
        BackColor = UiTheme.Surface;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = UiTheme.BodyFont;
        StartPosition = FormStartPosition.CenterScreen;

        BuildChrome();
        ShowDashboard();
    }

    private void BuildChrome()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = UiTheme.Primary };
        var title = new Label
        {
            Text = "برنامه قانونی و مجوزدار سفارشی دولت  |  نسخه ۱.۰.۰",
            ForeColor = Color.White,
            Font = UiTheme.TitleFont,
            AutoSize = true,
            Location = new Point(16, 18)
        };
        var userLbl = new Label
        {
            Name = "UserLbl",
            ForeColor = Color.WhiteSmoke,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Location = new Point(16, 42)
        };
        userLbl.Text = AppSession.CurrentUser == null
            ? ""
            : $"کاربر: {AppSession.CurrentUser.FullName}  |  {AppSession.CurrentUser.Badge}  |  سطح {AppSession.CurrentUser.AuthorizationLevel}";
        top.Controls.Add(title);
        top.Controls.Add(userLbl);
        top.Resize += (_, _) =>
        {
            userLbl.Left = top.Width - userLbl.Width - 20;
            title.Left = 16;
        };

        var nav = new Panel { Dock = DockStyle.Right, Width = 210, BackColor = UiTheme.PrimaryDark, Padding = new Padding(8) };
        string[] items =
        {
            "داشبورد", "عملیات", "شناسایی‌ها", "اسکن مجاز", "گزارش‌ها", "پرسنل", "تنظیمات"
        };
        int y = 12;
        foreach (var item in items)
        {
            var btn = new Button
            {
                Text = item,
                Width = 190,
                Height = 42,
                Location = new Point(8, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 50, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) => Navigate(item);
            nav.Controls.Add(btn);
            y += 50;
        }

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(40, 45, 50) };
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = Color.LimeGreen;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Padding = new Padding(8, 0, 8, 0);
        _status.Text = "✓ سامانه آماده است";
        _progress.Dock = DockStyle.Left;
        _progress.Width = 280;
        _progress.Style = ProgressBarStyle.Continuous;
        bottom.Controls.Add(_status);
        bottom.Controls.Add(_progress);

        Controls.Add(_content);
        Controls.Add(nav);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private void Navigate(string item)
    {
        switch (item)
        {
            case "داشبورد": ShowDashboard(); break;
            case "عملیات": ShowOperations(); break;
            case "شناسایی‌ها": ShowDetections(); break;
            case "اسکن مجاز": ShowScan(); break;
            case "گزارش‌ها": ShowReports(); break;
            case "پرسنل": ShowPersonnel(); break;
            case "تنظیمات": ShowSettings(); break;
        }
    }

    private void ClearContent()
    {
        _content.Controls.Clear();
        _opsGrid = null;
        _detGrid = null;
        _persGrid = null;
    }

    private void ShowDashboard()
    {
        ClearContent();
        var stats = _db.GetDashboardStats();

        var header = new Label
        {
            Text = "داشبورد عملیاتی",
            Dock = DockStyle.Top,
            Height = 36,
            Font = UiTheme.TitleFont,
            ForeColor = UiTheme.Primary,
            TextAlign = ContentAlignment.MiddleRight
        };

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 120,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 8)
        };
        cards.Controls.Add(StatCard("عملیات فعال", stats.ActiveOperations.ToString(), UiTheme.Accent));
        cards.Controls.Add(StatCard("کل شناسایی‌ها", stats.TotalDetections.ToString(), UiTheme.Primary));
        cards.Controls.Add(StatCard("منتظر اقدام", stats.PendingActions.ToString(), UiTheme.Warning));
        cards.Controls.Add(StatCard("ضبط‌شده", stats.SeizedDevices.ToString(), UiTheme.Danger));
        cards.Controls.Add(StatCard("مصرف (کیلووات)", stats.TotalPowerKw.ToString("F1"), Color.FromArgb(80, 60, 140)));
        cards.Controls.Add(StatCard("پرسنل فعال", stats.PersonnelCount.ToString(), Color.FromArgb(60, 100, 140)));

        var regional = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(regional);
        regional.Columns.Add("Province", "استان");
        regional.Columns.Add("Count", "تعداد");
        regional.Columns.Add("Power", "مصرف (W)");
        foreach (var (p, c, w) in _db.GetRegionalStats())
            regional.Rows.Add(p, c, w.ToString("F0"));

        var regTitle = new Label
        {
            Text = "آمار منطقه‌ای",
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiTheme.HeaderFont,
            ForeColor = UiTheme.Primary,
            TextAlign = ContentAlignment.MiddleRight
        };

        _content.Controls.Add(regional);
        _content.Controls.Add(regTitle);
        _content.Controls.Add(cards);
        _content.Controls.Add(header);
        SetStatus("داشبورد به‌روز شد");
    }

    private static Panel StatCard(string title, string value, Color accent)
    {
        var p = new Panel
        {
            Width = 150,
            Height = 100,
            Margin = new Padding(6),
            BackColor = Color.White,
            Padding = new Padding(10)
        };
        p.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(accent);
            e.Graphics.FillRectangle(brush, 0, 0, 5, p.Height);
            using var pen = new Pen(Color.FromArgb(220, 225, 230));
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        var v = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = accent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var t = new Label
        {
            Text = title,
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = UiTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter
        };
        p.Controls.Add(v);
        p.Controls.Add(t);
        return p;
    }

    private void ShowOperations()
    {
        ClearContent();
        var topBar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var addBtn = new Button { Text = "عملیات جدید", Width = 130, Height = 36, Left = 8, Top = 6 };
        var closeBtn = new Button { Text = "بستن عملیات", Width = 130, Height = 36, Left = 150, Top = 6 };
        var refreshBtn = new Button { Text = "بازنشانی", Width = 100, Height = 36, Left = 290, Top = 6 };
        UiTheme.StylePrimaryButton(addBtn);
        UiTheme.StyleDangerButton(closeBtn);
        UiTheme.StyleSecondaryButton(refreshBtn);
        addBtn.Click += (_, _) => CreateOperationDialog();
        closeBtn.Click += (_, _) => CloseSelectedOperation();
        refreshBtn.Click += (_, _) => LoadOperationsGrid();
        topBar.Controls.AddRange(new Control[] { addBtn, closeBtn, refreshBtn });

        _opsGrid = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(_opsGrid);
        _opsGrid.Columns.Add("Code", "کد عملیات");
        _opsGrid.Columns.Add("Region", "منطقه");
        _opsGrid.Columns.Add("Auth", "مجاز‌کننده");
        _opsGrid.Columns.Add("Start", "شروع");
        _opsGrid.Columns.Add("Status", "وضعیت");
        _opsGrid.Columns.Add("Count", "شناسایی");
        _opsGrid.Columns.Add("Power", "مصرف W");
        _opsGrid.Columns.Add("Notes", "یادداشت");

        var title = PageTitle("مدیریت عملیات دولتی");
        _content.Controls.Add(_opsGrid);
        _content.Controls.Add(topBar);
        _content.Controls.Add(title);
        LoadOperationsGrid();
    }

    private void LoadOperationsGrid()
    {
        if (_opsGrid == null) return;
        _opsGrid.Rows.Clear();
        foreach (var o in _db.GetOperations())
        {
            _opsGrid.Rows.Add(o.OperationCode, o.Region, o.AuthorizedBy,
                o.StartTime.ToString("yyyy-MM-dd HH:mm"), o.Status,
                o.DetectionCount, o.TotalConsumption.ToString("F0"), o.Notes);
        }
        SetStatus($"تعداد عملیات: {_opsGrid.Rows.Count}");
    }

    private void CreateOperationDialog()
    {
        using var dlg = new Form
        {
            Text = "ایجاد عملیات جدید",
            ClientSize = new Size(420, 280),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var code = new TextBox { PlaceholderText = "کد عملیات (مثال OP-1404-01)", Dock = DockStyle.Top, Height = 30 };
        var region = new TextBox { PlaceholderText = "منطقه / استان", Dock = DockStyle.Top, Height = 30 };
        var notes = new TextBox { PlaceholderText = "یادداشت", Dock = DockStyle.Top, Height = 60, Multiline = true };
        var ok = new Button { Text = "ثبت", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 40 };
        UiTheme.StylePrimaryButton(ok);
        dlg.Controls.Add(ok);
        dlg.Controls.Add(notes);
        dlg.Controls.Add(region);
        dlg.Controls.Add(code);
        dlg.Controls.Add(new Label { Text = "کد، منطقه و یادداشت را وارد کنید", Dock = DockStyle.Top, Height = 28 });
        dlg.AcceptButton = ok;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(region.Text))
        {
            MessageBox.Show("کد و منطقه الزامی است.", "خطا", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            _db.CreateOperation(new GovernmentOperation
            {
                OperationCode = code.Text.Trim(),
                Region = region.Text.Trim(),
                AuthorizedBy = AppSession.CurrentUser?.FullName ?? "نامشخص",
                StartTime = DateTime.Now,
                Status = "InProgress",
                Notes = notes.Text.Trim()
            });
            LoadOperationsGrid();
            SetStatus($"عملیات {code.Text.Trim()} ایجاد شد");
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطا در ثبت: " + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CloseSelectedOperation()
    {
        if (_opsGrid?.CurrentRow == null) return;
        var code = _opsGrid.CurrentRow.Cells[0].Value?.ToString();
        if (string.IsNullOrEmpty(code)) return;
        if (MessageBox.Show($"عملیات {code} بسته شود؟", "تأیید", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _db.UpdateOperationStatus(code, "Completed", DateTime.Now);
        LoadOperationsGrid();
    }

    private void ShowDetections()
    {
        ClearContent();
        var filterBar = new Panel { Dock = DockStyle.Top, Height = 48 };
        _opFilter = new ComboBox { Width = 180, Left = 8, Top = 10, DropDownStyle = ComboBoxStyle.DropDownList };
        _statusFilter = new ComboBox { Width = 160, Left = 200, Top = 10, DropDownStyle = ComboBoxStyle.DropDownList };
        _opFilter.Items.Add("همه عملیات");
        foreach (var o in _db.GetOperations()) _opFilter.Items.Add(o.OperationCode);
        _opFilter.SelectedIndex = 0;
        _statusFilter.Items.AddRange(new object[] { "همه", "منتظر‌دستورالعمل", "ضبط‌شده", "قطع‌برق", "اخطار", "بایگانی" });
        _statusFilter.SelectedIndex = 0;

        var refresh = new Button { Text = "اعمال فیلتر", Width = 100, Left = 380, Top = 8 };
        var actSeize = new Button { Text = "ثبت ضبط", Width = 100, Left = 490, Top = 8 };
        var actWarn = new Button { Text = "اخطار", Width = 90, Left = 600, Top = 8 };
        var actCut = new Button { Text = "قطع برق", Width = 90, Left = 700, Top = 8 };
        UiTheme.StyleSecondaryButton(refresh);
        UiTheme.StyleDangerButton(actSeize);
        UiTheme.StylePrimaryButton(actWarn);
        UiTheme.StylePrimaryButton(actCut);
        actCut.BackColor = UiTheme.Warning;

        refresh.Click += (_, _) => LoadDetectionsGrid();
        actSeize.Click += (_, _) => Enforce("ضبط‌شده");
        actWarn.Click += (_, _) => Enforce("اخطار");
        actCut.Click += (_, _) => Enforce("قطع‌برق");

        filterBar.Controls.AddRange(new Control[] { _opFilter, _statusFilter, refresh, actSeize, actWarn, actCut });

        _detGrid = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(_detGrid);
        string[] cols = { "ID", "کدعملیات", "IP", "پورت", "مدل", "مصرفW", "استان", "شهر", "مشترک", "تلفن", "ISP", "وضعیت", "اطمینان", "زمان" };
        foreach (var c in cols) _detGrid.Columns.Add(c, c);
        _detGrid.Columns[0].Visible = false;

        _content.Controls.Add(_detGrid);
        _content.Controls.Add(filterBar);
        _content.Controls.Add(PageTitle("فهرست دستگاه‌های شناسایی‌شده"));
        LoadDetectionsGrid();
    }

    private void LoadDetectionsGrid()
    {
        if (_detGrid == null) return;
        _detGrid.Rows.Clear();
        string? op = _opFilter?.SelectedItem?.ToString();
        if (op == "همه عملیات") op = null;
        string? st = _statusFilter?.SelectedItem?.ToString();
        foreach (var d in _db.GetDetections(op, st))
        {
            _detGrid.Rows.Add(d.OperationID, d.OperationCode, d.IPAddress, d.Port, d.DeviceModel,
                d.EstimatedConsumption.ToString("F0"), d.Province, d.City, d.SubscriberName,
                d.PhoneNumber, d.ISP, d.ActionStatus, d.Confidence.ToString("P0"),
                d.DetectionTime.ToString("yyyy-MM-dd HH:mm"));
        }
        SetStatus($"شناسایی‌ها: {_detGrid.Rows.Count}");
    }

    private void Enforce(string action)
    {
        if (_detGrid?.CurrentRow == null)
        {
            MessageBox.Show("یک ردیف انتخاب کنید.", "توجه");
            return;
        }
        var id = _detGrid.CurrentRow.Cells[0].Value?.ToString();
        if (string.IsNullOrEmpty(id)) return;
        var notes = "";
        using (var input = new Form
        {
            Text = "یادداشت اقدام",
            ClientSize = new Size(400, 160),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        })
        {
            var tb = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            var ok = new Button { Text = "ثبت", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
            UiTheme.StylePrimaryButton(ok);
            input.Controls.Add(tb);
            input.Controls.Add(ok);
            if (input.ShowDialog(this) != DialogResult.OK) return;
            notes = tb.Text;
        }
        _db.RecordEnforcement(id, action, AppSession.CurrentUser?.FullName ?? "سیستم", notes);
        LoadDetectionsGrid();
        SetStatus($"اقدام «{action}» ثبت شد");
    }

    private void ShowScan()
    {
        ClearContent();
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 320,
            ColumnCount = 2,
            RowCount = 6,
            RightToLeft = RightToLeft.Yes
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _scanOpCode = new TextBox { Dock = DockStyle.Fill };
        _scanRegion = new TextBox { Dock = DockStyle.Fill, Text = "تهران" };
        _ipList = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 100 };
        _ipList.PlaceholderText = "هر خط یک IP (فقط اهداف مجاز و دارای حکم)";
        _simMode = new CheckBox { Text = "حالت شبیه‌سازی آموزشی (بدون اسکن شبکه واقعی)", Checked = true, AutoSize = true };

        var ops = _db.GetOperations().Where(o => o.Status == "InProgress").Select(o => o.OperationCode).ToList();
        if (ops.Count > 0) _scanOpCode.Text = ops[0];
        else _scanOpCode.Text = "OP-DEMO-001";

        form.Controls.Add(new Label { Text = "کد عملیات", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
        form.Controls.Add(_scanOpCode, 1, 0);
        form.Controls.Add(new Label { Text = "منطقه", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
        form.Controls.Add(_scanRegion, 1, 1);
        form.Controls.Add(new Label { Text = "IPهای مجاز", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
        form.Controls.Add(_ipList, 1, 2);
        form.SetRowSpan(_ipList, 2);
        form.Controls.Add(_simMode, 1, 4);

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.RightToLeft };
        var startBtn = new Button { Text = "شروع اسکن", Width = 140 };
        var localBtn = new Button { Text = "پروب محلی سیستم", Width = 160 };
        var stopBtn = new Button { Text = "توقف", Width = 100 };
        UiTheme.StylePrimaryButton(startBtn);
        UiTheme.StyleSecondaryButton(localBtn);
        UiTheme.StyleDangerButton(stopBtn);
        startBtn.Click += async (_, _) => await StartScanAsync(false);
        localBtn.Click += async (_, _) => await StartLocalProbeAsync();
        stopBtn.Click += (_, _) => _scanCts?.Cancel();
        btnRow.Controls.Add(startBtn);
        btnRow.Controls.Add(localBtn);
        btnRow.Controls.Add(stopBtn);

        var notice = new Label
        {
            Dock = DockStyle.Top,
            Height = 70,
            ForeColor = UiTheme.Danger,
            Font = new Font("Segoe UI", 9f),
            Text = "هشدار قانونی: اسکن واقعی فقط روی اهدافی مجاز است که حکم/مجوز رسمی دارند.\n" +
                   "حالت شبیه‌سازی برای آموزش و تست بدون اتصال به شبکه خارجی است.\n" +
                   "پروب محلی فقط آدرس‌های IP همین رایانه را بررسی می‌کند."
        };

        var log = new TextBox
        {
            Name = "ScanLog",
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9f)
        };

        panel.Controls.Add(log);
        panel.Controls.Add(btnRow);
        panel.Controls.Add(notice);
        panel.Controls.Add(form);
        _content.Controls.Add(panel);
        _content.Controls.Add(PageTitle("اسکن مجاز — ردیابی دستگاه"));
    }

    private async Task StartScanAsync(bool forceLocal)
    {
        if (_scanOpCode == null || _scanRegion == null || _ipList == null || _simMode == null) return;
        var op = _scanOpCode.Text.Trim();
        var region = _scanRegion.Text.Trim();
        if (string.IsNullOrWhiteSpace(op))
        {
            MessageBox.Show("کد عملیات را وارد کنید.", "خطا");
            return;
        }

        // Ensure operation exists
        if (_db.GetOperations().All(o => o.OperationCode != op))
        {
            _db.CreateOperation(new GovernmentOperation
            {
                OperationCode = op,
                Region = region,
                AuthorizedBy = AppSession.CurrentUser?.FullName ?? "سیستم",
                StartTime = DateTime.Now,
                Status = "InProgress",
                Notes = "ایجاد خودکار از صفحه اسکن"
            });
        }

        var ips = _ipList.Lines.Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")).ToList();
        if (!_simMode.Checked && ips.Count == 0)
        {
            MessageBox.Show("برای اسکن واقعی حداقل یک IP مجاز وارد کنید یا حالت شبیه‌سازی را فعال کنید.", "توجه");
            return;
        }

        if (!_simMode.Checked)
        {
            var confirm = MessageBox.Show(
                "تأیید می‌کنید اسکن فقط روی اهداف دارای مجوز قانونی انجام می‌شود؟",
                "تأیید قانونی", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
        }

        _scanCts = new CancellationTokenSource();
        AppendScanLog($"شروع اسکن — عملیات {op} — منطقه {region} — شبیه‌سازی={_simMode.Checked}");
        try
        {
            var results = await _tracker.RunAuthorizedScanAsync(
                op, region, ips, Array.Empty<int>(), _simMode.Checked,
                AppSession.CurrentUser?.FullName ?? "سیستم", _scanCts.Token);
            AppendScanLog($"پایان: {results.Count} دستگاه ثبت شد.");
            MessageBox.Show($"اسکن تکمیل شد.\nتعداد شناسایی: {results.Count}", "نتیجه", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendScanLog("اسکن متوقف شد.");
        }
        catch (Exception ex)
        {
            AppendScanLog("خطا: " + ex.Message);
            MessageBox.Show(ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StartLocalProbeAsync()
    {
        if (_scanOpCode == null || _scanRegion == null) return;
        var op = _scanOpCode.Text.Trim();
        var region = _scanRegion.Text.Trim();
        if (string.IsNullOrWhiteSpace(op))
        {
            MessageBox.Show("کد عملیات را وارد کنید.", "خطا");
            return;
        }
        if (_db.GetOperations().All(o => o.OperationCode != op))
        {
            _db.CreateOperation(new GovernmentOperation
            {
                OperationCode = op,
                Region = region,
                AuthorizedBy = AppSession.CurrentUser?.FullName ?? "سیستم",
                StartTime = DateTime.Now,
                Status = "InProgress",
                Notes = "پروب محلی"
            });
        }
        _scanCts = new CancellationTokenSource();
        AppendScanLog("شروع پروب محلی...");
        try
        {
            var results = await _tracker.QuickLocalProbeAsync(op, region, _scanCts.Token);
            AppendScanLog($"پروب محلی: {results.Count} پورت باز مرتبط یافت شد.");
            MessageBox.Show($"پروب محلی تمام شد.\nیافته‌ها: {results.Count}", "نتیجه");
        }
        catch (Exception ex)
        {
            AppendScanLog(ex.Message);
        }
    }

    private void AppendScanLog(string msg)
    {
        var log = _content.Controls.Find("ScanLog", true).FirstOrDefault() as TextBox;
        if (log == null) return;
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
    }

    private void OnScanProgress(object? sender, ScanProgressEventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnScanProgress(sender, e));
            return;
        }
        _progress.Value = Math.Min(100, Math.Max(0, e.Percent));
        SetStatus($"{e.Message} | اسکن‌شده: {e.Scanned} | یافته: {e.Found}");
        AppendScanLog(e.Message);
    }

    private void ShowReports()
    {
        ClearContent();
        var bar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var opBox = new ComboBox { Width = 200, Left = 8, Top = 10, DropDownStyle = ComboBoxStyle.DropDownList };
        opBox.Items.Add("همه");
        foreach (var o in _db.GetOperations()) opBox.Items.Add(o.OperationCode);
        opBox.SelectedIndex = 0;
        var txtBtn = new Button { Text = "گزارش متنی", Width = 120, Left = 220, Top = 8 };
        var csvBtn = new Button { Text = "خروجی CSV", Width = 120, Left = 350, Top = 8 };
        var openBtn = new Button { Text = "باز کردن پوشه گزارش‌ها", Width = 160, Left = 480, Top = 8 };
        UiTheme.StylePrimaryButton(txtBtn);
        UiTheme.StyleSecondaryButton(csvBtn);
        UiTheme.StyleSecondaryButton(openBtn);

        txtBtn.Click += (_, _) =>
        {
            var code = opBox.SelectedItem?.ToString();
            if (code == "همه") code = null;
            var path = _reports.GenerateTextReport(code, AppSession.CurrentUser?.FullName ?? "سیستم");
            MessageBox.Show("گزارش ذخیره شد:\n" + path, "موفق");
            SetStatus("گزارش متنی تولید شد");
        };
        csvBtn.Click += (_, _) =>
        {
            var code = opBox.SelectedItem?.ToString();
            if (code == "همه") code = null;
            var path = _reports.ExportCsv(code, AppSession.CurrentUser?.FullName ?? "سیستم");
            MessageBox.Show("CSV ذخیره شد:\n" + path, "موفق");
        };
        openBtn.Click += (_, _) =>
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        };

        bar.Controls.AddRange(new Control[] { opBox, txtBtn, csvBtn, openBtn });
        var info = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopRight,
            Padding = new Padding(12),
            Font = new Font("Segoe UI", 10f),
            Text = "از این بخش می‌توانید گزارش رسمی متنی یا خروجی CSV برای پیگیری اداری تولید کنید.\n" +
                   "گزارش‌ها در پوشه Reports کنار فایل اجرایی ذخیره می‌شوند و در پایگاه داده نیز ثبت می‌گردند."
        };
        _content.Controls.Add(info);
        _content.Controls.Add(bar);
        _content.Controls.Add(PageTitle("تولید گزارش رسمی"));
    }

    private void ShowPersonnel()
    {
        ClearContent();
        var bar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var add = new Button { Text = "افزودن / ویرایش", Width = 140, Left = 8, Top = 8 };
        var refresh = new Button { Text = "بازنشانی", Width = 100, Left = 160, Top = 8 };
        UiTheme.StylePrimaryButton(add);
        UiTheme.StyleSecondaryButton(refresh);
        add.Click += (_, _) => EditPersonnelDialog();
        refresh.Click += (_, _) => LoadPersonnelGrid();
        bar.Controls.Add(add);
        bar.Controls.Add(refresh);

        _persGrid = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(_persGrid);
        foreach (var c in new[] { "ID", "نام", "بخش", "نقش", "Badge", "تلفن", "ایمیل", "سطح", "فعال", "آخرین ورود" })
            _persGrid.Columns.Add(c, c);
        _persGrid.Columns[0].Visible = false;

        _content.Controls.Add(_persGrid);
        _content.Controls.Add(bar);
        _content.Controls.Add(PageTitle("مدیریت پرسنل مجاز"));
        LoadPersonnelGrid();
    }

    private void LoadPersonnelGrid()
    {
        if (_persGrid == null) return;
        _persGrid.Rows.Clear();
        foreach (var p in _db.GetPersonnel())
        {
            _persGrid.Rows.Add(p.PersonnelID, p.FullName, p.Department, p.Role, p.Badge,
                p.PhoneNumber, p.Email, p.AuthorizationLevel, p.IsActive ? "بله" : "خیر",
                p.LastLogin?.ToString("yyyy-MM-dd HH:mm") ?? "-");
        }
    }

    private void EditPersonnelDialog()
    {
        Personnel? existing = null;
        if (_persGrid?.CurrentRow != null)
        {
            var id = _persGrid.CurrentRow.Cells[0].Value?.ToString();
            existing = _db.GetPersonnel().FirstOrDefault(p => p.PersonnelID == id);
        }

        using var dlg = new Form
        {
            Text = "پرسنل",
            ClientSize = new Size(440, 420),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };
        var name = Field(dlg, "نام کامل", existing?.FullName ?? "", 10);
        var dept = Field(dlg, "بخش", existing?.Department ?? "", 50);
        var role = Field(dlg, "نقش", existing?.Role ?? "", 90);
        var badge = Field(dlg, "Badge", existing?.Badge ?? "", 130);
        var phone = Field(dlg, "تلفن", existing?.PhoneNumber ?? "", 170);
        var email = Field(dlg, "ایمیل", existing?.Email ?? "", 210);
        var level = Field(dlg, "سطح (1-5)", (existing?.AuthorizationLevel ?? 1).ToString(), 250);
        var pass = Field(dlg, "رمز (خالی=بدون تغییر)", "", 290);
        pass.UseSystemPasswordChar = true;
        var ok = new Button { Text = "ذخیره", Left = 20, Top = 340, Width = 120, DialogResult = DialogResult.OK };
        UiTheme.StylePrimaryButton(ok);
        dlg.Controls.Add(ok);
        dlg.AcceptButton = ok;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var p = existing ?? new Personnel();
        p.FullName = name.Text.Trim();
        p.Department = dept.Text.Trim();
        p.Role = role.Text.Trim();
        p.Badge = badge.Text.Trim();
        p.PhoneNumber = phone.Text.Trim();
        p.Email = email.Text.Trim();
        p.AuthorizationLevel = int.TryParse(level.Text, out var lv) ? Math.Clamp(lv, 1, 5) : 1;
        p.IsActive = true;
        if (string.IsNullOrWhiteSpace(p.FullName) || string.IsNullOrWhiteSpace(p.Badge))
        {
            MessageBox.Show("نام و Badge الزامی است.");
            return;
        }
        try
        {
            _db.UpsertPersonnel(p, string.IsNullOrWhiteSpace(pass.Text) ? null : pass.Text);
            LoadPersonnelGrid();
            SetStatus("پرسنل ذخیره شد");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطا");
        }
    }

    private static TextBox Field(Form dlg, string label, string value, int top)
    {
        dlg.Controls.Add(new Label { Text = label, Left = 20, Top = top, Width = 160, TextAlign = ContentAlignment.MiddleRight });
        var tb = new TextBox { Left = 190, Top = top, Width = 220, Text = value };
        dlg.Controls.Add(tb);
        return tb;
    }

    private void ShowSettings()
    {
        ClearContent();
        var info = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            Font = new Font("Segoe UI", 10f),
            BackColor = Color.White,
            Text =
                "برنامه قانونی و مجوزدار سفارشی دولت\r\n" +
                "نسخه: 1.0.0\r\n" +
                "پلتفرم: Windows Forms / .NET 8\r\n\r\n" +
                $"مسیر پایگاه داده:\r\n{_db.DatabasePath}\r\n\r\n" +
                $"مسیر اجرا:\r\n{AppDomain.CurrentDomain.BaseDirectory}\r\n\r\n" +
                "قابلیت‌ها:\r\n" +
                "• مدیریت عملیات دولتی با کد یکتا\r\n" +
                "• ثبت و پیگیری دستگاه‌های شناسایی‌شده\r\n" +
                "• اسکن مجاز روی IPهای دارای مجوز + حالت شبیه‌سازی آموزشی\r\n" +
                "• ثبت اقدامات اجرایی (ضبط، اخطار، قطع برق)\r\n" +
                "• گزارش متنی و CSV\r\n" +
                "• مدیریت پرسنل مجاز با سطح دسترسی\r\n" +
                "• پایگاه داده SQLite محلی\r\n\r\n" +
                "ورود پیش‌فرض: ADMIN-001 / Admin@123\r\n"
        };
        _content.Controls.Add(info);
        _content.Controls.Add(PageTitle("تنظیمات و درباره برنامه"));
    }

    private static Label PageTitle(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        Height = 40,
        Font = UiTheme.TitleFont,
        ForeColor = UiTheme.Primary,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(0, 0, 8, 0)
    };

    private void SetStatus(string msg)
    {
        _status.Text = "✓ " + msg;
    }
}
