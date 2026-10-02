using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiscordRichPresenceInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class InstallerForm : Form
    {
        private const string Warning =
            "Esta aplicación se conecta a Discord usando el token de una cuenta de usuario. " +
            "La automatización de cuentas de usuario puede infringir los términos de Discord y " +
            "provocar la suspensión de la cuenta. Usa la app bajo tu responsabilidad.\n\n" +
            "El instalador no solicita ni almacena tu token; la app lo solicita al abrirse.";

        private readonly Button _installButton;
        private readonly Button _cancelButton;
        private readonly Button _closeButton;
        private readonly CheckBox _desktopShortcut;
        private readonly CheckBox _launchAfterInstall;
        private readonly Label _status;
        private readonly ProgressBar _progressBar;
        private readonly TextBox _log;
        private CancellationTokenSource _cancellation;

        public InstallerForm()
        {
            Text = "Discord Rich Presence - Instalador";
            MinimumSize = new Size(620, 530);
            Size = new Size(700, 600);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                Padding = new Padding(18)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(layout);

            var title = new Label
            {
                Text = "Instalar Discord Rich Presence",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8)
            };
            layout.Controls.Add(title, 0, 0);

            var description = new Label
            {
                Text = "El asistente comprobará Git, Python y WebView2, instalará lo que falte y descargará la app y sus dependencias.",
                AutoSize = true,
                MaximumSize = new Size(630, 0),
                Margin = new Padding(0, 0, 0, 12)
            };
            layout.Controls.Add(description, 0, 1);

            var warning = new Label
            {
                Text = Warning,
                AutoSize = true,
                MaximumSize = new Size(630, 0),
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 12),
                BackColor = Color.FromArgb(255, 244, 214),
                ForeColor = Color.FromArgb(92, 57, 0)
            };
            layout.Controls.Add(warning, 0, 2);

            var options = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            _desktopShortcut = new CheckBox
            {
                Text = "Crear acceso directo en el escritorio",
                AutoSize = true,
                Checked = true
            };
            _launchAfterInstall = new CheckBox
            {
                Text = "Abrir la aplicación al terminar",
                AutoSize = true,
                Checked = true
            };
            options.Controls.Add(_desktopShortcut);
            options.Controls.Add(_launchAfterInstall);
            layout.Controls.Add(options, 0, 3);

            _status = new Label
            {
                Text = "Listo para instalar.",
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 6)
            };
            layout.Controls.Add(_status, 0, 4);

            var logPanel = new Panel { Dock = DockStyle.Fill };
            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 251),
                Font = new Font("Consolas", 9F)
            };
            logPanel.Controls.Add(_log);
            _progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 25,
                Dock = DockStyle.Bottom,
                Height = 8,
                Visible = false
            };
            logPanel.Controls.Add(_progressBar);
            layout.Controls.Add(logPanel, 0, 5);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = new Padding(0, 12, 0, 0)
            };
            _closeButton = new Button { Text = "Cerrar", AutoSize = true };
            _cancelButton = new Button { Text = "Cancelar", AutoSize = true, Enabled = false };
            _installButton = new Button { Text = "Instalar", AutoSize = true };
            _installButton.Click += InstallClicked;
            _cancelButton.Click += CancelClicked;
            _closeButton.Click += delegate { Close(); };
            buttons.Controls.Add(_closeButton);
            buttons.Controls.Add(_cancelButton);
            buttons.Controls.Add(_installButton);
            layout.Controls.Add(buttons, 0, 6);
        }

        private async void InstallClicked(object sender, EventArgs e)
        {
            var answer = MessageBox.Show(
                "Se instalarán Git, Python o WebView2 si faltan. También se descargará el código desde " +
                "GitHub y las dependencias desde PyPI.\n\n" + Warning +
                "\n\n¿Quieres continuar?",
                "Confirmar instalación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                return;
            }

            SetBusy(true);
            _cancellation = new CancellationTokenSource();
            var progress = new Progress<string>(AppendLog);

            try
            {
                await InstallerService.InstallAsync(
                    progress,
                    _cancellation.Token,
                    _desktopShortcut.Checked,
                    _launchAfterInstall.Checked);

                _status.Text = "Instalación completada.";
                AppendLog("Instalación completada.");
                MessageBox.Show(
                    "Discord Rich Presence se instaló correctamente.",
                    "Instalación completada",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Instalación cancelada.";
                AppendLog("Instalación cancelada.");
            }
            catch (Win32Exception exception)
            {
                ShowInstallationError(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                ShowInstallationError(exception.Message);
            }
            catch (IOException exception)
            {
                ShowInstallationError(exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                ShowInstallationError(exception.Message);
            }
            catch (COMException exception)
            {
                ShowInstallationError(exception.Message);
            }
            catch (SecurityException exception)
            {
                ShowInstallationError(exception.Message);
            }
            finally
            {
                _cancellation.Dispose();
                _cancellation = null;
                SetBusy(false);
            }
        }

        private void CancelClicked(object sender, EventArgs e)
        {
            if (_cancellation != null)
            {
                _status.Text = "Cancelando...";
                _cancellation.Cancel();
            }
        }

        private void ShowInstallationError(string message)
        {
            _status.Text = "La instalación no pudo completarse.";
            AppendLog("ERROR: " + message);
            MessageBox.Show(
                message + "\n\nPuedes revisar el registro de esta ventana y volver a intentarlo.",
                "Error de instalación",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void AppendLog(string message)
        {
            _log.AppendText(message + Environment.NewLine);
        }

        private void SetBusy(bool busy)
        {
            _installButton.Enabled = !busy;
            _desktopShortcut.Enabled = !busy;
            _launchAfterInstall.Enabled = !busy;
            _cancelButton.Enabled = busy;
            _closeButton.Enabled = !busy;
            _progressBar.Visible = busy;
            if (busy)
            {
                _status.Text = "Preparando la instalación...";
            }
        }
    }
}
