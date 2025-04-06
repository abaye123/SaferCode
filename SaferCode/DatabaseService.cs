using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace SaferCode.Services
{
    public class PaymentCode
    {
        public string Code { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UsedDate { get; set; }
        public int? UsedByUserId { get; set; }
    }

    public class DatabaseService
    {
        private readonly string _dbPath = "C:\\ProgramData\\KioskTorani\\LocalSaferServer.db";
        private readonly Random _random = new Random();

        public DatabaseService()
        {
            EnsurePaymentCodesTableExists();
        }

        private void EnsurePaymentCodesTableExists()
        {
            using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS PaymentCodes (
                            Code TEXT PRIMARY KEY,
                            Amount REAL NOT NULL,
                            IsUsed INTEGER NOT NULL DEFAULT 0,
                            CreatedDate INTEGER NOT NULL,
                            UsedDate INTEGER,
                            UsedByUserId INTEGER,
                            FOREIGN KEY (UsedByUserId) REFERENCES Users (rowid)
                        )";
                    command.ExecuteNonQuery();
                }
            }
        }

        public async Task<List<string>> GeneratePaymentCodes(int count, decimal amount)
        {
            List<string> generatedCodes = new List<string>();

            await Task.Run(() => {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            for (int i = 0; i < count; i++)
                            {
                                string code = GenerateUniqueCode();
                                while (!IsCodeUnique(connection, code))
                                {
                                    code = GenerateUniqueCode();
                                }

                                using (var command = new SQLiteCommand(connection))
                                {
                                    command.CommandText = @"
                                        INSERT INTO PaymentCodes (Code, Amount, IsUsed, CreatedDate)
                                        VALUES (@Code, @Amount, 0, @CreatedDate)";
                                    command.Parameters.AddWithValue("@Code", code);
                                    command.Parameters.AddWithValue("@Amount", amount);
                                    command.Parameters.AddWithValue("@CreatedDate", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                                    command.ExecuteNonQuery();
                                    generatedCodes.Add(code);
                                }
                            }
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            });

            return generatedCodes;
        }

        private string GenerateUniqueCode()
        {
            const string chars = "0123456789";
            char[] codeArray = new char[8]; // 8-digit code

            for (int i = 0; i < codeArray.Length; i++)
            {
                codeArray[i] = chars[_random.Next(chars.Length)];
            }

            return new string(codeArray);
        }

        private bool IsCodeUnique(SQLiteConnection connection, string code)
        {
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = "SELECT COUNT(*) FROM PaymentCodes WHERE Code = @Code";
                command.Parameters.AddWithValue("@Code", code);
                long count = (long)command.ExecuteScalar();
                return count == 0;
            }
        }

        public async Task<List<PaymentCode>> GetPaymentCodes(bool? isUsed = null)
        {
            List<PaymentCode> codes = new List<PaymentCode>();

            try
            {
                await Task.Run(() => {
                    using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                    {
                        connection.Open();
                        using (var command = new SQLiteCommand(connection))
                        {
                            string whereClause = isUsed.HasValue ? "WHERE IsUsed = @IsUsed" : "";
                            command.CommandText = $@"
                        SELECT Code, Amount, IsUsed, CreatedDate, UsedDate, UsedByUserId
                        FROM PaymentCodes
                        {whereClause}
                        ORDER BY CreatedDate DESC";

                            if (isUsed.HasValue)
                            {
                                command.Parameters.AddWithValue("@IsUsed", isUsed.Value ? 1 : 0);
                            }

                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var code = new PaymentCode
                                    {
                                        Code = reader.GetString(0),
                                        Amount = reader.GetDecimal(1),
                                        IsUsed = reader.GetInt32(2) == 1,
                                        CreatedDate = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)).DateTime,
                                    };

                                    if (!reader.IsDBNull(4))
                                    {
                                        code.UsedDate = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)).DateTime;
                                    }

                                    if (!reader.IsDBNull(5))
                                    {
                                        code.UsedByUserId = reader.GetInt32(5);
                                    }

                                    codes.Add(code);
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                // רשום את השגיאה ללוג
                System.Diagnostics.Debug.WriteLine($"שגיאה בקבלת קודי תשלום: {ex.Message}");
                // החזר רשימה ריקה במקום לזרוק שגיאה למעלה
            }

            return codes; // תמיד יוחזר אובייקט חוקי, אפילו אם ריק
        }

        public async Task<(bool Success, string Message, decimal Amount)> RedeemCode(string code, int userId)
        {
            bool success = false;
            string message = "";
            decimal amount = 0;

            await Task.Run(() => {
                using (var connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Check if code exists and is unused
                            using (var command = new SQLiteCommand(connection))
                            {
                                command.CommandText = @"
                                    SELECT Amount FROM PaymentCodes 
                                    WHERE Code = @Code AND IsUsed = 0";
                                command.Parameters.AddWithValue("@Code", code);

                                var result = command.ExecuteScalar();
                                if (result == null)
                                {
                                    message = "קוד שגוי או כבר נוצל";
                                    return;
                                }

                                amount = Convert.ToDecimal(result);
                            }

                            // Mark code as used
                            using (var command = new SQLiteCommand(connection))
                            {
                                command.CommandText = @"
                                    UPDATE PaymentCodes
                                    SET IsUsed = 1, UsedDate = @UsedDate, UsedByUserId = @UserId
                                    WHERE Code = @Code";
                                command.Parameters.AddWithValue("@UsedDate", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                                command.Parameters.AddWithValue("@UserId", userId);
                                command.Parameters.AddWithValue("@Code", code);
                                command.ExecuteNonQuery();
                            }

                            // Add payment to user
                            using (var command = new SQLiteCommand(connection))
                            {
                                command.CommandText = @"
                                    INSERT INTO Payments (PaymentDate, Amount, userid)
                                    VALUES (@PaymentDate, @Amount, @UserId)";
                                command.Parameters.AddWithValue("@PaymentDate", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                                command.Parameters.AddWithValue("@Amount", amount);
                                command.Parameters.AddWithValue("@UserId", userId);
                                command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            success = true;
                            message = $"נטען בהצלחה {amount} ש\"ח";
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            message = $"שגיאה: {ex.Message}";
                        }
                    }
                }
            });

            return (success, message, amount);
        }
    }
}