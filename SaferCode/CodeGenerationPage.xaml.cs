using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using SaferCode.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Text;
using Windows.Storage.Provider;
using WinRT.Interop;
using CommunityToolkit.WinUI.UI.Controls;

namespace SaferCode.Pages
{
    public sealed partial class CodeGenerationPage : Page
    {
        private DatabaseService _databaseService;
        private ObservableCollection<CodeViewModel> _codes;
        private ObservableCollection<CodeViewModel> _lastGeneratedCodes;
        private ContentDialog? _lastGeneratedCodesDialog;

        public CodeGenerationPage()
        {
            this.InitializeComponent();
            _databaseService = new DatabaseService();
            _codes = new ObservableCollection<CodeViewModel>();
            _lastGeneratedCodes = new ObservableCollection<CodeViewModel>();

            CodesDataGrid.ItemsSource = _codes;

            Loaded += CodeGenerationPage_Loaded;
        }

        private async void CodeGenerationPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCodes();
        }

        private async Task LoadCodes(string filter = "All")
        {
            try
            {
                // וודא שהאובייקטים קיימים
                if (_codes == null)
                {
                    _codes = new ObservableCollection<CodeViewModel>();
                }

                if (CodesDataGrid != null && CodesDataGrid.ItemsSource == null)
                {
                    CodesDataGrid.ItemsSource = _codes;
                }

                _codes.Clear();

                if (_databaseService == null)
                {
                    _databaseService = new DatabaseService();
                }

                bool? isUsed = filter switch
                {
                    "Unused" => false,
                    "Used" => true,
                    _ => null
                };

                var codes = await _databaseService.GetPaymentCodes(isUsed);

                // כאן יתכן מאוד שרשימת הקודים ריקה
                if (codes == null)
                {
                    codes = new List<PaymentCode>(); // וודא שזו לא רשימה null
                }

                foreach (var code in codes)
                {
                    string username = "";
                    if (code.UsedByUserId.HasValue)
                    {
                        username = $"משתמש {code.UsedByUserId.Value}";
                    }

                    _codes.Add(new CodeViewModel
                    {
                        Code = code.Code,
                        Amount = code.Amount,
                        IsUsed = code.IsUsed,
                        CreatedDate = code.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                        UsedDate = code.UsedDate.HasValue ? code.UsedDate.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        UsedByUsername = username
                    });
                }

                // עדכן את הממשק משתמש שהטעינה הושלמה בהצלחה, אפילו אם אין קודים
                if (StatusInfoBar != null)
                {
                    StatusInfoBar.Title = "עודכן";
                    StatusInfoBar.Message = codes.Count > 0
                        ? $"נטענו {codes.Count} קודים"
                        : "אין קודים לתצוגה";
                    StatusInfoBar.Severity = InfoBarSeverity.Informational;
                    StatusInfoBar.IsOpen = true;
                }
            }
            catch (Exception ex)
            {
                // טיפול בשגיאה והצגה למשתמש
                System.Diagnostics.Debug.WriteLine($"שגיאה בטעינת קודים: {ex.Message}");

                if (StatusInfoBar != null)
                {
                    StatusInfoBar.Title = "שגיאה";
                    StatusInfoBar.Message = $"אירעה שגיאה בטעינת הקודים: {ex.Message}";
                    StatusInfoBar.Severity = InfoBarSeverity.Error;
                    StatusInfoBar.IsOpen = true;
                }
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            decimal amount = (decimal)AmountBox.Value;
            int count = (int)CountBox.Value;

            if (amount <= 0 || count <= 0)
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = "יש להזין ערכים חיוביים";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
                return;
            }

            GenerateButton.IsEnabled = false;
            StatusInfoBar.Title = "מייצר קודים";
            StatusInfoBar.Message = "אנא המתן...";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.IsOpen = true;

            try
            {
                var codes = await _databaseService.GeneratePaymentCodes(count, amount);

                StatusInfoBar.Title = "בוצע בהצלחה";
                StatusInfoBar.Message = $"יוצרו {codes.Count} קודים חדשים";
                StatusInfoBar.Severity = InfoBarSeverity.Success;

                // עדכון הרשימה הכללית
                if (FilterComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    await LoadCodes(item.Tag.ToString());
                }
                else
                {
                    await LoadCodes("All");
                }

                // עדכון רשימת הקודים האחרונים
                UpdateLastGeneratedCodes(codes);

                // הצגת הדיאלוג עם הקודים החדשים
                await ShowGeneratedCodesDialog();
            }
            catch (Exception ex)
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = $"אירעה שגיאה: {ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        // פונקציה לעדכון רשימת הקודים האחרונים
        private void UpdateLastGeneratedCodes(List<string> newCodes)
        {
            _lastGeneratedCodes.Clear();

            foreach (var code in newCodes)
            {
                // יצירת מודל עם הקוד והסכום הנוכחי שנבחר
                _lastGeneratedCodes.Add(new CodeViewModel
                {
                    Code = code,
                    Amount = (decimal)AmountBox.Value,
                    IsUsed = false,
                    CreatedDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    UsedDate = "",
                    UsedByUsername = ""
                });
            }
        }

        // פונקציה חדשה להצגת דיאלוג הקודים
        private async Task ShowGeneratedCodesDialog()
        {
            // וודא שיש קודים להצגה
            if (_lastGeneratedCodes.Count > 0)
            {
                // יצירת דיאלוג חדש
                _lastGeneratedCodesDialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot, // נדרש ב-WinUI 3
                    Title = $"נוצרו {_lastGeneratedCodes.Count} קודים חדשים",
                    PrimaryButtonText = "סגור",
                    DefaultButton = ContentDialogButton.Primary,
                    FlowDirection = FlowDirection.RightToLeft,
                    MinWidth = 800,
                    MinHeight = 600
                };

                // יצירת Grid עבור תוכן הדיאלוג
                var contentGrid = new Grid();
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                contentGrid.Margin = new Thickness(0, 0, 0, 20);

                // כותרת משנה (הסכום)
                var subtitle = new TextBlock
                {
                    Text = $"סכום כל שובר: {AmountBox.Value} ₪",
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                Grid.SetRow(subtitle, 0);
                contentGrid.Children.Add(subtitle);

                // טבלת הקודים
                var dataGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    IsReadOnly = true,
                    ItemsSource = _lastGeneratedCodes,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                // הוספת העמודות לטבלה
                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "קוד שובר",
                    Binding = new Binding { Path = new PropertyPath("Code") },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "סכום (₪)",
                    Binding = new Binding { Path = new PropertyPath("Amount") },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "תאריך יצירה",
                    Binding = new Binding { Path = new PropertyPath("CreatedDate") },
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });

                Grid.SetRow(dataGrid, 1);
                contentGrid.Children.Add(dataGrid);

                // כפתורי פעולות
                var actionsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 15, 0, 0),
                    Spacing = 15
                };

                var exportButton = new Button
                {
                    Content = "ייצוא קודים",
                    Padding = new Thickness(15, 8, 15, 8),
                    FontSize = 14
                };
                exportButton.Click += ExportLastGenerated_Click;

                var printButton = new Button
                {
                    Content = "הדפסת קודים",
                    Padding = new Thickness(15, 8, 15, 8),
                    FontSize = 14
                };
                printButton.Click += PrintLastGenerated_Click;

                actionsPanel.Children.Add(exportButton);
                actionsPanel.Children.Add(printButton);
                Grid.SetRow(actionsPanel, 2);
                contentGrid.Children.Add(actionsPanel);

                // הגדרת תוכן הדיאלוג
                _lastGeneratedCodesDialog.Content = contentGrid;

                // הצגת הדיאלוג
                await _lastGeneratedCodesDialog.ShowAsync();
            }
        }

        private async void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                await LoadCodes(item.Tag.ToString());
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (FilterComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                await LoadCodes(item.Tag.ToString());
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("קובץ CSV", new List<string>() { ".csv" });
            savePicker.SuggestedFileName = $"payment-codes-{DateTime.Now:yyyyMMdd}";

            // נדרש עבור WinUI 3
            if (MainWindow.Current != null)
            {
                InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(MainWindow.Current));

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    CachedFileManager.DeferUpdates(file);

                    StringBuilder csvContent = new StringBuilder();
                    csvContent.AppendLine("קוד,סכום,תאריך יצירה,מומש,תאריך מימוש,משתמש");

                    foreach (var code in _codes)
                    {
                        csvContent.AppendLine($"{code.Code},{code.Amount},{code.CreatedDate},{(code.IsUsed ? "כן" : "לא")},{code.UsedDate},{code.UsedByUsername}");
                    }

                    await FileIO.WriteTextAsync(file, csvContent.ToString());

                    FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status == FileUpdateStatus.Complete)
                    {
                        StatusInfoBar.Title = "ייצוא הושלם";
                        StatusInfoBar.Message = $"הקובץ נשמר ב: {file.Path}";
                        StatusInfoBar.Severity = InfoBarSeverity.Success;
                        StatusInfoBar.IsOpen = true;
                    }
                    else
                    {
                        StatusInfoBar.Title = "שגיאה";
                        StatusInfoBar.Message = "ייצוא הקובץ נכשל";
                        StatusInfoBar.Severity = InfoBarSeverity.Error;
                        StatusInfoBar.IsOpen = true;
                    }
                }
            }
            else
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = "לא ניתן לפתוח את חלון הייצוא";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            // מימוש מלא של הדפסה ב-WinUI 3 דורש שימוש ב-PrintManager
            // זהו מימוש מצומצם שיש להרחיב באפליקציה אמיתית
            StatusInfoBar.Title = "הדפסה";
            StatusInfoBar.Message = "פונקציית ההדפסה הישירה עדיין לא מומשה";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.IsOpen = true;
        }

        private async void ExportLastGenerated_Click(object sender, RoutedEventArgs e)
        {
            if (_lastGeneratedCodes.Count == 0)
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = "אין קודים אחרונים לייצוא";
                StatusInfoBar.Severity = InfoBarSeverity.Warning;
                StatusInfoBar.IsOpen = true;
                return;
            }

            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("קובץ CSV", new List<string>() { ".csv" });
            savePicker.SuggestedFileName = $"last-generated-codes-{DateTime.Now:yyyyMMdd}";

            // נדרש עבור WinUI 3
            if (MainWindow.Current != null)
            {
                InitializeWithWindow.Initialize(savePicker, WindowNative.GetWindowHandle(MainWindow.Current));

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    CachedFileManager.DeferUpdates(file);

                    StringBuilder csvContent = new StringBuilder();
                    csvContent.AppendLine("קוד,סכום,תאריך יצירה");

                    foreach (var code in _lastGeneratedCodes)
                    {
                        csvContent.AppendLine($"{code.Code},{code.Amount},{code.CreatedDate}");
                    }

                    await FileIO.WriteTextAsync(file, csvContent.ToString());

                    FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status == FileUpdateStatus.Complete)
                    {
                        StatusInfoBar.Title = "ייצוא הושלם";
                        StatusInfoBar.Message = $"הקובץ נשמר ב: {file.Path}";
                        StatusInfoBar.Severity = InfoBarSeverity.Success;
                        StatusInfoBar.IsOpen = true;

                        // סגירת דיאלוג אם פתוח
                        //if (_lastGeneratedCodesDialog != null)
                        //{
                        //    _lastGeneratedCodesDialog.Hide();
                        //}
                    }
                    else
                    {
                        StatusInfoBar.Title = "שגיאה";
                        StatusInfoBar.Message = "ייצוא הקובץ נכשל";
                        StatusInfoBar.Severity = InfoBarSeverity.Error;
                        StatusInfoBar.IsOpen = true;
                    }
                }
            }
            else
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = "לא ניתן לפתוח את חלון הייצוא";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
            }
        }

        private void PrintLastGenerated_Click(object sender, RoutedEventArgs e)
        {
            if (_lastGeneratedCodes.Count == 0)
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = "אין קודים אחרונים להדפסה";
                StatusInfoBar.Severity = InfoBarSeverity.Warning;
                StatusInfoBar.IsOpen = true;
                return;
            }

            // מימוש מלא של הדפסה ב-WinUI 3 דורש שימוש ב-PrintManager
            // זהו מימוש מצומצם שיש להרחיב באפליקציה אמיתית
            StatusInfoBar.Title = "הדפסה";
            StatusInfoBar.Message = "פונקציית ההדפסה של קודים אחרונים עדיין לא מומשה";
            StatusInfoBar.Severity = InfoBarSeverity.Informational;
            StatusInfoBar.IsOpen = true;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            // סגירת האפליקציה
            Application.Current.Exit();
        }
    }

    public class CodeViewModel
    {
        public string Code { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsUsed { get; set; }
        public string CreatedDate { get; set; } = string.Empty;
        public string UsedDate { get; set; } = string.Empty;
        public string UsedByUsername { get; set; } = string.Empty;
    }
}