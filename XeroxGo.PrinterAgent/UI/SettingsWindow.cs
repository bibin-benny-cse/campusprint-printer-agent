using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using XeroxGo.PrinterAgent.Models;
using XeroxGo.PrinterAgent.Services;

using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using Panel = System.Windows.Controls.Panel;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using WpfHAlign = System.Windows.HorizontalAlignment;
using WpfVAlign = System.Windows.VerticalAlignment;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace XeroxGo.PrinterAgent.UI
{
    public class SettingsWindow : Window
    {
        private readonly AppConfig _config;
        private readonly Action<AppConfig> _onSaveCallback;

        private TextBox _txtApiUrl = null!;
        private PasswordBox _txtApiKey = null!;
        private TextBox _txtApiKeyRevealed = null!;
        private Button _btnToggleKeyVisibility = null!;
        private bool _isKeyRevealed = false;

        private ComboBox _cmbLogicalSlot = null!;
        private ComboBox _cmbPhysicalPrinters = null!;
        private Slider _sliderPoll = null!;
        private TextBlock _lblPollValue = null!;

        private Border _togglePill = null!;
        private Ellipse _toggleThumb = null!;
        private TextBlock _lblToggleStatus = null!;
        private bool _isAutoStartEnabled = false;

        private Border _statusBadge = null!;
        private Ellipse _statusDot = null!;
        private TextBlock _statusText = null!;

        public SettingsWindow(AppConfig config, Action<AppConfig> onSaveCallback, string currentStatus = "Connected to Cloud", string statusState = "idle")
        {
            _config = config;
            _onSaveCallback = onSaveCallback;

            InitializeWindow();
            BuildLayout(currentStatus, statusState);
            LoadConfiguration();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            DwmApi.ApplyWindows11Styling(handle);
        }

        private void InitializeWindow()
        {
            Title = "XeroxGo — Agent Settings";
            Width = 620;
            Height = 740;
            MinWidth = 540;
            MinHeight = 580;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)); // Slate-50
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI, sans-serif");
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        }

        private void BuildLayout(string currentStatus, string statusState)
        {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Scrollable Body
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Footer
            Content = rootGrid;

            // ==========================================
            // Row 0: Modern Header
            // ==========================================
            var headerGrid = new Grid { Margin = new Thickness(28, 24, 28, 16) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // App Icon
            var logoBorder = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(10),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0, 103, 192),
                    Color.FromRgb(37, 99, 235),
                    90
                ),
                Margin = new Thickness(0, 0, 14, 0)
            };
            var logoCanvas = new Canvas { Width = 26, Height = 26, HorizontalAlignment = WpfHAlign.Center, VerticalAlignment = WpfVAlign.Center };
            var printerBody = new System.Windows.Shapes.Rectangle
            {
                Width = 22,
                Height = 12,
                RadiusX = 2,
                RadiusY = 2,
                Fill = Brushes.White,
                Margin = new Thickness(2, 8, 0, 0)
            };
            var paperTop = new System.Windows.Shapes.Rectangle
            {
                Width = 14,
                Height = 6,
                RadiusX = 1,
                RadiusY = 1,
                Fill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Margin = new Thickness(6, 2, 0, 0)
            };
            var paperSlot = new System.Windows.Shapes.Rectangle
            {
                Width = 10,
                Height = 2,
                Fill = new SolidColorBrush(Color.FromRgb(0, 103, 192)),
                Margin = new Thickness(8, 14, 0, 0)
            };
            logoCanvas.Children.Add(paperTop);
            logoCanvas.Children.Add(printerBody);
            logoCanvas.Children.Add(paperSlot);
            logoBorder.Child = logoCanvas;
            Grid.SetColumn(logoBorder, 0);
            headerGrid.Children.Add(logoBorder);

            // Title & Subtitle
            var titlePanel = new StackPanel { VerticalAlignment = WpfVAlign.Center };
            var txtTitle = new TextBlock
            {
                Text = "XeroxGo Agent",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };
            var txtSubtitle = new TextBlock
            {
                Text = "High-reliability counter printer daemon for Windows",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            titlePanel.Children.Add(txtTitle);
            titlePanel.Children.Add(txtSubtitle);
            Grid.SetColumn(titlePanel, 1);
            headerGrid.Children.Add(titlePanel);

            // Status Badge
            _statusBadge = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 6, 12, 6),
                VerticalAlignment = WpfVAlign.Center
            };
            var badgePanel = new StackPanel { Orientation = Orientation.Horizontal };
            _statusDot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = WpfVAlign.Center, Margin = new Thickness(0, 0, 8, 0) };
            _statusText = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = WpfVAlign.Center };
            badgePanel.Children.Add(_statusDot);
            badgePanel.Children.Add(_statusText);
            _statusBadge.Child = badgePanel;
            UpdateStatusPill(currentStatus, statusState);
            Grid.SetColumn(_statusBadge, 2);
            headerGrid.Children.Add(_statusBadge);

            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // ==========================================
            // Row 1: Scrollable Card Surfaces
            // ==========================================
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(28, 0, 28, 12)
            };

            var cardsPanel = new StackPanel();

            // Card 1: Cloud Backend
            cardsPanel.Children.Add(CreateCloudBackendCard());

            // Card 2: Physical Hardware & Slot
            cardsPanel.Children.Add(CreateHardwareCard());

            // Card 3: Automation & Dispatch
            cardsPanel.Children.Add(CreateAutomationCard());

            scrollViewer.Content = cardsPanel;
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

            // ==========================================
            // Row 2: Bottom Action Bar
            // ==========================================
            var footerBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Background = Brushes.White,
                Padding = new Thickness(28, 14, 28, 18)
            };
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Logs Button
            var btnLogs = CreateSecondaryButton("📄  View Activity Logs");
            btnLogs.Click += (s, e) =>
            {
                string path = Logger.GetLogFilePath();
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("No log file found yet.", "XeroxGo Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };
            Grid.SetColumn(btnLogs, 0);
            footerGrid.Children.Add(btnLogs);

            // Actions (Cancel + Save)
            var actionButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = WpfHAlign.Right };

            var btnCancel = CreateSecondaryButton("Cancel");
            btnCancel.Width = 90;
            btnCancel.Margin = new Thickness(0, 0, 12, 0);
            btnCancel.Click += (s, e) => Close();
            actionButtons.Children.Add(btnCancel);

            var btnSave = new Button
            {
                Content = "Save & Connect",
                Width = 140,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(0, 103, 192)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Cursor = Cursors.Hand
            };
            btnSave.Template = CreateRoundedButtonTemplate(
                new CornerRadius(6),
                new SolidColorBrush(Color.FromRgb(0, 103, 192)),
                new SolidColorBrush(Color.FromRgb(24, 119, 211)),
                new SolidColorBrush(Color.FromRgb(0, 90, 168)),
                Brushes.White
            );
            btnSave.Click += OnSaveClicked;
            actionButtons.Children.Add(btnSave);

            Grid.SetColumn(actionButtons, 2);
            footerGrid.Children.Add(actionButtons);

            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 2);
            rootGrid.Children.Add(footerBorder);
        }

        #region Card Builders
        private Border CreateCloudBackendCard()
        {
            var card = CreateBaseCard();
            var content = new StackPanel();

            content.Children.Add(CreateCardHeader("☁️  Cloud Backend Connection", "Configure pairing with your XeroxGo cloud server."));

            // 1. API URL
            content.Children.Add(CreateFieldLabel("Server API Endpoint"));
            var urlBorder = CreateInputContainer();
            _txtApiUrl = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                VerticalAlignment = WpfVAlign.Center
            };
            urlBorder.Child = _txtApiUrl;
            content.Children.Add(urlBorder);

            // 2. API Key
            content.Children.Add(CreateFieldLabel("Shop Agent Security Key (Bearer Token)"));
            var keyBorder = CreateInputContainer();
            var keyGrid = new Grid();
            keyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            keyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtApiKey = new PasswordBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                VerticalAlignment = WpfVAlign.Center
            };
            Grid.SetColumn(_txtApiKey, 0);
            keyGrid.Children.Add(_txtApiKey);

            _txtApiKeyRevealed = new TextBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                VerticalAlignment = WpfVAlign.Center,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(_txtApiKeyRevealed, 0);
            keyGrid.Children.Add(_txtApiKeyRevealed);

            _btnToggleKeyVisibility = new Button
            {
                Content = "👁",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = Cursors.Hand,
                Padding = new Thickness(6, 0, 4, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            _btnToggleKeyVisibility.Click += (s, e) =>
            {
                _isKeyRevealed = !_isKeyRevealed;
                if (_isKeyRevealed)
                {
                    _txtApiKeyRevealed.Text = _txtApiKey.Password;
                    _txtApiKey.Visibility = Visibility.Collapsed;
                    _txtApiKeyRevealed.Visibility = Visibility.Visible;
                }
                else
                {
                    _txtApiKey.Password = _txtApiKeyRevealed.Text;
                    _txtApiKeyRevealed.Visibility = Visibility.Collapsed;
                    _txtApiKey.Visibility = Visibility.Visible;
                }
            };
            Grid.SetColumn(_btnToggleKeyVisibility, 1);
            keyGrid.Children.Add(_btnToggleKeyVisibility);

            keyBorder.Child = keyGrid;
            content.Children.Add(keyBorder);

            card.Child = content;
            return card;
        }

        private Border CreateHardwareCard()
        {
            var card = CreateBaseCard();
            var content = new StackPanel();

            content.Children.Add(CreateCardHeader("🖨️  Physical Hardware & Counter Mapping", "Map this computer to a dashboard slot and Windows printer driver."));

            // Logical Slot
            content.Children.Add(CreateFieldLabel("Logical Slot in XeroxGo Dashboard"));
            _cmbLogicalSlot = new ComboBox
            {
                FontSize = 13,
                Height = 38,
                Margin = new Thickness(0, 0, 0, 14),
                IsEditable = true
            };
            _cmbLogicalSlot.Items.Add("Printer 1");
            _cmbLogicalSlot.Items.Add("Printer 2");
            _cmbLogicalSlot.Items.Add("Printer 3");
            _cmbLogicalSlot.Items.Add("Printer 4");
            _cmbLogicalSlot.Items.Add("Main B&W Counter");
            _cmbLogicalSlot.Items.Add("Color Printer Counter");
            content.Children.Add(_cmbLogicalSlot);

            // Physical Windows Printer
            content.Children.Add(CreateFieldLabel("Local Physical Windows Printer Driver"));
            var printerGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            printerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            printerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _cmbPhysicalPrinters = new ComboBox
            {
                FontSize = 13,
                Height = 38,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(_cmbPhysicalPrinters, 0);
            printerGrid.Children.Add(_cmbPhysicalPrinters);

            var btnRefresh = CreateSecondaryButton("🔄  Refresh");
            btnRefresh.Height = 38;
            btnRefresh.Click += (s, e) => PopulatePhysicalPrinters();
            Grid.SetColumn(btnRefresh, 1);
            printerGrid.Children.Add(btnRefresh);

            content.Children.Add(printerGrid);

            // Diagnostic Slip Button
            var btnTest = CreateSecondaryButton("🖨️  Send Diagnostic Test Slip to Selected Printer");
            btnTest.HorizontalAlignment = WpfHAlign.Left;
            btnTest.Click += OnTestPrintClicked;
            content.Children.Add(btnTest);

            card.Child = content;
            return card;
        }

        private Border CreateAutomationCard()
        {
            var card = CreateBaseCard();
            var content = new StackPanel();

            content.Children.Add(CreateCardHeader("⚡  Automation & Dispatch", "Configure job polling cadence and system startup."));

            // Polling Slider
            var pollHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            var lblPollTitle = new TextBlock
            {
                Text = "Queue Polling Interval",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
            };
            _lblPollValue = new TextBlock
            {
                Text = "Every 3 seconds",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192)),
                HorizontalAlignment = WpfHAlign.Right
            };
            pollHeaderGrid.Children.Add(lblPollTitle);
            pollHeaderGrid.Children.Add(_lblPollValue);
            content.Children.Add(pollHeaderGrid);

            _sliderPoll = new Slider
            {
                Minimum = 1,
                Maximum = 30,
                Value = 3,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 18)
            };
            _sliderPoll.ValueChanged += (s, e) =>
            {
                int val = (int)e.NewValue;
                _lblPollValue.Text = val == 1 ? "Every 1 second (Ultra-responsive)" : $"Every {val} seconds";
            };
            content.Children.Add(_sliderPoll);

            // Windows Startup Toggle Row
            var startupBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 12, 14, 12),
                Cursor = Cursors.Hand
            };
            var startupGrid = new Grid();
            startupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            startupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var startupTextPanel = new StackPanel();
            var lblStartupTitle = new TextBlock
            {
                Text = "Launch automatically on Windows startup",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            };
            var lblStartupSub = new TextBlock
            {
                Text = "Runs silently in the system tray when PC boots up",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            startupTextPanel.Children.Add(lblStartupTitle);
            startupTextPanel.Children.Add(lblStartupSub);
            Grid.SetColumn(startupTextPanel, 0);
            startupGrid.Children.Add(startupTextPanel);

            // Toggle Switch Visual
            var toggleContainer = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = WpfVAlign.Center };
            _lblToggleStatus = new TextBlock
            {
                Text = "Off",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                VerticalAlignment = WpfVAlign.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            toggleContainer.Children.Add(_lblToggleStatus);

            _togglePill = new Border
            {
                Width = 44,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2)
            };
            var toggleCanvas = new Canvas { Width = 38, Height = 16 };
            _toggleThumb = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
            Canvas.SetLeft(_toggleThumb, 2);
            Canvas.SetTop(_toggleThumb, 1);
            toggleCanvas.Children.Add(_toggleThumb);
            _togglePill.Child = toggleCanvas;
            toggleContainer.Children.Add(_togglePill);

            startupBorder.MouseDown += (s, e) => ToggleAutoStart();
            Grid.SetColumn(toggleContainer, 1);
            startupGrid.Children.Add(toggleContainer);

            startupBorder.Child = startupGrid;
            content.Children.Add(startupBorder);

            card.Child = content;
            return card;
        }

        private void ToggleAutoStart()
        {
            _isAutoStartEnabled = !_isAutoStartEnabled;
            UpdateToggleVisual();
        }

        private void UpdateToggleVisual()
        {
            if (_isAutoStartEnabled)
            {
                _togglePill.Background = new SolidColorBrush(Color.FromRgb(0, 103, 192));
                _togglePill.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 103, 192));
                _toggleThumb.Fill = Brushes.White;
                Canvas.SetLeft(_toggleThumb, 22);
                _lblToggleStatus.Text = "Active";
                _lblToggleStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 103, 192));
            }
            else
            {
                _togglePill.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                _togglePill.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                _toggleThumb.Fill = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                Canvas.SetLeft(_toggleThumb, 2);
                _lblToggleStatus.Text = "Off";
                _lblToggleStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
        }
        #endregion

        #region Helpers & Theming
        private static Border CreateBaseCard()
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 16),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(15, 23, 42),
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = 0.04
                }
            };
        }

        private static StackPanel CreateCardHeader(string title, string subtitle)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
            });
            panel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            return panel;
        }

        private static TextBlock CreateFieldLabel(string label)
        {
            return new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private static Border CreateInputContainer()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 38,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 14)
            };
        }

        private static Button CreateSecondaryButton(string text)
        {
            var btn = new Button
            {
                Content = text,
                Height = 36,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                Cursor = Cursors.Hand
            };
            btn.Template = CreateRoundedButtonTemplate(
                new CornerRadius(6),
                Brushes.White,
                new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                new SolidColorBrush(Color.FromRgb(226, 232, 240))
            );
            return btn;
        }

        private static ControlTemplate CreateRoundedButtonTemplate(
            CornerRadius cornerRadius,
            Brush normalBg,
            Brush hoverBg,
            Brush pressedBg,
            Brush textBrush,
            Brush? borderBrush = null)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "border";
            borderFactory.SetValue(Border.CornerRadiusProperty, cornerRadius);
            borderFactory.SetValue(Border.BackgroundProperty, normalBg);
            borderFactory.SetValue(Border.BorderThicknessProperty, borderBrush != null ? new Thickness(1) : new Thickness(0));
            if (borderBrush != null)
            {
                borderFactory.SetValue(Border.BorderBrushProperty, borderBrush);
            }

            var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            presenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, WpfHAlign.Center);
            presenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, WpfVAlign.Center);
            presenterFactory.SetValue(TextBlock.ForegroundProperty, textBrush);
            borderFactory.AppendChild(presenterFactory);

            template.VisualTree = borderFactory;

            // Hover Trigger
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "border"));
            template.Triggers.Add(hoverTrigger);

            // Pressed Trigger
            var pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(Border.BackgroundProperty, pressedBg, "border"));
            template.Triggers.Add(pressTrigger);

            return template;
        }

        public void UpdateStatusPill(string text, string state)
        {
            _statusText.Text = text;
            switch (state.ToLowerInvariant())
            {
                case "printing":
                    _statusBadge.Background = new SolidColorBrush(Color.FromRgb(224, 242, 254)); // Sky-100
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(3, 105, 161));   // Sky-700
                    _statusDot.Fill = new SolidColorBrush(Color.FromRgb(14, 165, 233));         // Sky-500
                    break;
                case "paused":
                    _statusBadge.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // Amber-100
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));     // Amber-700
                    _statusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));          // Amber-500
                    break;
                case "error":
                case "offline":
                    _statusBadge.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // Red-100
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28));    // Red-700
                    _statusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));           // Red-500
                    break;
                default:
                    _statusBadge.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)); // Emerald-100
                    _statusText.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61));    // Emerald-700
                    _statusDot.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));           // Emerald-500
                    break;
            }
        }
        #endregion

        #region Logic & Event Handlers
        private void LoadConfiguration()
        {
            _txtApiUrl.Text = _config.ApiUrl;
            _txtApiKey.Password = _config.AgentApiKey;
            _cmbLogicalSlot.Text = _config.LogicalPrinterName;
            _sliderPoll.Value = Math.Max(1, Math.Min(30, _config.PollIntervalSeconds));
            _isAutoStartEnabled = _config.AutoStartWithWindows;
            UpdateToggleVisual();

            PopulatePhysicalPrinters();
        }

        private void PopulatePhysicalPrinters()
        {
            _cmbPhysicalPrinters.Items.Clear();
            _cmbPhysicalPrinters.Items.Add("Auto (Auto-Detect Physical Windows Printer)");

            var installed = HardwareMonitor.GetInstalledPrinters();
            foreach (var p in installed)
            {
                string label = HardwareMonitor.IsVirtualPrinter(p) ? $"{p} [Virtual]" : p;
                _cmbPhysicalPrinters.Items.Add(label);
            }

            if (string.IsNullOrWhiteSpace(_config.PhysicalPrinterName) || _config.PhysicalPrinterName.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                _cmbPhysicalPrinters.SelectedIndex = 0;
            }
            else
            {
                int matchIndex = -1;
                for (int i = 0; i < _cmbPhysicalPrinters.Items.Count; i++)
                {
                    string item = _cmbPhysicalPrinters.Items[i]?.ToString() ?? "";
                    if (item.StartsWith(_config.PhysicalPrinterName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchIndex = i;
                        break;
                    }
                }
                _cmbPhysicalPrinters.SelectedIndex = matchIndex >= 0 ? matchIndex : 0;
            }
        }

        private void OnTestPrintClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string selected = GetSelectedPhysicalPrinterName();
                string resolved = HardwareMonitor.ResolveActivePrinter(selected);

                PrintEngine.PrintDiagnosticSlip(resolved);
                MessageBox.Show(
                    $"Diagnostic test slip sent to printer:\n\"{resolved}\"\n\nPlease inspect the printer output tray.",
                    "Test Print Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to send test print: {ex.Message}",
                    "Test Print Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            string apiUrl = _txtApiUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                MessageBox.Show("Please specify a valid backend API URL.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string logicalSlot = _cmbLogicalSlot.Text.Trim();
            if (string.IsNullOrWhiteSpace(logicalSlot))
            {
                logicalSlot = "Printer 1";
            }

            string apiKey = _isKeyRevealed ? _txtApiKeyRevealed.Text.Trim() : _txtApiKey.Password.Trim();

            _config.ApiUrl = apiUrl;
            _config.AgentApiKey = apiKey;
            _config.LogicalPrinterName = logicalSlot;
            _config.PhysicalPrinterName = GetSelectedPhysicalPrinterName();
            _config.PollIntervalSeconds = (int)_sliderPoll.Value;
            _config.AutoStartWithWindows = _isAutoStartEnabled;

            _config.Save();
            StartupManager.SetAutoStart(_config.AutoStartWithWindows);
            _onSaveCallback?.Invoke(_config);

            MessageBox.Show(
                $"Configuration saved successfully!\nSlot: {logicalSlot}\nDriver: {_config.PhysicalPrinterName}\nAPI: {apiUrl}",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            Close();
        }

        private string GetSelectedPhysicalPrinterName()
        {
            if (_cmbPhysicalPrinters.SelectedIndex <= 0) return "Auto";
            string raw = _cmbPhysicalPrinters.SelectedItem?.ToString() ?? "Auto";
            return raw.Replace(" [Virtual]", "").Trim();
        }
        #endregion
    }
}
