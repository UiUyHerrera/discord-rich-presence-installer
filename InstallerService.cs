using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordRichPresenceInstaller
{
    internal static class InstallerService
    {
        private const string RepositoryUrl =
            "https://github.com/UiUyHerrera/discord-rich-presence-desktop.git";
        private const string InstallFolderName = "EstadoDiscord";

        public static async Task InstallAsync(
            IProgress<string> progress,
            CancellationToken cancellationToken,
            bool createDesktopShortcut,
            bool launchAfterInstall)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var winget = FindExecutable("winget.exe");
            if (winget == null)
            {
                throw new InvalidOperationException(
                    "No se encontró winget. Instala o actualiza \"Instalador de aplicaciones\" " +
                    "desde Microsoft Store y vuelve a ejecutar este instalador.");
            }

            var git = FindExecutable("git.exe");
            if (git == null)
            {
                progress.Report("Git no está disponible. Instalando Git con winget...");
                await RunProcessAsync(
                    winget,
                    "install --id Git.Git --exact --silent --accept-package-agreements " +
                    "--accept-source-agreements",
                    progress,
                    cancellationToken);
                RefreshProcessPath();
                git = FindExecutable("git.exe");
            }

            if (git == null)
            {
                throw new InvalidOperationException(
                    "Git se instaló o se detectó, pero no se encontró git.exe. " +
                    "Cierra sesión o reinicia Windows para actualizar PATH y vuelve a intentarlo.");
            }
            progress.Report("Git disponible: " + git);

            var python = await FindSupportedPythonAsync(progress, cancellationToken);
            if (python == null)
            {
                progress.Report("Python 3.12 no está disponible. Instalando Python con winget...");
                await RunProcessAsync(
                    winget,
                    "install --id Python.Python.3.12 --exact --silent --accept-package-agreements " +
                    "--accept-source-agreements",
                    progress,
                    cancellationToken);
                RefreshProcessPath();
                python = await FindSupportedPythonAsync(progress, cancellationToken);
            }

            if (python == null)
            {
                throw new InvalidOperationException(
                    "No se encontró una instalación funcional de Python 3.11 o superior después " +
                    "de ejecutar winget.");
            }

            if (IsWebView2Installed())
            {
                progress.Report("WebView2 Runtime disponible.");
            }
            else
            {
                progress.Report("WebView2 Runtime no está disponible. Instalándolo con winget...");
                await RunProcessAsync(
                    winget,
                    "install --id Microsoft.EdgeWebView2Runtime --exact --silent " +
                    "--accept-package-agreements --accept-source-agreements",
                    progress,
                    cancellationToken);
            }

            var programsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs");
            var installPath = Path.Combine(programsFolder, InstallFolderName);
            var sourcePath = Path.Combine(installPath, "app.py");
            var gitMetadataPath = Path.Combine(installPath, ".git");

            Directory.CreateDirectory(programsFolder);
            if (Directory.Exists(installPath))
            {
                if (!File.Exists(sourcePath) || !Directory.Exists(gitMetadataPath))
                {
                    throw new InvalidOperationException(
                        "La carpeta de instalación ya existe, pero no contiene un clon completo de " +
                        "la aplicación. No se modificó su contenido: " + installPath);
                }

                var remoteUrl = await CaptureProcessOutputAsync(
                    git,
                    "-C " + Quote(installPath) + " remote get-url origin",
                    cancellationToken);
                if (!SameRepository(remoteUrl))
                {
                    throw new InvalidOperationException(
                        "La carpeta existente no corresponde al repositorio esperado. No se modificó: " +
                        installPath);
                }

                progress.Report(
                    "Se encontró una instalación anterior del mismo repositorio. " +
                    "Se reutilizará para reparar dependencias sin sobrescribir sus archivos.");
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report("Descargando la aplicación desde GitHub...");
                await RunProcessAsync(
                    git,
                    "clone --depth 1 --branch main " + Quote(RepositoryUrl) + " " + Quote(installPath),
                    progress,
                    cancellationToken);
            }

            var venvPython = Path.Combine(installPath, ".venv", "Scripts", "python.exe");
            if (!File.Exists(venvPython))
            {
                if (Directory.Exists(Path.Combine(installPath, ".venv")))
                {
                    throw new InvalidOperationException(
                        "El entorno virtual anterior está incompleto. Para reparar la instalación, " +
                        "cierra la app, elimina únicamente la carpeta .venv dentro de " + installPath +
                        " y vuelve a ejecutar el instalador. No se modificaron los archivos del usuario.");
                }

                progress.Report("Creando un entorno aislado para las dependencias...");
                await RunProcessAsync(
                    python,
                    "-m venv " + Quote(Path.Combine(installPath, ".venv")),
                    progress,
                    cancellationToken);
            }

            progress.Report("Instalando las dependencias de la aplicación...");
            await RunProcessAsync(
                venvPython,
                "-m pip install -r " + Quote(Path.Combine(installPath, "requirements.txt")),
                progress,
                cancellationToken);

            CreateShortcuts(installPath, createDesktopShortcut);
            progress.Report("Se crearon los accesos directos.");

            if (launchAfterInstall)
            {
                var pythonw = Path.Combine(installPath, ".venv", "Scripts", "pythonw.exe");
                if (!File.Exists(pythonw))
                {
                    throw new FileNotFoundException(
                        "El entorno de Python no contiene pythonw.exe; no se puede iniciar la app.",
                        pythonw);
                }

                progress.Report("Abriendo Discord Rich Presence...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = pythonw,
                    Arguments = Quote(Path.Combine(installPath, "app.py")),
                    WorkingDirectory = installPath,
                    UseShellExecute = false
                });
            }
        }

        private static bool SameRepository(string remoteUrl)
        {
            return string.Equals(
                NormalizeRepositoryUrl(remoteUrl),
                NormalizeRepositoryUrl(RepositoryUrl),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRepositoryUrl(string value)
        {
            var normalized = value.Trim().TrimEnd('/');
            if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            return normalized;
        }

        private static async Task<string> FindSupportedPythonAsync(
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            foreach (var candidate in GetPythonCandidates())
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                string output;
                try
                {
                    output = await CaptureProcessOutputAsync(
                        candidate,
                        "-c \"import sys; print('%d.%d' % sys.version_info[:2])\"",
                        cancellationToken);
                }
                catch (Win32Exception)
                {
                    continue;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                Version version;
                if (Version.TryParse(output.Trim(), out version) && version >= new Version(3, 11))
                {
                    progress.Report("Python disponible: " + candidate + " (" + version + ")");
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetPythonCandidates()
        {
            var candidates = new List<string>();
            var localPrograms = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Python");

            foreach (var version in new[] { "Python313", "Python312", "Python311" })
            {
                candidates.Add(Path.Combine(localPrograms, version, "python.exe"));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), version, "python.exe"));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), version, "python.exe"));
            }

            var onPath = FindExecutable("python.exe");
            if (onPath != null && onPath.IndexOf(
                    Path.Combine("Microsoft", "WindowsApps"),
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                candidates.Add(onPath);
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string FindExecutable(string executableName)
        {
            var path = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
            foreach (var entry in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                var candidate = Path.Combine(entry.Trim().Trim('"'), executableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsWebView2Installed()
        {
            var roots = new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "EdgeWebView",
                    "Application"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft",
                    "EdgeWebView",
                    "Application"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft",
                    "EdgeWebView",
                    "Application")
            };

            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (var versionFolder in Directory.GetDirectories(root))
                {
                    if (File.Exists(Path.Combine(versionFolder, "msedgewebview2.exe")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void RefreshProcessPath()
        {
            var userPath = Registry.GetValue(
                @"HKEY_CURRENT_USER\Environment", "Path", string.Empty) as string;
            var machinePath = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                "Path",
                string.Empty) as string;
            var combined = string.Join(
                Path.PathSeparator.ToString(),
                new[] { userPath, machinePath }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(Environment.ExpandEnvironmentVariables));
            Environment.SetEnvironmentVariable("Path", combined, EnvironmentVariableTarget.Process);
        }

        private static async Task RunProcessAsync(
            string fileName,
            string arguments,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        progress.Report(args.Data);
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        progress.Report(args.Data);
                    }
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException("No se pudo iniciar el proceso: " + fileName);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (cancellationToken.Register(delegate
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (Win32Exception exception)
                    {
                        progress.Report("No se pudo detener el proceso actual: " + exception.Message);
                    }
                }))
                {
                    await Task.Run(delegate { process.WaitForExit(); });
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        Path.GetFileName(fileName) + " terminó con código " + process.ExitCode +
                        ". Revisa el registro para ver el detalle.");
                }
            }
        }

        private static async Task<string> CaptureProcessOutputAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("No se pudo iniciar " + fileName);
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                using (cancellationToken.Register(delegate
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }))
                {
                    await Task.Run(delegate { process.WaitForExit(); });
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var output = await outputTask;
                var error = await errorTask;
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "No se pudo comprobar Python: " + error.Trim());
                }

                return output;
            }
        }

        private static void CreateShortcuts(string installPath, bool createDesktopShortcut)
        {
            var pythonw = Path.Combine(installPath, ".venv", "Scripts", "pythonw.exe");
            var script = Path.Combine(installPath, "app.py");
            var icon = Path.Combine(installPath, "ui", "icono.ico");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException(
                    "Windows Script Host no está disponible; no se pudieron crear los accesos directos.");
            }

            var shell = Activator.CreateInstance(shellType);
            try
            {
                var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                var shortcutsFolder = Path.Combine(startMenu, "Programs", "EstadoDiscord");
                Directory.CreateDirectory(shortcutsFolder);
                CreateShortcut(
                    shell,
                    Path.Combine(shortcutsFolder, "Discord Rich Presence.lnk"),
                    pythonw,
                    Quote(script),
                    installPath,
                    icon);

                if (createDesktopShortcut)
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(
                        shell,
                        Path.Combine(desktop, "Discord Rich Presence.lnk"),
                        pythonw,
                        Quote(script),
                        installPath,
                        icon);
                }
            }
            finally
            {
                if (Marshal.IsComObject(shell))
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
        }

        private static void CreateShortcut(
            object shell,
            string shortcutPath,
            string targetPath,
            string arguments,
            string workingDirectory,
            string iconPath)
        {
            dynamic shortcut = shell.GetType().InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Description = "Discord Rich Presence";
            if (File.Exists(iconPath))
            {
                shortcut.IconLocation = iconPath;
            }

            shortcut.Save();
            if (Marshal.IsComObject(shortcut))
            {
                Marshal.ReleaseComObject(shortcut);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
