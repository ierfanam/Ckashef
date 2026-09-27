using System.Diagnostics;
using System.Globalization;
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
    private DataGridView? _scanGrid;
    private DataGridView? _auditGrid;
    private ComboBox? _opFilter;
    private ComboBox? _statusFilter;

    private TextBox? _scanOpCode;
    private TextBox? _scanPermit;
    private TextBox? _scanRegion;
    private TextBox? _scanTargets;
    private TextBox? _scanPorts;
    private NumericUpDown? _scanParallel;
    private NumericUpDown? _scanTimeout;
    private CheckBox? _scanDefaultPorts;
    private CheckBox? _scanIntel;
    private CheckBox? _scanArp;

    public MainForm(DatabaseService db)
    {
        _db = db;
        _tracker = new TrackingService(db);
        _reports = new ReportService(db);
        _tracker.ProgressChanged += OnScanProgress;

        Text = "سامانه ردیابی دستگاه‌های ماینر — نسخه واقعی‌سنج";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 720);
        BackColor = UiTheme.Surface;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = UiTheme.BodyFont;
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += (_, _) => _scanCts?.Cancel();

        BuildChrome();
        ShowDashboard();
    }

    private int CurrentLevel => AppSession.CurrentUser?.AuthorizationLevel ?? 0;

    private bool RequireLevel(int level, string action)
    {
        if (CurrentLevel >= level) return true;
        MessageBox.Show($"سطح دسترسی شما برای «{action}» کافی نیست. حداقل سطح مورد نیاز: {level}",
            "عدم دسترسی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void BuildChrome()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = UiTheme.Primary };
        var title = new Label
        {
            Text = "سامانه ردیابی دستگاه‌های ماینر  |  " + AppVersion(),
            ForeColor = Color.White,
            Font = UiTheme.TitleFont,
            AutoSize = true,
            Location = new Point(16, 10)
        };
        var userLbl = new Label
        {
            ForeColor = Color.WhiteSmoke,
            AutoSize = true,
            Location = new Point(16, 38)
        };
        userLbl.Text = AppSession.CurrentUser == null
            ? ""
            : $"کاربر: {AppSession.CurrentUser.FullName} | {AppSession.CurrentUser.Badge} | سطح {AppSession.CurrentUser.AuthorizationLevel}";
        top.Controls.Add(title);
        top.Controls.Add(userLbl);
        top.Resize += (_, _) => userLbl.Left = Math.Max(16, top.Width - userLbl.Width - 20);

        var nav = new Panel { Dock = DockStyle.Right, Width = 200, BackColor = UiTheme.PrimaryDark, Padding = new Padding(8) };
        string[] items =
        {
            "داشبورد", "عملیات", "شناسایی‌ها", "پروب مجاز", "تاریخچه پروب",
            "گزارش‌ها", "پرسنل", "حسابرسی", "تنظیمات"
        };
        int y = 10;
        foreach (var item in items)
        {
            var btn = new Button
            {
                Text = item,
                Width = 180,
                Height = 38,
                Location = new Point(8, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 50, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (_, _) => Navigate(item);
            nav.Controls.Add(btn);
            y += 44;
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
            case "پروب مجاز": ShowScan(); break;
            case "تاریخچه پروب": ShowScanHistory(); break;
            case "گزارش‌ها": ShowReports(); break;
            case "پرسنل": ShowPersonnel(); break;
            case "حسابرسی": ShowAudit(); break;
            case "تنظیمات": ShowSettings(); break;
        }
    }

    private void ClearContent()
    {
        _content.Controls.Clear();
        _opsGrid = null;
        _detGrid = null;
        _persGrid = null;
        _scanGrid = null;
        _auditGrid = null;
    }

    private static string AppVersion() =>
        "نسخه " + (Application.ProductVersion ?? "2.0.0");

    // ─────────────────────────── داشبورد ───────────────────────────

    private void ShowDashboard()
    {
        ClearContent();
        var stats = _db.GetDashboardStats();

        var header = new Label
        {
            Text = "داشبورد عملیاتی — همه اعداد بر پایه داده‌های واقعی ثبت‌شده",
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
        cards.Controls.Add(StatCard("شناسایی قطعی", stats.IdentifiedDetections.ToString(), UiTheme.Accent));
        cards.Controls.Add(StatCard("پورت باز ناشناخته", stats.UnidentifiedOpenPorts.ToString(), UiTheme.Warning));
        cards.Controls.Add(StatCard("منتظر اقدام", stats.PendingActions.ToString(), UiTheme.Warning));
        cards.Controls.Add(StatCard("ضبط‌شده", stats.SeizedDevices.ToString(), UiTheme.Danger));
        cards.Controls.Add(StatCard("توان اندازه‌گیری‌شده (kW)", stats.MeasuredPowerKw.ToString("F2"),
            Color.FromArgb(80, 60, 140)));
        cards.Controls.Add(StatCard("تعداد پروب", stats.TotalScans.ToString(), Color.FromArgb(60, 100, 140)));
        cards.Controls.Add(StatCard("پرسنل فعال", stats.PersonnelCount.ToString(), Color.FromArgb(60, 100, 140)));

        var regional = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(regional);
        regional.Columns.Add("Province", "استان (بر پایه پاسخ اپراتور)");
        regional.Columns.Add("Count", "تعداد دستگاه");
        regional.Columns.Add("Power", "توان اندازه‌گیری‌شده (W)");
        var regionalStats = _db.GetRegionalStats();
        if (regionalStats.Count == 0)
            regional.Rows.Add("داده‌ای ثبت نشده است", "—", "—");
        else
            foreach (var (p, c, w) in regionalStats)
                regional.Rows.Add(p, c, w.ToString("F1"));

        var regTitle = new Label
        {
            Text = "آمار استانی",
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
        SetStatus($"دستگاه‌های دارای توان اندازه‌گیری‌شده: {stats.MeasuredPowerDevices}");
    }

    private static Panel StatCard(string title, string value, Color accent)
    {
        var p = new Panel
        {
            Width = 160,
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
            Font = new Font("Segoe UI", 17f, FontStyle.Bold),
            ForeColor = accent,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var t = new Label
        {
            Text = title,
            Dock = DockStyle.Bottom,
            Height = 30,
            ForeColor = UiTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter
        };
        p.Controls.Add(v);
        p.Controls.Add(t);
        return p;
    }

    // ─────────────────────────── عملیات ───────────────────────────

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
        _opsGrid.Columns.Add("Permit", "شماره حکم/مجوز");
        _opsGrid.Columns.Add("Auth", "مجازکننده");
        _opsGrid.Columns.Add("Start", "شروع");
        _opsGrid.Columns.Add("Status", "وضعیت");
        _opsGrid.Columns.Add("Count", "شناسایی");
        _opsGrid.Columns.Add("Power", "توان اندازه‌گیری‌شده W");
        _opsGrid.Columns.Add("Notes", "یادداشت");

        _content.Controls.Add(_opsGrid);
        _content.Controls.Add(topBar);
        _content.Controls.Add(PageTitle("مدیریت عملیات"));
        LoadOperationsGrid();
    }

    private void LoadOperationsGrid()
    {
        if (_opsGrid == null) return;
        _opsGrid.Rows.Clear();
        foreach (var o in _db.GetOperations())
        {
            _opsGrid.Rows.Add(o.OperationCode, o.Region, o.PermitNumber, o.AuthorizedBy,
                o.StartTime.ToString("yyyy-MM-dd HH:mm"), o.Status,
                o.DetectionCount, o.TotalConsumption.ToString("F1"), o.Notes);
        }
        SetStatus($"تعداد عملیات: {_opsGrid.Rows.Count}");
    }

    private void CreateOperationDialog()
    {
        using var dlg = new Form
        {
            Text = "ایجاد عملیات جدید",
            ClientSize = new Size(460, 320),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            RightToLeftLayout = true,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = UiTheme.BodyFont
        };
        var code = new TextBox { Dock = DockStyle.Top, Height = 30 };
        var permit = new TextBox { Dock = DockStyle.Top, Height = 30 };
        var region = new TextBox { Dock = DockStyle.Top, Height = 30 };
        var notes = new TextBox { Dock = DockStyle.Top, Height = 60, Multiline = true };
        var ok = new Button { Text = "ثبت", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 40 };
        UiTheme.StylePrimaryButton(ok);
        dlg.Controls.Add(ok);
        dlg.Controls.Add(notes);
        dlg.Controls.Add(region);
        dlg.Controls.Add(permit);
        dlg.Controls.Add(code);
        dlg.Controls.Add(new Label
        {
            Text = "کد عملیات، شماره حکم/مجوز، منطقه و یادداشت را وارد کنید",
            Dock = DockStyle.Top,
            Height = 28
        });
        dlg.AcceptButton = ok;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (string.IsNullOrWhiteSpace(code.Text) || string.IsNullOrWhiteSpace(region.Text))
        {
            MessageBox.Show("کد عملیات و منطقه الزامی است.", "خطا", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try
        {
            _db.CreateOperation(new GovernmentOperation
            {
                OperationCode = code.Text.Trim(),
                Region = region.Text.Trim(),
                PermitNumber = permit.Text.Trim(),
                AuthorizedBy = AppSession.CurrentUser?.FullName ?? "نامشخص",
                StartTime = DateTime.Now,
                Status = "در جریان",
                Notes = notes.Text.Trim()
            });
            LoadOperationsGrid();
            SetStatus($"عملیات {code.Text.Trim()} ثبت شد");
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطا در ثبت: " + ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CloseSelectedOperation()
    {
        if (_opsGrid?.CurrentRow == null)
        {
            MessageBox.Show("یک ردیف انتخاب کنید.", "توجه");
            return;
        }
        var code = _opsGrid.CurrentRow.Cells[0].Value?.ToString();
        if (string.IsNullOrEmpty(code)) return;
        if (MessageBox.Show($"عملیات {code} بسته شود؟", "تأیید", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            != DialogResult.Yes) return;
        _db.UpdateOperationStatus(code, "پایان‌یافته", DateTime.Now);
        LoadOperationsGrid();
    }

    // ─────────────────────────── شناسایی‌ها ───────────────────────────

    private void ShowDetections()
    {
        ClearContent();
        var filterBar = new Panel { Dock = DockStyle.Top, Height = 48 };
        _opFilter = new ComboBox { Width = 170, Left = 8, Top = 10, DropDownStyle = ComboBoxStyle.DropDownList };
        _statusFilter = new ComboBox { Width = 150, Left = 188, Top = 10, DropDownStyle = ComboBoxStyle.DropDownList };
        _opFilter.Items.Add("همه عملیات");
        foreach (var o in _db.GetOperations()) _opFilter.Items.Add(o.OperationCode);
        _opFilter.SelectedIndex = 0;
        _statusFilter.Items.AddRange(new object[]
        {
            "همه", "منتظر دستور", "منتظر‌دستورالعمل", "ضبط‌شده", "قطع برق", "اخطار", "بایگانی"
        });
        _statusFilter.SelectedIndex = 0;

        var refresh = new Button { Text = "اعمال فیلتر", Width = 100, Left = 348, Top = 8 };
        var detail = new Button { Text = "مشاهده شواهد", Width = 110, Left = 456, Top = 8 };
        var reprobe = new Button { Text = "پروب مجدد", Width = 100, Left = 574, Top = 8 };
        var actSeize = new Button { Text = "ثبت ضبط", Width = 100, Left = 682, Top = 8 };
        var actWarn = new Button { Text = "اخطار", Width = 90, Left = 790, Top = 8 };
        var actCut = new Button { Text = "قطع برق", Width = 90, Left = 888, Top = 8 };
        UiTheme.StyleSecondaryButton(refresh);
        UiTheme.StyleSecondaryButton(detail);
        UiTheme.StyleSecondaryButton(reprobe);
        UiTheme.StyleDangerButton(actSeize);
        UiTheme.StylePrimaryButton(actWarn);
        UiTheme.StylePrimaryButton(actCut);
        actCut.BackColor = UiTheme.Warning;

        refresh.Click += (_, _) => LoadDetectionsGrid();
        detail.Click += (_, _) => ShowDetectionDetails();
        reprobe.Click += async (_, _) => await ReprobeSelectedAsync();
        actSeize.Click += (_, _) => Enforce("ضبط‌شده");
        actWarn.Click += (_, _) => Enforce("اخطار");
        actCut.Click += (_, _) => Enforce("قطع برق");

        filterBar.Controls.AddRange(new Control[]
        {
            _opFilter, _statusFilter, refresh, detail, reprobe, actSeize, actWarn, actCut
        });

        _detGrid = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(_detGrid);
        string[] cols =
        {
            "ID", "کدعملیات", "IP", "پورت", "وضعیت پورت", "نرم‌افزار", "مدل", "هش‌ریت",
            "دما", "توان W", "AS", "مالک شبکه", "ISP/ثبت‌کننده", "MAC", "اطمینان", "اقدام", "آخرین مشاهده"
        };
        foreach (var c in cols) _detGrid.Columns.Add(c, c);
        _detGrid.Columns[0].Visible = false;

        _content.Controls.Add(_detGrid);
        _content.Controls.Add(filterBar);
        _content.Controls.Add(PageTitle("دستگاه‌های شناسایی‌شده (داده‌های واقعی)"));
        LoadDetectionsGrid();
    }

    private void LoadDetectionsGrid()
    {
        if (_detGrid == null) return;
        _detGrid.Rows.Clear();
        string? op = _opFilter?.SelectedItem?.ToString();
        if (op == "همه عملیات") op = null;
        var st = _statusFilter?.SelectedItem?.ToString();
        var rows = _db.GetDetections(op, st);
        foreach (var d in rows)
        {
            _detGrid.Rows.Add(d.OperationID, d.OperationCode, d.IPAddress, d.Port, D(d.PortState),
                D(d.SoftwareName), D(d.DeviceModel), D(d.HashRate),
                d.TemperatureC?.ToString("F1", CultureInfo.InvariantCulture) ?? "—",
                d.PowerWatts?.ToString("F1", CultureInfo.InvariantCulture) ?? "—",
                D(d.AsNumber), D(d.AsName), D(d.NetworkRegistrant), D(d.MacAddress),
                d.Confidence.ToString("P0", CultureInfo.InvariantCulture),
                D(d.ActionStatus),
                d.LastSeen?.ToString("yyyy-MM-dd HH:mm") ?? "—");
        }
        if (_detGrid.Rows.Count == 0)
            SetStatus("هیچ شناسایی ثبت نشده است (داده‌ای وجود ندارد)");
        else
            SetStatus($"شناسایی‌ها: {_detGrid.Rows.Count}");
    }

    private string? SelectedDetectionId() =>
        _detGrid?.CurrentRow?.Cells[0].Value?.ToString();

    private static string D(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "ثبت نشده" : value;

    private async Task ReprobeSelectedAsync()
    {
        var id = SelectedDetectionId();
        if (id == null) { MessageBox.Show("یک ردیف انتخاب کنید.", "توجه"); return; }
        if (!RequireLevel(2, "پروب مجدد")) return;

        _scanCts = new CancellationTokenSource();
        SetStatus("در حال پروب مجدد...");
        try
        {
            var device = await _tracker.RefreshDetectionAsync(id, _scanCts.Token);
            if (device == null)
                MessageBox.Show("پورت دیگر پاسخ نمی‌دهد؛ دستگاه خاموش یا فیلتر شده است.",
                    "نتیجه پروب", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show(
                    $"وضعیت جدید:\nنرم‌افزار: {D(device.SoftwareName)}\nمدل: {D(device.DeviceModel)}\n" +
                    $"هش‌ریت: {D(device.HashRate)}\nتوان: {(device.PowerWatts?.ToString("F1") ?? "ثبت نشده")} وات\n" +
                    $"اطمینان: {device.Confidence:P0}",
                    "نتیجه پروب", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadDetectionsGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowDetectionDetails()
    {
        var id = SelectedDetectionId();
        if (id == null) { MessageBox.Show("یک ردیف انتخاب کنید.", "توجه"); return; }
        var d = _db.GetDetection(id);
        if (d == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"IP / پورت              : {d.IPAddress}:{d.Port}");
        sb.AppendLine($"وضعیت پورت             : {D(d.PortState)}   | پروتکل: {D(d.Protocol)}");
        sb.AppendLine($"تأخیر اتصال            : {(d.LatencyMs?.ToString() ?? "ثبت نشده")} میلی‌ثانیه");
        sb.AppendLine($"روش شناسایی            : {D(d.DetectionMethod)}");
        sb.AppendLine($"وضعیت HTTP             : {D(d.HttpStatus)}");
        sb.AppendLine();
        sb.AppendLine($"نرم‌افزار              : {D(d.SoftwareName)}");
        sb.AppendLine($"مدل دستگاه            : {D(d.DeviceModel)}");
        sb.AppendLine($"فریم‌ور                : {D(d.FirmwareVersion)}");
        sb.AppendLine($"هش‌ریت                 : {D(d.HashRate)}");
        sb.AppendLine($"دما                     : {(d.TemperatureC?.ToString("F1") ?? "ثبت نشده")} درجه سلسیوس");
        sb.AppendLine($"سرعت فن                : {(d.FanPercent?.ToString("F0") ?? "ثبت نشده")} درصد");
        sb.AppendLine($"توان                    : {(d.PowerWatts?.ToString("F1") ?? "ثبت نشده")} وات — منبع: {D(d.PowerSource)}");
        sb.AppendLine($"آپ‌تایم                : {(d.UptimeSeconds.HasValue ? TimeSpan.FromSeconds(d.UptimeSeconds.Value).ToString() : "ثبت نشده")}");
        sb.AppendLine($"استخر                  : {D(d.PoolAddress)}");
        sb.AppendLine($"نام کارگر              : {D(d.WorkerName)}");
        sb.AppendLine($"سطح اطمینان            : {d.Confidence:P0} (بر پایه شواهد پاسخ واقعی دستگاه)");
        sb.AppendLine();
        sb.AppendLine("── استعلام شبکه ──");
        sb.AppendLine($"AS                      : {D(d.AsNumber)}");
        sb.AppendLine($"نام AS                  : {D(d.AsName)}");
        sb.AppendLine($"ثبت‌کننده               : {D(d.NetworkRegistrant)}");
        sb.AppendLine($"کشور                    : {D(d.NetworkCountry)}");
        sb.AppendLine($"محدوده                  : {D(d.PrefixCidr)}");
        sb.AppendLine($"PTR                     : {D(d.ReverseDns)}");
        sb.AppendLine($"MAC (جدول ARP)         : {D(d.MacAddress)}");
        sb.AppendLine($"وضعیت استعلام          : {D(d.IntelSource)}");
        sb.AppendLine();
        sb.AppendLine("── اطلاعات مشترک (فقط از پاسخ رسمی اپراتور) ──");
        sb.AppendLine($"استان/شهر               : {D(d.Province)} / {D(d.City)}");
        sb.AppendLine($"نشانی                   : {D(d.Street)}");
        sb.AppendLine($"کدپستی                  : {D(d.PostalCode)}");
        sb.AppendLine($"نام مشترک               : {D(d.SubscriberName)}");
        sb.AppendLine($"شماره تماس              : {D(d.PhoneNumber)}");
        sb.AppendLine($"شناسه رکورد استعلام      : {D(d.OperatorRecordID)}");
        sb.AppendLine();
        sb.AppendLine("── اقدام و سوابق ──");
        sb.AppendLine($"وضعیت اقدام             : {D(d.ActionStatus)}");
        sb.AppendLine($"نوع اقدام               : {D(d.ActionType)}");
        sb.AppendLine($"اپراتور                 : {D(d.ActionEnforcedBy)}");
        sb.AppendLine($"یادداشت                 : {D(d.ActionNotes)}");
        sb.AppendLine($"زمان شناسایی            : {d.DetectionTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"آخرین مشاهده            : {(d.LastSeen?.ToString("yyyy-MM-dd HH:mm:ss") ?? "ثبت نشده")}");
        sb.AppendLine($"شناسه پروب               : {D(d.ScanID)}");
        sb.AppendLine();
        sb.AppendLine($"هش شواهد (SHA-256)      : {D(d.EvidenceHash)}");
        sb.AppendLine($"خلاصه شواهد             : {D(d.EvidenceSummary)}");
        sb.AppendLine();
        sb.AppendLine("── متن خام دریافتی از دستگاه ──");
        sb.AppendLine(string.IsNullOrWhiteSpace(d.BannerText) ? "متنی دریافت نشد" : d.BannerText);

        using var dlg = new Form
        {
            Text = $"شواهد فنی — {d.IPAddress}:{d.Port}",
            ClientSize = new Size(860, 680),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            Font = UiTheme.BodyFont
        };
        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9.5f),
            Text = sb.ToString()
        };
        var opBtn = new Button { Text = "ثبت اطلاعات استعلام‌شده از اپراتور", Dock = DockStyle.Bottom, Height = 40 };
        UiTheme.StylePrimaryButton(opBtn);
        dlg.Controls.Add(box);
        dlg.Controls.Add(opBtn);
        opBtn.Click += (_, _) =>
        {
            if (OperatorDialog.Show(this, _db, d))
                MessageBox.Show("اطلاعات ثبت و به رکورد شناسایی متصل شد.", "ثبت شد",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        dlg.ShowDialog(this);
        LoadDetectionsGrid();
    }

    private void Enforce(string action)
    {
        var id = SelectedDetectionId();
        if (id == null) { MessageBox.Show("یک ردیف انتخاب کنید.", "توجه"); return; }
        if (!RequireLevel(4, "ثبت اقدام اجرایی")) return;

        var notes = "";
        using (var input = new Form
        {
            Text = "ثبت اقدام — " + action,
            ClientSize = new Size(460, 220),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            Font = UiTheme.BodyFont,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        })
        {
            var tb = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            var ok = new Button { Text = "ثبت", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 36 };
            UiTheme.StylePrimaryButton(ok);
            input.Controls.Add(tb);
            input.Controls.Add(ok);
            input.AcceptButton = ok;
            if (input.ShowDialog(this) != DialogResult.OK) return;
            notes = tb.Text.Trim();
        }
        if (string.IsNullOrWhiteSpace(notes))
        {
            MessageBox.Show("ثبت یادداشت الزامی است؛ اقدام بدون شرح قابل دفاع نیست.", "توجه");
            return;
        }

        _db.RecordEnforcement(id, action, AppSession.CurrentUser?.FullName ?? "سیستم", notes);
        LoadDetectionsGrid();
        SetStatus($"اقدام «{action}» ثبت شد");
    }

    // ─────────────────────────── پروب مجاز ───────────────────────────

    private void ShowScan()
    {
        ClearContent();
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 340,
            ColumnCount = 2,
            RowCount = 7,
            RightToLeft = RightToLeft.Yes
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _scanOpCode = new TextBox { Dock = DockStyle.Fill };
        _scanPermit = new TextBox { Dock = DockStyle.Fill };
        _scanRegion = new TextBox { Dock = DockStyle.Fill };
        _scanTargets = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "هر خط یک هدف: ۱۰.۰.۰.۵  |  ۱۰.۰.۰.۱-۱۰.۰.۰.۵۰  |  ۱۰.۰.۰.۰/۲۴"
        };
        _scanPorts = new TextBox { Dock = DockStyle.Fill, Enabled = false };
        _scanParallel = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 256, Value = 48 };
        _scanTimeout = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 200, Maximum = 10000, Value = 800, Increment = 100 };
        _scanDefaultPorts = new CheckBox { Text = "استفاده از فهرست پورت‌های متداول ماینر", Checked = true, AutoSize = true };
        _scanDefaultPorts.CheckedChanged += (_, _) => _scanPorts.Enabled = !_scanDefaultPorts.Checked;
        _scanIntel = new CheckBox { Text = "استعلام مالکیت شبکه از RDAP / RIPE Stat (نیازمند اینترنت)", Checked = true, AutoSize = true };
        _scanArp = new CheckBox { Text = "ثبت نشانی فیزیکی از جدول ARP سیستم", Checked = true, AutoSize = true };

        var ops = _db.GetOperations().Where(o => o.Status != "پایان‌یافته").ToList();
        if (ops.Count > 0)
        {
            _scanOpCode.Text = ops[0].OperationCode;
            _scanRegion.Text = ops[0].Region;
            _scanPermit.Text = ops[0].PermitNumber;
        }

        form.Controls.Add(Lbl("کد عملیات"), 0, 0);
        form.Controls.Add(_scanOpCode, 1, 0);
        form.Controls.Add(Lbl("شماره حکم/مجوز"), 0, 1);
        form.Controls.Add(_scanPermit, 1, 1);
        form.Controls.Add(Lbl("منطقه"), 0, 2);
        form.Controls.Add(_scanRegion, 1, 2);
        form.Controls.Add(Lbl("اهداف مجاز (IP / بازه / CIDR)"), 0, 3);
        form.Controls.Add(_scanTargets, 1, 3);
        form.SetRowSpan(_scanTargets, 2);
        form.Controls.Add(Lbl("پورت‌ها (در صورت غیرفعال بودن گزینه بالا)"), 0, 5);
        form.Controls.Add(_scanPorts, 1, 5);
        form.Controls.Add(Lbl("حداکثر اتصال هم‌زمان"), 0, 6);
        form.Controls.Add(_scanParallel, 1, 6);

        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 72,
            FlowDirection = FlowDirection.RightToLeft
        };
        var timeoutLbl = new Label { Text = "مهلت اتصال (ms)", AutoSize = true, Padding = new Padding(0, 8, 4, 0) };
        options.Controls.AddRange(new Control[]
        {
            _scanDefaultPorts, _scanIntel, _scanArp, timeoutLbl, _scanTimeout
        });

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.RightToLeft };
        var startBtn = new Button { Text = "شروع پروب واقعی", Width = 150 };
        var localBtn = new Button { Text = "پروب این رایانه", Width = 150 };
        var expandBtn = new Button { Text = "گسترش اهداف و نمایش تعداد", Width = 190 };
        var stopBtn = new Button { Text = "توقف", Width = 100 };
        UiTheme.StylePrimaryButton(startBtn);
        UiTheme.StyleSecondaryButton(localBtn);
        UiTheme.StyleSecondaryButton(expandBtn);
        UiTheme.StyleDangerButton(stopBtn);
        startBtn.Click += async (_, _) => await StartScanAsync();
        localBtn.Click += async (_, _) => await StartLocalProbeAsync();
        expandBtn.Click += (_, _) => PreviewTargets();
        stopBtn.Click += (_, _) => _scanCts?.Cancel();
        btnRow.Controls.AddRange(new Control[] { startBtn, localBtn, expandBtn, stopBtn });

        var notice = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            ForeColor = UiTheme.Danger,
            Font = new Font("Segoe UI", 9f),
            Text = "هشدار قانونی: پروب فقط و فقط روی اهدافی مجاز است که دارای حکم رسمی هستند. " +
                   "سامانه هیچ داده ساختگی یا شبیه‌سازی‌شده‌ای تولید نمی‌کند؛ هر فیلد تنها در صورت " +
                   "دریافت پاسخ واقعی از دستگاه یا استعلام از مرجع معتبر ثبت می‌شود."
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
        panel.Controls.Add(options);
        panel.Controls.Add(form);
        _content.Controls.Add(panel);
        _content.Controls.Add(PageTitle("پروب مجاز — سنجش واقعی دستگاه"));
    }

    private static Label Lbl(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight
    };

    private void PreviewTargets()
    {
        if (_scanTargets == null) return;
        var lines = _scanTargets.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var expanded = _tracker.ExpandTargets(lines, out var warnings);
        var msg = $"تعداد آدرس‌های نهایی پس از گسترش: {expanded.Count}";
        if (expanded.Count > 0)
            msg += $"\nنمونه: {string.Join(" , ", expanded.Take(12))}{(expanded.Count > 12 ? " ..." : "")}";
        if (warnings.Count > 0) msg += "\n\nهشدارها:\n" + string.Join("\n", warnings);
        MessageBox.Show(this, msg, "پیش‌نمایش اهداف", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void EnsureOperationExists(string code, string region, string permit)
    {
        if (_db.OperationExists(code)) return;
        _db.CreateOperation(new GovernmentOperation
        {
            OperationCode = code,
            Region = region,
            PermitNumber = permit,
            AuthorizedBy = AppSession.CurrentUser?.FullName ?? "سیستم",
            StartTime = DateTime.Now,
            Status = "در جریان",
            Notes = "ایجاد خودکار در صفحه پروب"
        });
    }

    private async Task StartScanAsync()
    {
        if (_scanOpCode == null || _scanRegion == null || _scanTargets == null ||
            _scanPorts == null || _scanParallel == null || _scanTimeout == null) return;

        if (!RequireLevel(3, "اجرای پروب")) return;

        var op = _scanOpCode.Text.Trim();
        var region = _scanRegion.Text.Trim();
        var permit = _scanPermit?.Text.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(op))
        {
            MessageBox.Show("کد عملیات را وارد کنید.", "خطا");
            return;
        }
        if (string.IsNullOrWhiteSpace(permit))
        {
            MessageBox.Show("شماره حکم/مجوز الزامی است؛ بدون آن پروب آغاز نمی‌شود.", "خطا",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var lines = _scanTargets.Lines.Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        if (lines.Count == 0)
        {
            MessageBox.Show("حداقل یک هدف مجاز وارد کنید.", "توجه");
            return;
        }

        var useDefaultPorts = _scanDefaultPorts?.Checked ?? true;
        var ports = new List<int>();
        if (!useDefaultPorts)
        {
            foreach (var part in _scanPorts.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var p) && p is > 0 and < 65536) ports.Add(p);
            }
            if (ports.Count == 0)
            {
                MessageBox.Show("فهرست پورت معتبر وارد نشده است.", "خطا");
                return;
            }
        }

        var expanded = _tracker.ExpandTargets(lines, out var warnings);
        if (expanded.Count == 0)
        {
            MessageBox.Show("هیچ آدرس IPv4 معتبری در اهداف واردشده یافت نشد.", "خطا");
            return;
        }

        var totalProbes = (long)expanded.Count * (useDefaultPorts ? TrackingService.DefaultMinerPorts.Length : ports.Distinct().Count());
        var confirm = MessageBox.Show(
            $"تأیید می‌کنید که این پروب تحت حکم شماره {permit} مجاز است؟\n\n" +
            $"تعداد آدرس‌ها: {expanded.Count:N0}\n" +
            $"تعداد پورت برای هر آدرس: {(useDefaultPorts ? TrackingService.DefaultMinerPorts.Length : ports.Distinct().Count())}\n" +
            $"تعداد کل اتصال: {totalProbes:N0}\n\n" +
            "سامانه تنها اتصال TCP می‌گیرد و هیچ داده‌ای جعل یا شبیه‌سازی نمی‌کند.",
            "تأیید قانونی پروب", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        EnsureOperationExists(op, region, permit);

        var request = new ScanRequest
        {
            OperationCode = op,
            Region = region,
            Targets = lines,
            Ports = ports,
            AuthorizedBy = AppSession.CurrentUser?.FullName ?? "سیستم",
            MaxParallel = (int)_scanParallel.Value,
            ConnectTimeoutMs = (int)_scanTimeout.Value,
            ResolveNetworkOwner = _scanIntel?.Checked ?? true,
            LookUpLocalMac = _scanArp?.Checked ?? true
        };

        await RunScanAsync(request, warnings);
    }

    private async Task StartLocalProbeAsync()
    {
        if (_scanOpCode == null) return;
        if (!RequireLevel(2, "پروب محلی")) return;

        var op = _scanOpCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(op))
        {
            MessageBox.Show("کد عملیات را وارد کنید.", "خطا");
            return;
        }
        var subnets = TrackingService.GetLocalSubnets();
        var text = string.Join(Environment.NewLine, subnets.Select(s => s.Address));
        if (text.Length == 0) text = string.Join(Environment.NewLine, TrackingService.GetLocalIPv4Addresses());

        _scanTargets ??= new TextBox();
        _scanTargets.Text = text;
        await StartScanAsync();
    }

    private async Task RunScanAsync(ScanRequest request, List<string> warnings)
    {
        _scanCts = new CancellationTokenSource();
        _progress.Value = 0;
        foreach (var w in warnings) AppendScanLog("هشدار: " + w);
        AppendScanLog($"شروع پروب واقعی — عملیات {request.OperationCode} — اپراتور {request.AuthorizedBy}");

        try
        {
            var outcome = await _tracker.RunAuthorizedScanAsync(request, _scanCts.Token);
            AppendScanLog("———— نتیجه پروب —————");
            AppendScanLog($"شناسه پروب: {outcome.ScanID}");
            AppendScanLog($"آدرس‌های بررسی‌شده: {outcome.TargetsExpanded:N0} | اتصال‌ها: {outcome.PortsProbed:N0}");
            AppendScanLog($"پورت‌های باز: {outcome.OpenPorts} | شناسایی قطعی: {outcome.Identified}");
            AppendScanLog($"پورت‌های بسته: {outcome.ClosedPorts} | فیلترشده: {outcome.FilteredPorts}");
            AppendScanLog($"مدت: {outcome.ElapsedSeconds:F1} ثانیه | استعلام‌های ناموفق: {outcome.IntelFailures}");

            MessageBox.Show(
                $"پایان پروب.\nآدرس‌های بررسی‌شده: {outcome.TargetsExpanded:N0}\n" +
                $"پورت‌های باز: {outcome.OpenPorts}\nشناسایی قطعی: {outcome.Identified}\n" +
                $"مدت: {outcome.ElapsedSeconds:F1} ثانیه",
                "نتیجه پروب", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetStatus($"پروب پایان یافت — {outcome.OpenPorts} پورت باز");
        }
        catch (OperationCanceledException)
        {
            AppendScanLog("پروب توسط اپراتور متوقف شد. سوابق تا این لحظه حفظ می‌شود.");
        }
        catch (Exception ex)
        {
            AppendScanLog("خطا: " + ex.Message);
            MessageBox.Show(ex.Message, "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AppendScanLog(string msg)
    {
        var log = _content.Controls.Find("ScanLog", true).FirstOrDefault() as TextBox;
        if (log == null) return;
        if (log.IsDisposed) return;
        log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
    }

    private void OnScanProgress(object? sender, ScanProgressEventArgs e)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => OnScanProgress(sender, e)); }
            catch (ObjectDisposedException) { }
            return;
        }
        _progress.Value = Math.Min(100, Math.Max(0, e.Percent));
        SetStatus($"{e.Message} | بررسی‌شده: {e.Probed:N0}/{e.Total:N0} | یافته: {e.Found} | زمان: {e.ElapsedSeconds:F0}ث");
    }

    // ─────────────────────────── تاریخچه پروب ───────────────────────────

    private void ShowScanHistory()
    {
        ClearContent();
        var bar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var refresh = new Button { Text = "بازنشانی", Width = 100, Left = 8, Top = 8 };
        UiTheme.StyleSecondaryButton(refresh);
        refresh.Click += (_, _) => LoadScanHistory();
        bar.Controls.Add(refresh);

        _scanGrid = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(_scanGrid);
        string[] cols =
        {
            "ScanID", "عملیات", "نوع", "اپراتور", "رایانه", "شروع", "پایان", "مدت(ث)",
            "آدرس‌ها", "اتصال‌ها", "باز", "شناسایی", "بسته", "فیلترشده", "یادداشت"
        };
        foreach (var c in cols) _scanGrid.Columns.Add(c, c);

        _content.Controls.Add(_scanGrid);
        _content.Controls.Add(bar);
        _content.Controls.Add(PageTitle("تاریخچه پروب‌های واقعی"));
        LoadScanHistory();
    }

    private void LoadScanHistory()
    {
        if (_scanGrid == null) return;
        _scanGrid.Rows.Clear();
        foreach (var s in _db.GetScanHistory())
        {
            _scanGrid.Rows.Add(s.ScanID, s.OperationCode, s.ScanType, s.OperatorName, s.MachineName,
                s.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                s.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—",
                s.ElapsedSeconds.ToString("F1"),
                s.IPsScanned, s.PortsProbed, s.DevicesFound, s.IdentifiedDevices,
                s.ClosedPorts, s.FilteredPorts, s.Notes);
        }
        SetStatus($"تعداد پروب‌های ثبت‌شده: {_scanGrid.Rows.Count}");
    }

    // ─────────────────────────── گزارش‌ها ───────────────────────────

    private void ShowReports()
    {
        ClearContent();
        var bar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var opBox = new ComboBox { Width = 190, Left = 8, Top = 10, DropDownStyle = ComboBoxStyle.DropDownList };
        opBox.Items.Add("همه");
        foreach (var o in _db.GetOperations()) opBox.Items.Add(o.OperationCode);
        opBox.SelectedIndex = 0;
        var txtBtn = new Button { Text = "گزارش رسمی متنی", Width = 130, Left = 208, Top = 8 };
        var csvBtn = new Button { Text = "خروجی CSV", Width = 110, Left = 346, Top = 8 };
        var jsonBtn = new Button { Text = "خروجی شواهد JSON", Width = 150, Left = 464, Top = 8 };
        var openBtn = new Button { Text = "باز کردن پوشه گزارش‌ها", Width = 160, Left = 622, Top = 8 };
        UiTheme.StylePrimaryButton(txtBtn);
        UiTheme.StyleSecondaryButton(csvBtn);
        UiTheme.StyleSecondaryButton(jsonBtn);
        UiTheme.StyleSecondaryButton(openBtn);

        string? SelectedCode()
        {
            var code = opBox.SelectedItem?.ToString();
            return code == "همه" ? null : code;
        }

        txtBtn.Click += (_, _) =>
        {
            try
            {
                var path = _reports.GenerateTextReport(SelectedCode(), AppSession.CurrentUser?.FullName ?? "سیستم");
                MessageBox.Show("گزارش ذخیره شد:\n" + path, "موفق");
                SetStatus("گزارش رسمی تولید شد");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "خطا"); }
        };
        csvBtn.Click += (_, _) =>
        {
            try
            {
                var path = _reports.ExportCsv(SelectedCode(), AppSession.CurrentUser?.FullName ?? "سیستم");
                MessageBox.Show("CSV ذخیره شد:\n" + path, "موفق");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "خطا"); }
        };
        jsonBtn.Click += (_, _) =>
        {
            try
            {
                var path = _reports.ExportEvidenceJson(SelectedCode(), AppSession.CurrentUser?.FullName ?? "سیستم");
                MessageBox.Show("خروجی شواهد ذخیره شد:\n" + path, "موفق");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "خطا"); }
        };
        openBtn.Click += (_, _) =>
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        };

        bar.Controls.AddRange(new Control[] { opBox, txtBtn, csvBtn, jsonBtn, openBtn });
        var info = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopRight,
            Padding = new Padding(12),
            Font = new Font("Segoe UI", 10f),
            Text = "گزارش‌ها فقط از داده‌های واقعی پایگاه داده ساخته می‌شوند.\n" +
                   "مقادیری که واقعاً اندازه‌گیری نشده‌اند با «ثبت نشده» درج می‌شوند و هرگز تخمین زده نمی‌شوند.\n" +
                   "انتهای گزارش متنی شامل اثر انگشت SHA-256 سند و وضعیت صحت زنجیره حسابرسی است.\n" +
                   "خروجی JSON شامل متن خام پاسخ دستگاه برای بازبینی کارشناسی است."
        };
        _content.Controls.Add(info);
        _content.Controls.Add(bar);
        _content.Controls.Add(PageTitle("تولید گزارش رسمی"));
    }

    // ─────────────────────────── پرسنل ───────────────────────────

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
                p.LastLogin?.ToString("yyyy-MM-dd HH:mm") ?? "—");
        }
    }

    private void EditPersonnelDialog()
    {
        if (!RequireLevel(5, "مدیریت پرسنل")) return;

        Personnel? existing = null;
        if (_persGrid?.CurrentRow != null)
        {
            var id = _persGrid.CurrentRow.Cells[0].Value?.ToString();
            existing = _db.GetPersonnel().FirstOrDefault(p => p.PersonnelID == id);
        }

        using var dlg = new Form
        {
            Text = existing == null ? "افزودن پرسنل" : "ویرایش پرسنل",
            ClientSize = new Size(480, 440),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            Font = UiTheme.BodyFont,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };
        var name = Field(dlg, "نام کامل", existing?.FullName ?? "", 10);
        var dept = Field(dlg, "بخش", existing?.Department ?? "", 50);
        var role = Field(dlg, "نقش", existing?.Role ?? "", 90);
        var badge = Field(dlg, "Badge", existing?.Badge ?? "", 130);
        var phone = Field(dlg, "تلفن", existing?.PhoneNumber ?? "", 170);
        var email = Field(dlg, "ایمیل", existing?.Email ?? "", 210);
        var level = Field(dlg, "سطح دسترسی (1-5)", (existing?.AuthorizationLevel ?? 1).ToString(), 250);
        var pass = Field(dlg, existing == null ? "رمز عبور (الزامی)" : "رمز جدید (خالی=بدون تغییر)", "", 290);
        pass.UseSystemPasswordChar = true;
        var ok = new Button { Text = "ذخیره", Left = 20, Top = 350, Width = 120, DialogResult = DialogResult.OK };
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
        if (existing == null && string.IsNullOrWhiteSpace(pass.Text))
        {
            MessageBox.Show("برای کاربر جدید رمز عبور الزامی است.");
            return;
        }
        try
        {
            _db.UpsertPersonnel(p, string.IsNullOrWhiteSpace(pass.Text) ? null : pass.Text);
            LoadPersonnelGrid();
            SetStatus("اطلاعات پرسنل ذخیره شد");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "خطا");
        }
    }

    private static TextBox Field(Form dlg, string label, string value, int top)
    {
        dlg.Controls.Add(new Label { Text = label, Left = 20, Top = top, Width = 200, TextAlign = ContentAlignment.MiddleRight });
        var tb = new TextBox { Left = 230, Top = top, Width = 220, Text = value };
        dlg.Controls.Add(tb);
        return tb;
    }

    // ─────────────────────────── حسابرسی ───────────────────────────

    private void ShowAudit()
    {
        ClearContent();
        var bar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var refresh = new Button { Text = "بازنشانی", Width = 100, Left = 8, Top = 8 };
        var verify = new Button { Text = "بررسی صحت زنجیره هش", Width = 160, Left = 116, Top = 8 };
        UiTheme.StyleSecondaryButton(refresh);
        UiTheme.StylePrimaryButton(verify);
        refresh.Click += (_, _) => LoadAudit();
        verify.Click += (_, _) =>
        {
            var result = _db.VerifyAuditChain();
            MessageBox.Show(
                result.Valid
                    ? $"زنجیره حسابرسی معتبر است.\n{result.Checked:N0} رکورد بررسی شد."
                    : $"زنجیره حسابرسی معتبر نیست!\nنخستین رکورد نامعتبر: {result.FirstBrokenId}",
                result.Valid ? "نتیجه صحت‌سنجی" : "هشدار",
                MessageBoxButtons.OK,
                result.Valid ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        };
        bar.Controls.Add(refresh);
        bar.Controls.Add(verify);

        _auditGrid = new DataGridView { Dock = DockStyle.Fill };
        UiTheme.StyleDataGrid(_auditGrid);
        foreach (var c in new[] { "زمان", "کاربر", "Badge", "اقدام", "موجودیت", "شناسه", "شرح", "رایانه", "اثر انگشت" })
            _auditGrid.Columns.Add(c, c);

        _content.Controls.Add(_auditGrid);
        _content.Controls.Add(bar);
        _content.Controls.Add(PageTitle("حسابرسی تغییرناپذیر"));
        LoadAudit();
    }

    private void LoadAudit()
    {
        if (_auditGrid == null) return;
        _auditGrid.Rows.Clear();
        foreach (var a in _db.GetAuditLog())
        {
            _auditGrid.Rows.Add(a.LoggedAt.ToString("yyyy-MM-dd HH:mm:ss"), a.UserName, a.Badge,
                a.Action, a.Entity, a.EntityID, a.Details, a.MachineName, a.IntegrityHash);
        }
        SetStatus($"رکوردهای حسابرسی: {_auditGrid.Rows.Count}");
    }

    // ─────────────────────────── تنظیمات ───────────────────────────

    private void ShowSettings()
    {
        ClearContent();
        var subnets = TrackingService.GetLocalSubnets();
        var info = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Segoe UI", 10f),
            BackColor = Color.White,
            Text =
                "سامانه ردیابی دستگاه‌های ماینر\r\n" +
                $"{AppVersion()}\r\n" +
                "پلتفرم: Windows Forms / .NET 8\r\n\r\n" +
                $"مسیر پایگاه داده:\r\n{_db.DatabasePath}\r\n\r\n" +
                $"مسیر اجرا:\r\n{AppDomain.CurrentDomain.BaseDirectory}\r\n\r\n" +
                $"رایانه اجرایی:\r\n{Environment.MachineName}\r\n\r\n" +
                "اصول کاری سامانه:\r\n" +
                "• هیچ داده ساختگی، نمونه یا شبیه‌سازی‌شده‌ای تولید نمی‌شود.\r\n" +
                "• هر شناسایی حاصل اتصال TCP واقعی و تحلیل پاسخ واقعی همان دستگاه است.\r\n" +
                "• مالکیت شبکه تنها از RDAP و RIPE Stat و PTR سیستم‌عامل خوانده می‌شود.\r\n" +
                "• اطلاعات مشترک تنها از پاسخ رسمی اپراتور ثبت و به سند استعلام پیوند می‌خورد.\r\n" +
                "• توان مصرفی تنها در صورتی ثبت می‌شود که دستگاه آن را اعلام کرده باشد.\r\n" +
                "• هر سند دارای اثر انگشت SHA-256 و زنجیره حسابرسی تغییرناپذیر است.\r\n\r\n" +
                $"محدوده‌های شبکه این رایانه:\r\n{string.Join(Environment.NewLine, subnets.Select(s => $"    {s.Address}    (کارت: {s.Cidr})"))}\r\n\r\n" +
                "سطوح دسترسی:\r\n" +
                "    1: مشاهده    2: پروب محلی و پروب مجدد    3: اجرای پروب    4: ثبت اقدام    5: مدیریت پرسنل\r\n"
        };
        _content.Controls.Add(info);
        _content.Controls.Add(PageTitle("تنظیمات و شناسنامه سامانه"));
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

    private void SetStatus(string msg) => _status.Text = "✓ " + msg;
}
