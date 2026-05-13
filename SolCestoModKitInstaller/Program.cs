using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace SolCestoModKitInstaller;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 3 && string.Equals(args[0], "--patch", StringComparison.OrdinalIgnoreCase))
        {
            var moneyValue = args.Length >= 4 && int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
                ? parsedValue
                : 999;

            ModKitInstaller.CreateModdedBuild(
                new PatchOptions(
                    GameFolder: args[1],
                    OutputFolder: args[2],
                    MoneyValue: moneyValue,
                    IncludeMoneyMod: true,
                    MoneyLockStartsOn: false),
                _ => { });
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox _gameFolderText = new();
    private readonly TextBox _outputFolderText = new();
    private readonly NumericUpDown _moneyValue = new();
    private readonly CheckBox _includeMoneyMod = new();
    private readonly CheckBox _moneyLockStartsOn = new();
    private readonly Button _patchButton = new();
    private readonly Button _browseGameButton = new();
    private readonly Button _browseOutputButton = new();
    private readonly CheckBox _openFolderWhenDone = new();
    private readonly CheckBox _launchWhenDone = new();
    private readonly TextBox _log = new();

    public MainForm()
    {
        Text = "Sol Cesto ModKit Installer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 520);
        Size = new Size(880, 570);
        Font = new Font("Segoe UI", 10F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Text = "Pick the Sol Cesto game folder. The installer creates a separate modded copy next to it.",
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };
        root.Controls.Add(title, 0, 0);

        root.Controls.Add(CreateFolderRow("Game folder", _gameFolderText, _browseGameButton), 0, 1);
        root.Controls.Add(CreateFolderRow("Output folder", _outputFolderText, _browseOutputButton), 0, 2);
        root.Controls.Add(CreateOptionsRow(), 0, 3);

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Dock = DockStyle.Fill;
        _log.Font = new Font("Consolas", 9F);
        root.Controls.Add(_log, 0, 4);

        _browseGameButton.Click += (_, _) => BrowseForGameFolder();
        _browseOutputButton.Click += (_, _) => BrowseForOutputFolder();
        _patchButton.Click += async (_, _) => await PatchAsync();
        _gameFolderText.TextChanged += (_, _) => AutoFillOutputFolder();

        _gameFolderText.PlaceholderText = @"Select the folder containing SolCesto.exe and package.nw";
        _outputFolderText.PlaceholderText = @"Auto-fills after selecting the game folder";
        AutoFillOutputFolder();

        AppendLog("Installs a lightweight BepInEx-style JavaScript mod loader for Sol Cesto.");
        AppendLog("Generated mods live in the output folder's mods directory.");
        AppendLog("Default money mod hotkeys: F8 sets money once, F9 toggles the money lock.");
    }

    private Control CreateFolderRow(string labelText, TextBox textBox, Button button)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };

        textBox.Dock = DockStyle.Fill;
        textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;

        button.Text = "Browse...";
        button.Dock = DockStyle.Fill;

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(textBox, 1, 0);
        panel.Controls.Add(button, 2, 0);
        return panel;
    }

    private Control CreateOptionsRow()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 12),
            WrapContents = false
        };

        panel.Controls.Add(new Label
        {
            Text = "Money mod value",
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 0)
        });

        _moneyValue.Minimum = 1;
        _moneyValue.Maximum = 999999;
        _moneyValue.Value = 999;
        _moneyValue.Width = 100;
        panel.Controls.Add(_moneyValue);

        _includeMoneyMod.Text = "Include money mod";
        _includeMoneyMod.Checked = true;
        _includeMoneyMod.AutoSize = true;
        _includeMoneyMod.Margin = new Padding(18, 6, 0, 0);
        panel.Controls.Add(_includeMoneyMod);

        _moneyLockStartsOn.Text = "Lock starts on";
        _moneyLockStartsOn.Checked = false;
        _moneyLockStartsOn.AutoSize = true;
        _moneyLockStartsOn.Margin = new Padding(18, 6, 0, 0);
        panel.Controls.Add(_moneyLockStartsOn);

        _openFolderWhenDone.Text = "Open modded folder";
        _openFolderWhenDone.Checked = true;
        _openFolderWhenDone.AutoSize = true;
        _openFolderWhenDone.Margin = new Padding(18, 6, 0, 0);
        panel.Controls.Add(_openFolderWhenDone);

        _launchWhenDone.Text = "Launch modded game";
        _launchWhenDone.Checked = false;
        _launchWhenDone.AutoSize = true;
        _launchWhenDone.Margin = new Padding(18, 6, 0, 0);
        panel.Controls.Add(_launchWhenDone);

        _patchButton.Text = "Create modded build";
        _patchButton.AutoSize = true;
        _patchButton.Margin = new Padding(28, 0, 0, 0);
        panel.Controls.Add(_patchButton);

        return panel;
    }

    private void BrowseForGameFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the Sol Cesto folder containing SolCesto.exe and package.nw",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_gameFolderText.Text) ? _gameFolderText.Text : NeutralStartFolder()
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _gameFolderText.Text = dialog.SelectedPath;
        }
    }

    private void BrowseForOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select or create the output folder for the modded copy",
            UseDescriptionForTitle = true,
            SelectedPath = OutputBrowserStartFolder()
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputFolderText.Text = dialog.SelectedPath;
        }
    }

    private void AutoFillOutputFolder()
    {
        try
        {
            if (!Directory.Exists(_gameFolderText.Text))
            {
                return;
            }

            var source = new DirectoryInfo(_gameFolderText.Text);
            var parent = source.Parent?.FullName;
            if (parent is null)
            {
                return;
            }

            _outputFolderText.Text = Path.Combine(parent, source.Name + " modded");
        }
        catch
        {
        }
    }

    private string OutputBrowserStartFolder()
    {
        if (Directory.Exists(_outputFolderText.Text))
        {
            return _outputFolderText.Text;
        }

        if (Directory.Exists(_gameFolderText.Text))
        {
            var parent = new DirectoryInfo(_gameFolderText.Text).Parent?.FullName;
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                return parent;
            }
        }

        return NeutralStartFolder();
    }

    private static string NeutralStartFolder()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory);
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            return root;
        }

        return Directory.Exists(@"C:\") ? @"C:\" : ".";
    }

    private async Task PatchAsync()
    {
        _patchButton.Enabled = false;
        try
        {
            var options = new PatchOptions(
                GameFolder: _gameFolderText.Text.Trim(),
                OutputFolder: _outputFolderText.Text.Trim(),
                MoneyValue: (int)_moneyValue.Value,
                IncludeMoneyMod: _includeMoneyMod.Checked,
                MoneyLockStartsOn: _moneyLockStartsOn.Checked);

            if (Directory.Exists(options.OutputFolder))
            {
                var answer = MessageBox.Show(
                    this,
                    "The output folder already exists and will be replaced:\r\n\r\n" + options.OutputFolder,
                    "Replace modded folder?",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);

                if (answer != DialogResult.OK)
                {
                    AppendLog("Canceled.");
                    return;
                }
            }

            _log.Clear();
            await Task.Run(() => ModKitInstaller.CreateModdedBuild(options, AppendLog));

            AppendLog("Done.");

            if (_openFolderWhenDone.Checked)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = options.OutputFolder,
                    UseShellExecute = true
                });
            }

            if (_launchWhenDone.Checked)
            {
                var exePath = Path.Combine(options.OutputFolder, "SolCesto.exe");
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = options.OutputFolder,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _patchButton.Enabled = true;
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }

        _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine);
    }
}

internal sealed record PatchOptions(
    string GameFolder,
    string OutputFolder,
    int MoneyValue,
    bool IncludeMoneyMod,
    bool MoneyLockStartsOn);

internal static class ModKitInstaller
{
    private const string MoneyMarkerStart = "/* BEGIN SolCesto money helper */";
    private const string MoneyMarkerEnd = "/* END SolCesto money helper */";
    private const string OldMarkerStart = "/* SolCesto gold helper injected by Codex */";
    private const string OldMarkerEnd = "console.log(\"[SolCesto gold helper] loaded. F8 sets gold to 999. F9 toggles lock.\");\r\n}());";

    public static void CreateModdedBuild(PatchOptions options, Action<string> log)
    {
        var sourceDir = FullPath(options.GameFolder);
        var outputDir = FullPath(options.OutputFolder);
        var sourceExe = Path.Combine(sourceDir, "SolCesto.exe");
        var sourcePackage = Path.Combine(sourceDir, "package.nw");
        var outputPackage = Path.Combine(outputDir, "package.nw");

        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException("Game folder not found: " + sourceDir);
        }

        if (!File.Exists(sourceExe))
        {
            throw new FileNotFoundException("Expected SolCesto.exe in the selected folder.");
        }

        if (!File.Exists(sourcePackage) && !Directory.Exists(sourcePackage))
        {
            throw new FileNotFoundException("Expected package.nw in the selected folder.");
        }

        if (PathsEqual(sourceDir, outputDir))
        {
            throw new InvalidOperationException("The output folder cannot be the same as the source folder.");
        }

        if (IsInsideDirectory(outputDir, sourceDir))
        {
            throw new InvalidOperationException("The output folder cannot be inside the source game folder.");
        }

        log("Source: " + sourceDir);
        log("Output: " + outputDir);

        if (Directory.Exists(outputDir))
        {
            log("Removing existing output folder...");
            Directory.Delete(outputDir, recursive: true);
        }

        Directory.CreateDirectory(outputDir);

        log("Copying game files...");
        CopyDirectory(sourceDir, outputDir, entryNameToSkip: "package.nw");

        log("Preparing extracted package.nw folder...");
        if (Directory.Exists(sourcePackage))
        {
            CopyDirectory(sourcePackage, outputPackage);
        }
        else
        {
            Directory.CreateDirectory(outputPackage);
            ZipFile.ExtractToDirectory(sourcePackage, outputPackage);
        }

        var mainJsPath = Path.Combine(outputPackage, "scripts", "main.js");
        if (!File.Exists(mainJsPath))
        {
            throw new FileNotFoundException("Could not find scripts\\main.js inside package.nw.");
        }

        log("Injecting Sol Cesto ModKit loader...");
        var mainText = File.ReadAllText(mainJsPath, Encoding.UTF8);
        mainText = RemoveExistingHelperBlocks(mainText);
        mainText = mainText.TrimEnd() + Environment.NewLine + Environment.NewLine + FrameworkTemplates.LoaderScript() + Environment.NewLine;
        File.WriteAllText(mainJsPath, mainText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        CreateModsFolder(outputDir, options, log);

        log("Modded executable:");
        log(Path.Combine(outputDir, "SolCesto.exe"));
        log("Mods folder:");
        log(Path.Combine(outputDir, "mods"));
        if (options.IncludeMoneyMod)
        {
            log("Money mod hotkeys: F8 sets money once, F9 toggles lock.");
        }
    }

    private static string RemoveExistingHelperBlocks(string text)
    {
        text = RemoveDelimitedBlock(text, FrameworkTemplates.LoaderMarkerStart, FrameworkTemplates.LoaderMarkerEnd);
        text = RemoveDelimitedBlock(text, MoneyMarkerStart, MoneyMarkerEnd);

        var oldStart = text.IndexOf(OldMarkerStart, StringComparison.Ordinal);
        if (oldStart >= 0)
        {
            var oldEnd = text.IndexOf(OldMarkerEnd, oldStart, StringComparison.Ordinal);
            if (oldEnd >= 0)
            {
                text = text.Remove(oldStart, oldEnd + OldMarkerEnd.Length - oldStart);
            }
        }

        return text;
    }

    private static void CreateModsFolder(string outputDir, PatchOptions options, Action<string> log)
    {
        var modsDir = Path.Combine(outputDir, "mods");
        var examplesDir = Path.Combine(modsDir, "examples");

        log("Writing mods folder...");
        Directory.CreateDirectory(modsDir);
        Directory.CreateDirectory(examplesDir);

        FrameworkTemplates.WriteUtf8NoBom(Path.Combine(modsDir, "mods.json"), FrameworkTemplates.Manifest(options.IncludeMoneyMod));
        FrameworkTemplates.WriteUtf8NoBom(Path.Combine(modsDir, "README.md"), FrameworkTemplates.ModsReadme());
        FrameworkTemplates.WriteUtf8NoBom(Path.Combine(examplesDir, "example-overlay.js"), FrameworkTemplates.ExampleOverlayMod());

        if (options.IncludeMoneyMod)
        {
            FrameworkTemplates.WriteUtf8NoBom(
                Path.Combine(modsDir, "money-lock.js"),
                FrameworkTemplates.MoneyMod(options.MoneyValue, options.MoneyLockStartsOn));
        }
    }

    private static string RemoveDelimitedBlock(string text, string start, string end)
    {
        while (true)
        {
            var startIndex = text.IndexOf(start, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return text;
            }

            var endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                return text;
            }

            text = text.Remove(startIndex, endIndex + end.Length - startIndex);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, string? entryNameToSkip = null)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(directory);
            if (entryNameToSkip is not null && string.Equals(name, entryNameToSkip, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destinationDir, name));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (entryNameToSkip is not null && string.Equals(name, entryNameToSkip, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destinationDir, name), overwrite: true);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(FullPath(left).TrimEnd(Path.DirectorySeparatorChar), FullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInsideDirectory(string candidatePath, string parentPath)
    {
        var candidate = FullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parent = FullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static string FullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A required folder path is empty.");
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
    }
}
