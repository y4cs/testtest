using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace SkidLeaks
{
    public class ScriptItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Extension { get; set; }
    }


    public partial class FileNameInputDialog : Window
    {
        public string FileName { get; private set; }
        private TextBox fileNameTextBox;

        public FileNameInputDialog()
        {
            InitializeComponent();
        }

        #region SavePopup XAML Code

        private void InitializeComponent()
        {
            this.Title = "Save Script";
            this.Width = 350;
            this.Height = 140;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ResizeMode = ResizeMode.NoResize;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;

            var mainBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF151324")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF12101a")),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(10, 10, 10, 10)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(15, 15, 15, 15);

            var textBlock = new TextBlock
            {
                Text = "Enter filename:",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7e7d81")),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(textBlock, 0);

            fileNameTextBox = new TextBox
            {
                Height = 28,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF151320")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7e7d81")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF12101a")),
                BorderThickness = new Thickness(1, 1, 1, 1),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                CaretBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A7BC2"))
            };

            var textBoxBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Child = fileNameTextBox
            };

            Grid.SetRow(textBoxBorder, 1);

            fileNameTextBox.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    SaveFile();
                }
                else if (e.Key == Key.Escape)
                {
                    this.DialogResult = false;
                    this.Close();
                }
            };

            grid.Children.Add(textBlock);
            grid.Children.Add(textBoxBorder);

            mainBorder.Child = grid;
            this.Content = mainBorder;

            this.Loaded += (sender, e) => fileNameTextBox.Focus();

            mainBorder.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    this.DragMove();
                }
            };

            #endregion
        }


        private void SaveFile()
        {
            if (fileNameTextBox != null)
            {
                string fileName = fileNameTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(fileName))
                {
                    if (!fileName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) &&
                        !fileName.EndsWith(".luau", StringComparison.OrdinalIgnoreCase) &&
                        !fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName += ".lua";
                    }

                    foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                    {
                        fileName = fileName.Replace(c, '_');
                    }

                    this.FileName = fileName;
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    var originalBrush = ((Border)this.Content).BorderBrush;
                    ((Border)this.Content).BorderBrush = Brushes.Red;

                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    timer.Tick += (s, e) =>
                    {
                        ((Border)this.Content).BorderBrush = originalBrush;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }
    }


    public partial class MainWindow : Window
    {
        private ObservableCollection<ScriptItem> _scriptItems;
        private string _scriptsDirectory;


        public MainWindow()
        {
            InitializeComponent();
            InitializeScriptWorkspace();
            InitializeAsync();

            var cursorStream = Application.GetResourceStream(
            new Uri("pack://application:,,,/Resources/circle.cur")).Stream;
            var myCursor = new Cursor(cursorStream);
            Mouse.OverrideCursor = myCursor;
        }

       

        #region Button Handlers

        private void Close(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void Minimise(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        #endregion

        #region Workspace Initialization and Management

        private void InitializeScriptWorkspace()
        {
            _scriptItems = new ObservableCollection<ScriptItem>();
            ScriptWorkspace.ItemsSource = _scriptItems;
            _scriptsDirectory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Scripts");
            if (!Directory.Exists(_scriptsDirectory))
            {
                Directory.CreateDirectory(_scriptsDirectory);
            }
            LoadScriptsFromDirectory();
            SetupFileSystemWatcher();
        }

        private void LoadScriptsFromDirectory()
        {
            _scriptItems.Clear();

            if (!Directory.Exists(_scriptsDirectory))
                return;

            try
            {
                string[] extensions = { "*.lua", "*.luau", "*.txt" };
                var scriptFiles = new List<string>();

                foreach (string extension in extensions)
                {
                    scriptFiles.AddRange(Directory.GetFiles(_scriptsDirectory, extension, SearchOption.AllDirectories));
                }

                scriptFiles.Sort();

                foreach (string filePath in scriptFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    var scriptItem = new ScriptItem
                    {
                        Name = fileInfo.Name,
                        FullPath = filePath,
                        Extension = fileInfo.Extension.ToLower()
                    };

                    _scriptItems.Add(scriptItem);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading scripts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupFileSystemWatcher()
        {
            try
            {
                var watcher = new FileSystemWatcher(_scriptsDirectory);
                watcher.Filter = "*.*";
                watcher.IncludeSubdirectories = true;
                watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;

                watcher.Created += OnScriptFileChanged;
                watcher.Deleted += OnScriptFileChanged;
                watcher.Renamed += OnScriptFileChanged;

                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
            }
        }

        private void OnScriptFileChanged(object sender, FileSystemEventArgs e)
        {
            string extension = System.IO.Path.GetExtension(e.Name).ToLower();
            if (extension == ".lua" || extension == ".luau" || extension == ".txt")
            {
                Dispatcher.Invoke(() => LoadScriptsFromDirectory());
            }
        }

        private async void ScriptWorkspace_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScriptWorkspace.SelectedItem is ScriptItem selectedScript)
            {
                try
                {
                    string scriptContent = await File.ReadAllTextAsync(selectedScript.FullPath);

                    string escapedScript = scriptContent
                        .Replace("\\", "\\\\")
                        .Replace("`", "\\`")
                        .Replace("${", "\\${");
                    await Editor.CoreWebView2.ExecuteScriptAsync($"editor.setValue(`{escapedScript}`);");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ListBoxItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is ScriptItem script)
            {
                try
                {
                    if (File.Exists(script.FullPath))
                    {
                        File.Delete(script.FullPath);
                    }

                    _scriptItems.Remove(script);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error deleting script: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion


        #region Editor Initialization

        private async void InitializeAsync()
        {
            try
            {
                await Editor.EnsureCoreWebView2Async(null);
                Editor.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0, 0, 0, 0);
                Editor.CoreWebView2.Navigate("https://skid-leaks-monaco.vercel.app/");
                Editor.ZoomFactor = 0.9;
            }
            catch (Exception)
            {

            }
        }

        #endregion

        private void ExecuteClicked(object sender, MouseButtonEventArgs e)
        {
            // Execute Function Here
        }

        private void AttachClicked(object sender, MouseButtonEventArgs e)
        {
            // Attach Function Here
        }

        private void ClearClicked(object sender, MouseButtonEventArgs e)
        {
            Editor.ExecuteScriptAsync("editor.setValue(' ');");
        }

        private async void SaveClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string rawResult = await Editor.ExecuteScriptAsync("editor.getValue()");
                string editorContent = JsonSerializer.Deserialize<string>(rawResult);

                if (string.IsNullOrWhiteSpace(editorContent))
                {
                    MessageBox.Show("Editor is empty. Nothing to save.", "Empty Editor",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new FileNameInputDialog();
                dialog.Owner = this;

                if (dialog.ShowDialog() == true)
                {
                    string fileName = dialog.FileName;
                    string fullPath = System.IO.Path.Combine(_scriptsDirectory, fileName);

                    if (File.Exists(fullPath))
                    {
                        var result = MessageBox.Show(
                            $"A file named '{fileName}' already exists. Do you want to overwrite it?",
                            "File Exists",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }

                    await File.WriteAllTextAsync(fullPath, editorContent);
                    LoadScriptsFromDirectory();
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void OpenClicked(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                InitialDirectory = _scriptsDirectory,
                Filter = "Script files (*.txt;*.lua)|*.txt;*.lua|All files (*.*)|*.*",
                Title = "Open Script File"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string fileContent = File.ReadAllText(dlg.FileName);

                    string jsSafeContent = fileContent
                        .Replace("\\", "\\\\")
                        .Replace("'", "\\'")
                        .Replace("\r", "")
                        .Replace("\n", "\\n");

                    Editor.ExecuteScriptAsync($"editor.setValue('{jsSafeContent}');");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error opening file: {ex.Message}");
                }
            }
        }
    }
}