using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Threading.Tasks;
using SaferCode.Services;

namespace SaferCode.Pages
{
    public sealed partial class CodeRedemptionPage : Page
    {
        private DatabaseService _databaseService;
        private ObservableCollection<RedemptionHistoryItem> _redemptionHistory;
        private int _userId;
        private string _userName = string.Empty;
        private decimal _currentBalance;

        public CodeRedemptionPage()
        {
            this.InitializeComponent();
            _databaseService = new DatabaseService();
            _redemptionHistory = new ObservableCollection<RedemptionHistoryItem>();
            RedemptionHistoryGrid.ItemsSource = _redemptionHistory;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is UserData userData)
            {
                _userId = userData.UserId;
                _userName = $"{userData.FirstName} {userData.LastName}";
                _currentBalance = userData.Balance;

                WelcomeTextBlock.Text = $"שלום {_userName}";
                UpdateBalanceDisplay();

                await LoadRedemptionHistory();
            }
        }

        private void UpdateBalanceDisplay()
        {
            BalanceTextBlock.Text = $"היתרה הנוכחית שלך היא: {_currentBalance} ₪";
        }

        private async Task LoadRedemptionHistory()
        {
            _redemptionHistory.Clear();

            try
            {
                using (var connection = new SQLiteConnection("Data Source=C:\\ProgramData\\KioskTorani\\LocalSaferServer.db;Version=3;"))
                {
                    connection.Open();

                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = @"
                            SELECT PC.Code, PC.Amount, PC.UsedDate 
                            FROM PaymentCodes PC
                            WHERE PC.UsedByUserId = @UserId AND PC.IsUsed = 1
                            ORDER BY PC.UsedDate DESC
                            LIMIT 50";
                        command.Parameters.AddWithValue("@UserId", _userId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var code = reader.GetString(0);
                                var amount = reader.GetDecimal(1);
                                var usedDateUnix = reader.GetInt64(2);
                                var usedDate = DateTimeOffset.FromUnixTimeSeconds(usedDateUnix).DateTime;

                                _redemptionHistory.Add(new RedemptionHistoryItem
                                {
                                    Code = code,
                                    Amount = amount,
                                    Date = usedDate.ToString("dd/MM/yyyy HH:mm")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = $"אירעה שגיאה בטעינת ההיסטוריה: {ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
            }
        }

        private async void RedeemButton_Click(object sender, RoutedEventArgs e)
        {
            await RedeemCode();
        }

        private async void CodeTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await RedeemCode();
            }
        }

        private async Task RedeemCode()
        {
            string code = CodeTextBox.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = "נא להזין קוד תקין";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
                return;
            }

            RedeemButton.IsEnabled = false;
            CodeTextBox.IsEnabled = false;

            try
            {
                var result = await _databaseService.RedeemCode(code, _userId);

                if (result.Success)
                {
                    _currentBalance += result.Amount;
                    UpdateBalanceDisplay();

                    StatusInfoBar.Title = "הצלחה";
                    StatusInfoBar.Message = result.Message;
                    StatusInfoBar.Severity = InfoBarSeverity.Success;

                    CodeTextBox.Text = string.Empty;

                    // עדכון היסטוריית הטעינות
                    await LoadRedemptionHistory();
                }
                else
                {
                    StatusInfoBar.Title = "שגיאה";
                    StatusInfoBar.Message = result.Message;
                    StatusInfoBar.Severity = InfoBarSeverity.Error;
                }
            }
            catch (Exception ex)
            {
                StatusInfoBar.Title = "שגיאה";
                StatusInfoBar.Message = $"אירעה שגיאה: {ex.Message}";
                StatusInfoBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                RedeemButton.IsEnabled = true;
                CodeTextBox.IsEnabled = true;
                StatusInfoBar.IsOpen = true;
                CodeTextBox.Focus(FocusState.Programmatic);
            }
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

    public class RedemptionHistoryItem
    {
        public string Code { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Date { get; set; } = string.Empty;
    }

    public class UserData
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }
}