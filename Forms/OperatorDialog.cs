using GovernmentMiningApp.Models;
using GovernmentMiningApp.Services;
using GovernmentMiningApp.Ui;

namespace GovernmentMiningApp.Forms;

/// <summary>
/// ثبت اطلاعات مشترک تنها بر پایه پاسخ رسمی مراجع ذی‌صلاح.
/// سامانه هیچ اطلاعات هویتی یا نشانی‌ای را حدس نمی‌زند؛ این فرم فقط
/// پاسخ دریافتی از استعلام رسمی را همراه با شماره نامه و مرجع پاسخ‌دهنده ثبت می‌کند.
/// </summary>
public static class OperatorDialog
{
    public static bool Show(IWin32Window owner, DatabaseService db, DetectedDevice device)
    {
        using var dlg = new Form
        {
            Text = $"ثبت اطلاعات استعلام‌شده از اپراتور — {device.IPAddress}",
            ClientSize = new Size(560, 520),
            StartPosition = FormStartPosition.CenterParent,
            RightToLeft = RightToLeft.Yes,
            Font = UiTheme.BodyFont,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false
        };

        var subscriber = AddField(dlg, "نام/نام خانوادگی مشترک", 10, 460);
        var phone = AddField(dlg, "شماره تماس", 48, 460);
        var province = AddField(dlg, "استان", 86, 460);
        var city = AddField(dlg, "شهر", 124, 460);
        var street = AddField(dlg, "نشانی", 162, 460);
        var postal = AddField(dlg, "کد پستی", 200, 460);
        var reference = AddField(dlg, "شماره نامه/استعلام رسمی (الزامی)", 238, 460);
        var authority = AddField(dlg, "مرجع پاسخ‌دهنده (الزامی)", 276, 460);
        var received = AddField(dlg, "تاریخ دریافت پاسخ", 314, 460);
        received.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        var notes = new TextBox
        {
            Left = 120,
            Top = 352,
            Width = 400,
            Height = 90,
            Multiline = true
        };
        dlg.Controls.Add(new Label
        {
            Text = "یادداشت (متن نامه، مدت اعتبار پاسخ و ...)",
            Left = 20,
            Top = 352,
            Width = 240,
            TextAlign = ContentAlignment.TopRight
        });
        dlg.Controls.Add(notes);

        var ok = new Button { Text = "ثبت رکورد استعلام", Left = 20, Top = 452, Width = 150, DialogResult = DialogResult.OK };
        UiTheme.StylePrimaryButton(ok);
        dlg.Controls.Add(ok);
        dlg.AcceptButton = ok;

        if (dlg.ShowDialog(owner) != DialogResult.OK) return false;

        if (string.IsNullOrWhiteSpace(reference.Text) || string.IsNullOrWhiteSpace(authority.Text))
        {
            MessageBox.Show("شماره نامه استعلام و مرجع پاسخ‌دهنده الزامی است؛ سند بدون ارجاع قابل دفاع نیست.",
                "توجه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var payload = string.Join("|", new[]
        {
            device.IPAddress, subscriber.Text.Trim(), phone.Text.Trim(), province.Text.Trim(),
            city.Text.Trim(), street.Text.Trim(), postal.Text.Trim(),
            reference.Text.Trim(), authority.Text.Trim(), received.Text.Trim(), notes.Text.Trim()
        });

        var record = new OperatorRecord
        {
            RecordID = Guid.NewGuid().ToString("N"),
            OperationID = device.OperationID,
            IPAddress = device.IPAddress,
            SubscriberName = subscriber.Text.Trim(),
            PhoneNumber = phone.Text.Trim(),
            Province = province.Text.Trim(),
            City = city.Text.Trim(),
            Street = street.Text.Trim(),
            PostalCode = postal.Text.Trim(),
            RequestReference = reference.Text.Trim(),
            SourceAuthority = authority.Text.Trim(),
            RequestedBy = AppSession.CurrentUser?.FullName ?? "سیستم",
            RequestedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            ReceivedAt = received.Text.Trim(),
            PayloadHash = MinerFingerprintService.Sha256Hex(payload),
            Notes = notes.Text.Trim()
        };

        db.SaveOperatorRecord(record, applyToDetection: true);
        return true;
    }

    private static TextBox AddField(Form dlg, string label, int top, int width)
    {
        dlg.Controls.Add(new Label
        {
            Text = label,
            Left = 20,
            Top = top,
            Width = 260,
            TextAlign = ContentAlignment.MiddleRight
        });
        var tb = new TextBox { Left = 290, Top = top, Width = width - 310 };
        dlg.Controls.Add(tb);
        return tb;
    }
}
