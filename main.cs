using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LocalImageConverter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private static readonly string[] SupportedExtensions =
        [".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff"];

    private readonly ListBox fileList = new()
    {
        Dock = DockStyle.Fill,
        HorizontalScrollbar = true,
        SelectionMode = SelectionMode.MultiExtended
    };
    private readonly ComboBox formatBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown qualityBox = new()
    {
        Minimum = 1,
        Maximum = 100,
        Value = 90,
        Width = 70
    };
    private readonly TextBox outputFolderBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button convertButton = new() { Text = "Convert images", AutoSize = true };
    private readonly Button themeButton = new() { Text = "Dark mode", AutoSize = true };
    private readonly Label qualityLabel = new() { Text = "JPEG quality:", AutoSize = true };
    private readonly ProgressBar progressBar = new() { Dock = DockStyle.Fill, Height = 23 };
    private readonly Label statusLabel = new() { Text = "Add one or more images to begin.", AutoSize = true };
    private readonly PictureBox logoBox = new()
    {
        Size = new Size(72, 54),
        SizeMode = PictureBoxSizeMode.Zoom,
        Margin = new Padding(0, 0, 10, 0)
    };
    private bool isDarkMode;
    private string? outputFolder;

    public MainForm()
    {
        Text = "Local Image Converter";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 500);
        Size = new Size(850, 600);
        AllowDrop = true;
        DragEnter += HandleDragEnter;
        DragDrop += HandleDragDrop;
        Load += (_, _) => LoadLogo();

        formatBox.Items.AddRange(["PNG", "JPEG", "BMP", "GIF", "TIFF"]);
        formatBox.SelectedIndex = 0;
        formatBox.SelectedIndexChanged += (_, _) =>
            qualityLabel.Visible = qualityBox.Visible = formatBox.SelectedItem is "JPEG";

        var addButton = new Button { Text = "Add images...", AutoSize = true };
        addButton.Click += AddImages;
        var removeButton = new Button { Text = "Remove selected", AutoSize = true };
        removeButton.Click += (_, _) =>
        {
            while (fileList.SelectedItems.Count > 0)
                fileList.Items.Remove(fileList.SelectedItems[0]!);
            UpdateControls();
        };
        var clearButton = new Button { Text = "Clear", AutoSize = true };
        clearButton.Click += (_, _) =>
        {
            fileList.Items.Clear();
            UpdateControls();
        };
        var outputButton = new Button { Text = "Choose output folder...", AutoSize = true };
        outputButton.Click += ChooseOutputFolder;
        convertButton.Click += ConvertImages;
        themeButton.Click += ToggleTheme;

        var titleLabel = new Label
        {
            Text = "Local Image Converter",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 17, 0, 0)
        };
        var header = new Panel { Dock = DockStyle.Fill, Height = 64 };
        header.Controls.Add(logoBox);
        header.Controls.Add(titleLabel);
        header.Controls.Add(themeButton);
        logoBox.Location = new Point(0, 0);
        titleLabel.Location = new Point(82, 0);
        themeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        themeButton.Location = new Point(header.Width - themeButton.Width, 17);
        header.Resize += (_, _) => themeButton.Left = header.ClientSize.Width - themeButton.Width;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        toolbar.Controls.AddRange([addButton, removeButton, clearButton]);

        var formatPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false
        };
        formatPanel.Controls.Add(new Label { Text = "Output format:", AutoSize = true, Margin = new Padding(0, 7, 6, 0) });
        formatPanel.Controls.Add(formatBox);
        formatPanel.Controls.Add(qualityLabel);
        formatPanel.Controls.Add(qualityBox);
        formatPanel.Controls.Add(outputButton);

        var outputRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        outputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputRow.Controls.Add(new Label { Text = "Output folder:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 0, 0) }, 0, 0);
        outputRow.Controls.Add(outputFolderBox, 1, 0);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, AutoSize = true };
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.Controls.Add(formatPanel, 0, 0);
        bottom.Controls.Add(outputRow, 0, 1);
        bottom.Controls.Add(progressBar, 0, 2);

        var actionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        actionRow.Controls.Add(convertButton);
        actionRow.Controls.Add(statusLabel);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            RowCount = 5,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(toolbar, 0, 1);
        root.Controls.Add(fileList, 0, 2);
        root.Controls.Add(bottom, 0, 3);
        root.Controls.Add(actionRow, 0, 4);
        Controls.Add(root);
        UpdateControls();
    }

    private void LoadLogo()
    {
        var logoPath = Path.Combine(AppContext.BaseDirectory, "logo.png");
        if (File.Exists(logoPath))
        {
            using var image = Image.FromFile(logoPath);
            logoBox.Image = new Bitmap(image);
        }
    }

    private void ToggleTheme(object? sender, EventArgs e)
    {
        isDarkMode = !isDarkMode;
        themeButton.Text = isDarkMode ? "Light mode" : "Dark mode";
        ApplyTheme(this);
    }

    private void ApplyTheme(Control control)
    {
        var background = isDarkMode ? Color.FromArgb(32, 34, 37) : SystemColors.Window;
        var foreground = isDarkMode ? Color.WhiteSmoke : SystemColors.ControlText;
        control.BackColor = background;
        control.ForeColor = foreground;

        if (control is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = isDarkMode ? Color.FromArgb(90, 94, 100) : SystemColors.ControlDark;
            button.BackColor = isDarkMode ? Color.FromArgb(55, 58, 64) : SystemColors.Control;
        }
        else if (control is TextBox textBox)
        {
            textBox.BackColor = isDarkMode ? Color.FromArgb(45, 47, 52) : SystemColors.Window;
        }
        else if (control is ListBox listBox)
        {
            listBox.BackColor = isDarkMode ? Color.FromArgb(45, 47, 52) : SystemColors.Window;
        }
        else if (control is ComboBox comboBox)
        {
            comboBox.BackColor = isDarkMode ? Color.FromArgb(45, 47, 52) : SystemColors.Window;
        }

        foreach (Control child in control.Controls)
            ApplyTheme(child);
    }

    private void AddImages(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose images",
            Multiselect = true,
            Filter = "Image files|*.bmp;*.gif;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All files|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        AddFiles(dialog.FileNames);
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void HandleDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
            AddFiles(files);
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(File.Exists)
                                  .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                                  .Where(path => !fileList.Items.Contains(path)))
            fileList.Items.Add(path);
        UpdateControls();
    }

    private void ChooseOutputFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where converted images will be saved",
            UseDescriptionForTitle = true,
            SelectedPath = outputFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            outputFolder = dialog.SelectedPath;
            outputFolderBox.Text = outputFolder;
            UpdateControls();
        }
    }

    private async void ConvertImages(object? sender, EventArgs e)
    {
        if (fileList.Items.Count == 0 || outputFolder is null)
            return;

        var files = fileList.Items.Cast<string>().ToArray();
        var format = GetSelectedFormat();
        var quality = (long)qualityBox.Value;
        convertButton.Enabled = false;
        progressBar.Value = 0;
        progressBar.Maximum = files.Length;

        var converted = 0;
        var errors = new List<string>();
        foreach (var file in files)
        {
            try
            {
                await Task.Run(() => ConvertFile(file, outputFolder, format, quality));
                converted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or ExternalException)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
            progressBar.Value++;
            statusLabel.Text = $"Converted {converted} of {files.Length}...";
        }

        convertButton.Enabled = true;
        statusLabel.Text = errors.Count == 0
            ? $"Done. Converted {converted} image(s)."
            : $"Finished with {errors.Count} error(s). Converted {converted}.";
        if (errors.Count > 0)
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Some files could not be converted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void ConvertFile(string sourcePath, string destinationFolder, ImageFormat format, long quality)
    {
        var destinationPath = Path.Combine(destinationFolder, Path.GetFileNameWithoutExtension(sourcePath) + GetExtension(format));
        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            destinationPath = Path.Combine(destinationFolder, Path.GetFileNameWithoutExtension(sourcePath) + "_converted" + GetExtension(format));
        destinationPath = GetAvailablePath(destinationPath);

        using var source = Image.FromFile(sourcePath);
        using var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(format.Guid == ImageFormat.Jpeg.Guid ? Color.White : Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        if (format.Guid == ImageFormat.Jpeg.Guid)
        {
            var encoder = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
            using var parameters = new EncoderParameters(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            bitmap.Save(destinationPath, encoder, parameters);
        }
        else
        {
            bitmap.Save(destinationPath, format);
        }
    }

    private static string GetAvailablePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; ; index++)
        {
            var candidate = Path.Combine(directory, $"{name}_{index}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private ImageFormat GetSelectedFormat() => formatBox.SelectedItem switch
    {
        "JPEG" => ImageFormat.Jpeg,
        "BMP" => ImageFormat.Bmp,
        "GIF" => ImageFormat.Gif,
        "TIFF" => ImageFormat.Tiff,
        _ => ImageFormat.Png
    };

    private static string GetExtension(ImageFormat format) => format.Guid switch
    {
        var guid when guid == ImageFormat.Jpeg.Guid => ".jpg",
        var guid when guid == ImageFormat.Bmp.Guid => ".bmp",
        var guid when guid == ImageFormat.Gif.Guid => ".gif",
        var guid when guid == ImageFormat.Tiff.Guid => ".tiff",
        _ => ".png"
    };

    private void UpdateControls()
    {
        convertButton.Enabled = fileList.Items.Count > 0 && outputFolder is not null;
        statusLabel.Text = fileList.Items.Count == 0
            ? "Add one or more images to begin."
            : outputFolder is null
                ? "Choose an output folder to enable conversion."
                : $"{fileList.Items.Count} image(s) ready.";
    }
}