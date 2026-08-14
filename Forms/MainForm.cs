using GovernmentMiningApp.Models;
using GovernmentMiningApp.Services;
using GovernmentMiningApp.Ui;

namespace GovernmentMiningApp.Forms;

public sealed class MainForm : Form
{
    private readonly DatabaseService _db;
    private readonly TrackingService _tracker;
    private readonly ReportService _reports;
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Surface, Padding = new Padding(20) };
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private DataGridView? _opsGrid;
    private DataGridView? _detGrid;
    private DataGridView? _persGrid;
    private ComboBox? _opFilter;
    private ComboBox? _statusFilter;
    private TextBox? _ipList;
    private CheckBox? _simMode;
    private TextBox? _scanRegion;
    private TextBox? _scanOpCode;
    private CancellationTokenSource? _scanCts;

    public MainForm(DatabaseService db)
    {
        _db = db; _tracker = new TrackingService(db); _reports = new ReportService(db);
        _tracker.ProgressChanged += OnScanProgress;
        Text = "Ckashef | مرکز فرمان عملیات"; WindowState = FormWindowState.Maximized; MinimumSize = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterScreen; UiTheme.ApplyWindow(this); BuildChrome(); ShowDashboard();
    }

    private void BuildChrome()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = UiTheme.PrimaryDark, Padding = new Padding(24, 12, 24, 8) };
        var brand = new Label { Text = "CKASHEF", Dock = DockStyle.Right, Width = 210, Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = UiTheme.Accent, TextAlign = ContentAlignment.MiddleRight };
        var title = new Label { Text = "مرکز فرمان و مدیریت عملیات", Dock = DockStyle.Fill, Font = UiTheme.HeaderFont, ForeColor = UiTheme.Text, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 18, 0) };
        var user = new Label { Text = AppSession.CurrentUser == null ? "" : $"{AppSession.CurrentUser.FullName}  •  سطح دسترسی {AppSession.CurrentUser.AuthorizationLevel}", Dock = DockStyle.Left, Width = 280, ForeColor = UiTheme.TextMuted, TextAlign = ContentAlignment.MiddleLeft };
        top.Controls.Add(user); top.Controls.Add(title); top.Controls.Add(brand);

        var nav = new Panel { Dock = DockStyle.Right, Width = 220, BackColor = Color.FromArgb(9, 14, 24), Padding = new Padding(12) };
        string[] items = { "داشبورد", "عملیات", "شناسایی‌ها", "اسکن مجاز", "گزارش‌ها", "پرسنل", "تنظیمات" };
        var y = 8;
        foreach (var item in items)
        {
            var b = new Button { Text = item, Width = 196, Height = 43, Location = new Point(0, y), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(20, 28, 42), ForeColor = UiTheme.Text, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(12,0,12,0) };
            b.FlatAppearance.BorderSize = 0; b.MouseEnter += (_,_) => b.BackColor = Color.FromArgb(30, 62, 88); b.MouseLeave += (_,_) => b.BackColor = Color.FromArgb(20,28,42); b.Click += (_,_) => Navigate(item); nav.Controls.Add(b); y += 50;
        }
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(12,18,28), Padding = new Padding(8,0,8,0) };
        _status.Dock = DockStyle.Fill; _status.ForeColor = UiTheme.Success; _status.TextAlign = ContentAlignment.MiddleRight; _status.Text = "● سامانه آماده است";
        _progress.Dock = DockStyle.Left; _progress.Width = 260; bottom.Controls.Add(_status); bottom.Controls.Add(_progress);
        Controls.Add(_content); Controls.Add(nav); Controls.Add(bottom); Controls.Add(top);
    }

    private void Navigate(string item) { switch(item){ case "داشبورد":ShowDashboard();break; case "عملیات":ShowOperations();break; case "شناسایی‌ها":ShowDetections();break; case "اسکن مجاز":ShowScan();break; case "گزارش‌ها":ShowReports();break; case "پرسنل":ShowPersonnel();break; case "تنظیمات":ShowSettings();break; } }
    private void ClearContent(){_content.Controls.Clear();_opsGrid=null;_detGrid=null;_persGrid=null;}

    private void ShowDashboard()
    {
        ClearContent(); var s=_db.GetDashboardStats();
        _content.Controls.Add(UiTheme.SectionTitle("داشبورد عملیاتی"));
        var cards=new FlowLayoutPanel{Dock=DockStyle.Top,Height=130,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,8,0,8)};
        cards.Controls.Add(UiTheme.MetricCard("عملیات فعال",s.ActiveOperations.ToString(),UiTheme.Accent)); cards.Controls.Add(UiTheme.MetricCard("کل شناسایی‌ها",s.TotalDetections.ToString(),UiTheme.Primary)); cards.Controls.Add(UiTheme.MetricCard("منتظر اقدام",s.PendingActions.ToString(),UiTheme.Warning)); cards.Controls.Add(UiTheme.MetricCard("ضبط‌شده",s.SeizedDevices.ToString(),UiTheme.Danger)); cards.Controls.Add(UiTheme.MetricCard("مصرف kW",s.TotalPowerKw.ToString("F1"),Color.FromArgb(135,100,220))); cards.Controls.Add(UiTheme.MetricCard("پرسنل فعال",s.PersonnelCount.ToString(),Color.FromArgb(65,150,185)));
        var grid=new DataGridView{Dock=DockStyle.Fill};UiTheme.StyleDataGrid(grid);grid.Columns.Add("Province","استان");grid.Columns.Add("Count","تعداد");grid.Columns.Add("Power","مصرف (W)");foreach(var x in _db.GetRegionalStats())grid.Rows.Add(x.Province,x.Count,x.Power.ToString("F0"));
        _content.Controls.Add(grid);_content.Controls.Add(cards);SetStatus("داشبورد عملیاتی به‌روز شد");
    }

    private void ShowOperations()
    {
        ClearContent();_content.Controls.Add(UiTheme.SectionTitle("مدیریت عملیات"));var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=52,FlowDirection=FlowDirection.RightToLeft};
        var add=new Button{Text="＋ عملیات جدید",Width=130};var close=new Button{Text="بستن عملیات",Width=120};var refresh=new Button{Text="بازنشانی",Width=100};UiTheme.StylePrimaryButton(add);UiTheme.StyleDangerButton(close);UiTheme.StyleSecondaryButton(refresh);add.Click+=(_,_)=>CreateOperationDialog();close.Click+=(_,_)=>CloseSelectedOperation();refresh.Click+=(_,_)=>LoadOperationsGrid();bar.Controls.AddRange(new Control[]{add,close,refresh});
        _opsGrid=new DataGridView{Dock=DockStyle.Fill};UiTheme.StyleDataGrid(_opsGrid);foreach(var c in new[]{("Code","کد عملیات"),("Region","منطقه"),("Auth","مجازکننده"),("Start","شروع"),("Status","وضعیت"),("Count","شناسایی"),("Power","مصرف W"),("Notes","یادداشت")})_opsGrid.Columns.Add(c.Item1,c.Item2);_content.Controls.Add(_opsGrid);_content.Controls.Add(bar);LoadOperationsGrid();
    }
    private void LoadOperationsGrid(){if(_opsGrid==null)return;_opsGrid.Rows.Clear();foreach(var o in _db.GetOperations())_opsGrid.Rows.Add(o.OperationCode,o.Region,o.AuthorizedBy,o.StartTime.ToString("yyyy-MM-dd HH:mm"),o.Status,o.DetectionCount,o.TotalConsumption.ToString("F0"),o.Notes);SetStatus($"{_opsGrid.Rows.Count} عملیات");}

    private void CreateOperationDialog(){using var d=new Form{Text="ایجاد عملیات جدید",ClientSize=new Size(430,300),StartPosition=FormStartPosition.CenterParent,RightToLeft=RightToLeft.Yes,RightToLeftLayout=true,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false};var code=new TextBox{PlaceholderText="کد عملیات",Dock=DockStyle.Top,Height=34};var region=new TextBox{PlaceholderText="منطقه / استان",Dock=DockStyle.Top,Height=34};var notes=new TextBox{PlaceholderText="یادداشت",Dock=DockStyle.Top,Height=70,Multiline=true};var ok=new Button{Text="ثبت عملیات",Dock=DockStyle.Bottom,Height=42,DialogResult=DialogResult.OK};UiTheme.StylePrimaryButton(ok);d.Controls.Add(ok);d.Controls.Add(notes);d.Controls.Add(region);d.Controls.Add(code);if(d.ShowDialog(this)!=DialogResult.OK)return;if(string.IsNullOrWhiteSpace(code.Text)||string.IsNullOrWhiteSpace(region.Text)){MessageBox.Show("کد و منطقه الزامی است.");return;}try{_db.CreateOperation(new GovernmentOperation{OperationCode=code.Text.Trim(),Region=region.Text.Trim(),AuthorizedBy=AppSession.CurrentUser?.FullName??"نامشخص",StartTime=DateTime.Now,Status="InProgress",Notes=notes.Text.Trim()});LoadOperationsGrid();}catch(Exception ex){MessageBox.Show("خطا: "+ex.Message);}}
    private void CloseSelectedOperation(){if(_opsGrid?.CurrentRow==null)return;var code=_opsGrid.CurrentRow.Cells[0].Value?.ToString();if(string.IsNullOrWhiteSpace(code))return;if(MessageBox.Show($"عملیات {code} بسته شود؟","تأیید",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;_db.UpdateOperationStatus(code,"Completed",DateTime.Now);LoadOperationsGrid();}

    private void ShowDetections()
    {
        ClearContent();_content.Controls.Add(UiTheme.SectionTitle("شناسایی‌ها و اطلاعات مشترک"));var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=52,FlowDirection=FlowDirection.RightToLeft};_opFilter=new ComboBox{Width=180,DropDownStyle=ComboBoxStyle.DropDownList};_statusFilter=new ComboBox{Width=160,DropDownStyle=ComboBoxStyle.DropDownList};UiTheme.StyleComboBox(_opFilter);UiTheme.StyleComboBox(_statusFilter);_opFilter.Items.Add("همه عملیات");foreach(var o in _db.GetOperations())_opFilter.Items.Add(o.OperationCode);_opFilter.SelectedIndex=0;_statusFilter.Items.AddRange(new object[]{"همه","منتظر‌دستورالعمل","ضبط‌شده","قطع‌برق","اخطار","بایگانی"});_statusFilter.SelectedIndex=0;var refresh=new Button{Text="اعمال فیلتر",Width=105};UiTheme.StylePrimaryButton(refresh);refresh.Click+=(_,_)=>LoadDetectionsGrid();bar.Controls.AddRange(new Control[]{refresh,_statusFilter,_opFilter});
        _detGrid=new DataGridView{Dock=DockStyle.Fill};UiTheme.StyleDataGrid(_detGrid);foreach(var c in new[]{("IP","IP"),("Subscriber","نام و نام خانوادگی"),("Phone","شماره همراه"),("Province","استان"),("City","شهر"),("Street","نشانی"),("Postal","کدپستی"),("ISP","اپراتور/ISP"),("Device","دستگاه"),("Power","مصرف W"),("Status","وضعیت"),("Time","زمان")})_detGrid.Columns.Add(c.Item1,c.Item2);_content.Controls.Add(_detGrid);_content.Controls.Add(bar);LoadDetectionsGrid();
    }
    private void LoadDetectionsGrid(){if(_detGrid==null)return;_detGrid.Rows.Clear();var op=_opFilter?.SelectedItem?.ToString();if(op=="همه عملیات")op=null;var st=_statusFilter?.SelectedItem?.ToString();if(st=="همه")st=null;foreach(var d in _db.GetDetections(op,st))_detGrid.Rows.Add(d.IPAddress,d.SubscriberName,d.PhoneNumber,d.Province,d.City,d.Street,d.PostalCode,d.ISP,d.DeviceModel,d.EstimatedConsumption.ToString("F0"),d.ActionStatus,d.DetectionTime.ToString("yyyy-MM-dd HH:mm"));SetStatus($"{_detGrid.Rows.Count} رکورد");}

    private void ShowScan(){ClearContent();_content.Controls.Add(UiTheme.SectionTitle("اسکن مجاز"));var panel=UiTheme.MakeCard("پارامترهای عملیات",220);panel.Dock=DockStyle.Top;_scanRegion=new TextBox{PlaceholderText="منطقه مجاز",Dock=DockStyle.Top,Height=34};_scanOpCode=new TextBox{PlaceholderText="کد عملیات مجاز",Dock=DockStyle.Top,Height=34};_ipList=new TextBox{PlaceholderText="IPهای مجاز، هر خط یک مورد",Dock=DockStyle.Top,Height=90,Multiline=true,ScrollBars=ScrollBars.Vertical};_simMode=new CheckBox{Text="حالت شبیه‌سازی / Demo",Dock=DockStyle.Top,Height=30,ForeColor=UiTheme.Text};foreach(var x in new Control[]{_scanRegion,_scanOpCode,_ipList})UiTheme.StyleTextBox(x);var start=new Button{Text="▶ شروع اسکن",Width=130};var stop=new Button{Text="■ توقف",Width=100};UiTheme.StylePrimaryButton(start);UiTheme.StyleDangerButton(stop);start.Click+=async(_,_)=>await StartScan();stop.Click+=(_,_)=>_scanCts?.Cancel();panel.Controls.Add(stop);panel.Controls.Add(start);panel.Controls.Add(_simMode);panel.Controls.Add(_ipList);panel.Controls.Add(_scanOpCode);panel.Controls.Add(_scanRegion);_content.Controls.Add(panel);}

    private async Task StartScan(){if(_scanCts!=null)return;var op=_scanOpCode?.Text.Trim();if(string.IsNullOrWhiteSpace(op)){MessageBox.Show("کد عملیات مجاز الزامی است.");return;}var ips=(_ipList?.Text??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);if(ips.Length==0){MessageBox.Show("حداقل یک IP مجاز وارد کنید.");return;}try{_scanCts=new CancellationTokenSource();SetStatus("اسکن مجاز در حال اجراست…");await _tracker.ScanAsync(op,ips,_scanCts.Token);SetStatus("اسکن با موفقیت پایان یافت");}catch(OperationCanceledException){SetStatus("اسکن متوقف شد");}catch(Exception ex){MessageBox.Show("خطای اسکن: "+ex.Message);}finally{_scanCts.Dispose();_scanCts=null;}}

    private void ShowReports(){ClearContent();_content.Controls.Add(UiTheme.SectionTitle("گزارش‌ها"));var info=UiTheme.MakeCard("گزارش رسمی عملیات",170);info.Controls.Add(new Label{Text="گزارش‌ها باید فقط بر اساس داده‌های ثبت‌شده و منابع مجاز تولید شوند.",Dock=DockStyle.Fill,ForeColor=UiTheme.TextMuted,TextAlign=ContentAlignment.MiddleRight,Padding=new Padding(12)});_content.Controls.Add(info);}
    private void ShowPersonnel(){ClearContent();_content.Controls.Add(UiTheme.SectionTitle("پرسنل و سطح دسترسی"));_persGrid=new DataGridView{Dock=DockStyle.Fill};UiTheme.StyleDataGrid(_persGrid);foreach(var c in new[]{("Name","نام و نام خانوادگی"),("Badge","شناسه"),("Department","واحد"),("Role","نقش"),("Level","سطح دسترسی"),("Status","وضعیت"),("Last","آخرین ورود")})_persGrid.Columns.Add(c.Item1,c.Item2);_content.Controls.Add(_persGrid);}
    private void ShowSettings(){ClearContent();_content.Controls.Add(UiTheme.SectionTitle("تنظیمات سامانه"));var card=UiTheme.MakeCard("وضعیت محیط اجرا",170);card.Controls.Add(new Label{Text=$"پایگاه داده: {_db.DatabasePath}\nکاربر جاری: {AppSession.CurrentUser?.FullName}\nسطح دسترسی: {AppSession.CurrentUser?.AuthorizationLevel}",Dock=DockStyle.Fill,ForeColor=UiTheme.Text,TextAlign=ContentAlignment.MiddleRight});_content.Controls.Add(card);}
    private void OnScanProgress(object? sender, TrackingProgressEventArgs e){if(IsDisposed)return;BeginInvoke(new Action(()=>{_progress.Value=Math.Clamp(e.Percent,0,100);_status.Text=$"● {e.Message}";}));}
    private void SetStatus(string text)=>_status.Text="● "+text;
}
