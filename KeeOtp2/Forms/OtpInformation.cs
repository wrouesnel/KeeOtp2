﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassLib.Security;
using OtpSharp;
using ZXing;

namespace KeeOtp2
{
    public partial class OtpInformation : Form
    {
        public OtpAuthData Data { get; set; }
        bool fullyLoaded = false;
        KeePassLib.PwEntry entry;
        IPluginHost host;

        public OtpInformation(OtpAuthData data, KeePassLib.PwEntry entry, IPluginHost host)
        {
            InitializeComponent();
            this.Shown += (sender, e) => FormWasShown();

            pictureBoxBanner.Image = KeePass.UI.BannerFactory.CreateBanner(pictureBoxBanner.Width,
                pictureBoxBanner.Height,
                KeePass.UI.BannerStyle.Default,
                Resources.lock_key.GetThumbnailImage(32, 32, null, IntPtr.Zero),
                "Configuration",
                "Set up the key for generating one time passwords");

            this.Icon = host.MainWindow.Icon;

            this.Data = data;
            this.entry = entry;
            this.host = host;

            this.TopMost = host.MainWindow.TopMost;

            if (this.Data != null && this.Data.KeeOtp1Mode)
            {
                buttonScanQRCode.Visible = false;
                buttonMigrate.Visible = true;
                pictureBoxMigrateQuestionmark.Visible = true;
                pictureBoxMigrateQuestionmark.Image = SystemIcons.Question.ToBitmap();

                ToolTip toolTip = new ToolTip();
                toolTip.ToolTipTitle = "Why am I seeing this?";
                toolTip.IsBalloon = true;
                toolTip.SetToolTip(buttonMigrate, "Since KeePass 2.47, TOTPs can generated by a built-in function.\nYou can use this button to easily migrate to the built-in function.\n\n(It is also recommended!)");
                toolTip.SetToolTip(pictureBoxMigrateQuestionmark, "Since KeePass 2.47, TOTPs can generated by a built-in function.\nYou can use this button to easily migrate to the built-in function.\n\n(It is also recommended!)");
            }
        }

        private void OtpInformation_Load(object sender, EventArgs e)
        {
            this.Left = this.host.MainWindow.Left + 20;
            this.Top = this.host.MainWindow.Top + 20;
        }

        private void FormWasShown()
        {
            loadData();
        }

        private void loadData()
        {
            if (this.Data != null)
            {
                this.textBoxKey.Text = this.Data.GetPlainSecret();

                if (this.Data.Period != 30 || this.Data.KeeOtp1Mode ||
                    this.Data.Encoding != OtpSecretEncoding.Base32 ||
                    this.Data.Digits != 6 || this.Data.Algorithm != OtpHashMode.Sha1)
                {
                    this.checkBoxCustomSettings.Checked = true;
                }

                this.textBoxStep.Text = this.Data.Period.ToString();

                this.checkboxOldKeeOtp.Checked = this.Data.KeeOtp1Mode;

                if (this.Data.Encoding == OtpSecretEncoding.Base64)
                {
                    this.radioButtonBase32.Checked = false;
                    this.radioButtonBase64.Checked = true;
                    this.radioButtonHex.Checked = false;
                    this.radioButtonUtf8.Checked = false;
                }
                else if (this.Data.Encoding == OtpSecretEncoding.Hex)
                {
                    this.radioButtonBase32.Checked = false;
                    this.radioButtonBase64.Checked = false;
                    this.radioButtonHex.Checked = true;
                    this.radioButtonUtf8.Checked = false;
                }
                else if (this.Data.Encoding == OtpSecretEncoding.UTF8)
                {
                    this.radioButtonBase32.Checked = false;
                    this.radioButtonBase64.Checked = false;
                    this.radioButtonHex.Checked = false;
                    this.radioButtonUtf8.Checked = true;
                }
                else // default encoding
                {
                    this.radioButtonBase32.Checked = true;
                    this.radioButtonBase64.Checked = false;
                    this.radioButtonHex.Checked = false;
                    this.radioButtonUtf8.Checked = false;

                }

                if (this.Data.Digits == 8)
                {
                    this.radioButtonSix.Checked = false;
                    this.radioButtonEight.Checked = true;
                }
                else // default size
                {
                    this.radioButtonSix.Checked = true;
                    this.radioButtonEight.Checked = false;
                }

                if (this.Data.Algorithm == OtpHashMode.Sha256)
                {
                    this.radioButtonSha1.Checked = false;
                    this.radioButtonSha256.Checked = true;
                    this.radioButtonSha512.Checked = false;
                }
                else if (this.Data.Algorithm == OtpHashMode.Sha512)
                {
                    this.radioButtonSha1.Checked = false;
                    this.radioButtonSha256.Checked = false;
                    this.radioButtonSha512.Checked = true;
                }
                else // default hashmode
                {
                    this.radioButtonSha1.Checked = true;
                    this.radioButtonSha256.Checked = false;
                    this.radioButtonSha512.Checked = false;
                }
            }
            else
            {
                this.textBoxStep.Text = "30";
                this.radioButtonSha1.Checked = true;
                this.radioButtonSha256.Checked = false;
                this.radioButtonSha512.Checked = false;

                this.radioButtonSix.Checked = true;
                this.radioButtonEight.Checked = false;

                this.radioButtonBase32.Checked = true;
                this.radioButtonBase64.Checked = false;
                this.radioButtonHex.Checked = false;
                this.radioButtonUtf8.Checked = false;
            }

            SetCustomSettingsState(false);
            this.fullyLoaded = true;
        }

        private void OtpInformation_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.DialogResult == DialogResult.Cancel)
                return;
            try
            {
                if (textBoxKey.Text.StartsWith("otpauth://"))
                {
                    this.Data = OtpAuthUtils.uriToOtpAuthData(new Uri(textBoxKey.Text));
                }
                else
                {
                    string secret = textBoxKey.Text.Replace(" ", string.Empty).Replace("-", string.Empty);
                    if (string.IsNullOrEmpty(this.textBoxKey.Text))
                    {
                        MessageBox.Show("A key must be set", "Missing key", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        e.Cancel = true;
                        return;
                    }

                    
                    this.Data = new OtpAuthData();

                    // encoding
                    if (this.radioButtonBase32.Checked)
                        this.Data.Encoding = OtpSecretEncoding.Base32;
                    else if (this.radioButtonBase64.Checked)
                        this.Data.Encoding = OtpSecretEncoding.Base64;
                    else if (this.radioButtonHex.Checked)
                        this.Data.Encoding = OtpSecretEncoding.Hex;
                    else if (this.radioButtonUtf8.Checked)
                        this.Data.Encoding = OtpSecretEncoding.UTF8;

                    secret = OtpAuthUtils.correctPlainSecret(secret, this.Data.Encoding);

                    // Validate secret (catch)
                    OtpAuthUtils.validatePlainSecret(secret, this.Data.Encoding);

                    int step = 30;
                    if (int.TryParse(this.textBoxStep.Text, out step))
                    {
                        if (step != 30)
                        {
                            if (step <= 0)
                            {
                                this.textBoxStep.Text = "30";
                                MessageBox.Show("The time step must be a non-zero positive integer. The standard value is 30.", "Invalid time step", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                e.Cancel = true;
                                return;
                            }
                            else if (MessageBox.Show("You have selected a non-standard time step. You should only proceed if you were specifically told to use this time step size.\nDefault Value: 30\n\nDo you wish to proceed?", "Non-standard time step size", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.No)
                            {
                                e.Cancel = true;
                                return;
                            }
                        }
                    }
                    else
                    {
                        this.textBoxStep.Text = "30";
                        MessageBox.Show("The time step must be a non-zero positive integer. The standard value is 30.", "Invalid time step", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        e.Cancel = true;
                        return;
                    }

                    // size
                    if (this.radioButtonSix.Checked)
                        this.Data.Digits = 6;
                    else if (this.radioButtonEight.Checked)
                        this.Data.Digits = 8;

                    // step
                    this.Data.Period = step;

                    // hashmode
                    if (this.radioButtonSha1.Checked)
                        this.Data.Algorithm = OtpHashMode.Sha1;
                    else if (this.radioButtonSha256.Checked)
                        this.Data.Algorithm = OtpHashMode.Sha256;
                    else if (this.radioButtonSha512.Checked)
                        this.Data.Algorithm = OtpHashMode.Sha512;

                    this.Data.SetPlainSecret(secret);
                }

                this.entry.CreateBackup(this.host.Database);

                if (checkboxOldKeeOtp.Checked)
                {
                    OtpAuthUtils.migrateToKeeOtp1String(this.Data, this.entry);
                }
                else
                {
                    OtpAuthUtils.migrateToBuiltInOtp(this.Data, this.entry);
                }
                if (OtpAuthUtils.loadData(this.entry) != null)
                    OtpAuthUtils.purgeLoadedFields(this.Data, this.entry);

                this.entry.Touch(true);
                this.host.MainWindow.ActiveDatabase.Modified = true;
                this.host.MainWindow.UpdateUI(false, null, false, null, false, null, true);
            }
            catch (InvalidBase32FormatException ex)
            {
                MessageBox.Show(ex.Message, "Invalid Base32 Format!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                e.Cancel = true;
                return;
            }
            catch (InvalidBase64FormatException ex)
            {
                MessageBox.Show(ex.Message, "Invalid Base64 Format!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                e.Cancel = true;
                return;
            }
            catch (InvalidHexFormatException ex)
            {
                MessageBox.Show(ex.Message, "Invalid Hex Format!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                e.Cancel = true;
                return;
            }
            catch (Exception)
            {
                MessageBox.Show("There happened an error. Please check your entered key and your settings!", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                e.Cancel = true;
                return;
            }
        }

        private void checkBoxCustomSettings_CheckedChanged(object sender, EventArgs e)
        {
            SetCustomSettingsState(this.fullyLoaded);
        }

        private void SetCustomSettingsState(bool showWarning)
        {
            var useCustom = this.checkBoxCustomSettings.Checked;

            this.radioButtonBase32.Enabled =
                this.radioButtonBase64.Enabled =
                this.radioButtonHex.Enabled =
                this.radioButtonUtf8.Enabled =
                this.radioButtonSix.Enabled =
                this.radioButtonEight.Enabled =
                this.textBoxStep.Enabled = 
                this.radioButtonSha1.Enabled = 
                this.radioButtonSha256.Enabled = 
                this.radioButtonSha512.Enabled = 
                this.checkboxOldKeeOtp.Enabled = useCustom;
        }

        private void buttonMigrate_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Do you want to replace the Auto-Type key {TOTP} with the built-in key {TIMEOTP}?", "Migrate Auto-Type", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                this.entry.AutoType.DefaultSequence = this.entry.AutoType.DefaultSequence.Replace("{TOTP}", "{TIMEOTP}");
                foreach (KeePassLib.Collections.AutoTypeAssociation ata in this.entry.AutoType.Associations)
                {
                    ata.Sequence = ata.Sequence.Replace("{TOTP}", "{TIMEOTP}");
                }
            }

            checkboxOldKeeOtp.Checked = false;
            this.DialogResult = DialogResult.OK;
        }

        private void textBoxKey_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
                this.DialogResult = DialogResult.OK;
        }

        private void textBoxKey_TextChanged(object sender, EventArgs e)
        {
            if (textBoxKey.Text.StartsWith("otpauth://"))
            {
                llbl_LoadUri.Visible = true;
                llbl_LoadUri.Enabled = true;
            }
        }

        private void llbl_LoadUri_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            llbl_LoadUri.Visible = false;
            llbl_LoadUri.Enabled = false;
            try
            {
                OtpAuthData data = OtpAuthUtils.uriToOtpAuthData(new Uri(textBoxKey.Text));
                if (data != null)
                {
                    this.Data = data;
                    loadData();
                }
                else
                    MessageBox.Show("The give Uri does not contain a secret.\n\nA secret is required!", "Failure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (InvalidBase32FormatException ex)
            {
                MessageBox.Show("The secret encoding is invalid. Uri strings only allow Base32 encoding. Please validate the secret!\n\nError Message:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (InvalidUriFormat ex)
            {
                MessageBox.Show("The Uri you have entered is invalid. The Uri string have to be started with 'otpauth://'.\n\nError Message:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("There happend an error! Please try again.\n\nError Message:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonScanQRCode_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Ensure that the QRCode is somewhere visible on the screen.\nThe plugin will look for any QRCode on the screen.\n\nPress 'OK' to start the scan!", "Scan QRCode", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
            {
                scanQRCode();
            }
        }

        private void scanQRCode()
        {
            Uri uri = null;
            IBarcodeReader reader = new BarcodeReader();
            Bitmap bmpScreenshot;
            Graphics gfxScreenshot;

            this.Hide();
            foreach (Screen sc in Screen.AllScreens)
            {
                bmpScreenshot = new Bitmap(sc.Bounds.Width, sc.Bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                gfxScreenshot = Graphics.FromImage(bmpScreenshot);
                gfxScreenshot.CopyFromScreen(sc.Bounds.X, sc.Bounds.Y, 0, 0, sc.Bounds.Size, CopyPixelOperation.SourceCopy);
                var result = reader.Decode(bmpScreenshot);
                if (result != null)
                    if (result.ToString().StartsWith("otpauth"))
                        uri = new Uri(result.ToString());
            }

            this.Show();

            if (uri != null)
            {
                MessageBox.Show("Great! The QRCode was found and the credentials will now be configured for you!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Data = OtpAuthUtils.uriToOtpAuthData(uri);
                loadData();
            }
            else
            {
                if (MessageBox.Show("No QRCodes found!\n\nPlease ensure that the QRCode is somewhere visible on the screen.\n\nPress 'Retry' to restart the scan!", "No QRCode found", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) == DialogResult.Retry)
                {
                    scanQRCode();
                }
            }
        }
    }
}
