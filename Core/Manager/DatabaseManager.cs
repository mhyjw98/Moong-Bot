using Discord;
using Discord.WebSocket;
using Google.Apis.Gmail.v1.Data;
using Microsoft.Data.Sqlite;
using MoongBot.Core.Commands;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;


namespace MoongBot.Core.Manager
{
    public class DatabaseManager
    {
        private static CoinMarketManager _coinManager = new CoinMarketManager();
        public readonly string _connectionString;

        public DatabaseManager()
        {
            //_connectionString = @"Data Source=C:\Users\rhaps\SQLite\moongbot.sqlite";
            string ResourcesFolder = "Resource";
            string SQLiteDBFile = "moongbot.sqlite";
            string guildPath = Path.GetFullPath(ResourcesFolder) + "/" + SQLiteDBFile;
            _connectionString = $"Data Source={guildPath}";
        }
        public async Task RegisterAudioFileAsync(string word, string filePath, ISocketMessageChannel channel, ulong userId, bool isOwnerOnly = false, int volume = 70)
        {
            Console.WriteLine($"RegisterAudioFileAsync: {word}, {filePath}, Volume: {volume}");
            bool isChange = false;
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Check if the word already exists in the database
                    string selectQuery = @"SELECT FilePath FROM AudioFiles WHERE Word = @Word;";
                    string existingFilePath = null;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@Word", word);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            existingFilePath = result.ToString();
                        }
                    }

                    // If the word exists, delete the existing file
                    if (!string.IsNullOrEmpty(existingFilePath) && File.Exists(existingFilePath))
                    {
                        File.Delete(existingFilePath);
                        Console.WriteLine($"Deleted the existing audio file: {existingFilePath}");
                        isChange = true;
                    }

                    // Insert or Update logic
                    string query = @"INSERT INTO AudioFiles (Word, FilePath, IsOwnerOnly, Volume) VALUES (@Word, @FilePath, @IsOwnerOnly, @Volume)
                             ON CONFLICT(Word) DO UPDATE SET FilePath = excluded.FilePath, IsOwnerOnly = excluded.IsOwnerOnly, Volume = excluded.Volume;;";


                    using (var cmd = new SqliteCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Word", word);
                        cmd.Parameters.AddWithValue("@FilePath", filePath);
                        cmd.Parameters.AddWithValue("@IsOwnerOnly", isOwnerOnly ? 1 : 0);
                        cmd.Parameters.AddWithValue("@Volume", volume);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // In-memory dictionary update (optional)
                if (AudioManager.AudioFiles.ContainsKey(word))
                {
                    AudioManager.AudioFiles[word] = filePath;
                }
                else
                {
                    AudioManager.AudioFiles.Add(word, filePath);
                }

                if (isChange)
                {
                    await channel.SendMessageAsync($"기존 명령어 \"{word}\"에 등록된 mp3파일을 새 mp3파일로 변경하였습니다.");
                }
                else
                {
                    await channel.SendMessageAsync($"\"{word}\" 단어에 대한 mp3 파일이 등록되었습니다.");
                }                                  
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : RegisterAudioFileAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<Dictionary<string, string>> LoadAudioFilesAsync()
        {
            var audioFiles = new Dictionary<string, string>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT Word, FilePath FROM AudioFiles;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string word = reader.GetString(0);
                                string filePath = reader.GetString(1);
                                audioFiles[word] = filePath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : LoadAudioFilesAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }

            return audioFiles;
        }

        public async Task DeleteAudioFileAsync(string word, ITextChannel channel, ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 먼저 파일 경로를 조회합니다.
                    string selectQuery = @"SELECT FilePath, IsOwnerOnly FROM AudioFiles WHERE Word = @Word;";
                    string existingFilePath = null;
                    bool isWordOwnedByAdmin = false;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@Word", word);
                        using (var reader = await selectCmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                existingFilePath = reader.GetString(0);
                                isWordOwnedByAdmin = reader.GetInt32(1) == 1;
                            }
                            else
                            {
                                await channel.SendMessageAsync($"\"{word}\" 단어에 해당하는 mp3 파일을 찾지못했습니다.");
                                return;
                            }
                        }
                    }

                    if (isWordOwnedByAdmin && userId != ConfigManager.Config.OwnerId)
                    {
                        await channel.SendMessageAsync("이 단어를 삭제할 권한이 없습니다.");
                        return;
                    }

                    if (!string.IsNullOrEmpty(existingFilePath) && File.Exists(existingFilePath))
                    {
                        File.Delete(existingFilePath);
                        Console.WriteLine($"Deleted the audio file: {existingFilePath}");
                    }

                    // 데이터베이스에서 레코드 삭제
                    string deleteQuery = "DELETE FROM AudioFiles WHERE Word = @Word;";
                    using (var deleteCmd = new SqliteCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@Word", word);
                        int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"Successfully deleted the record for word: {word}");
                            await channel.SendMessageAsync($"\"{word}\" 단어에 연결된 mp3 파일이 삭제되었습니다.");
                        }
                        else
                        {
                            Console.WriteLine($"No record found for word: {word}");
                            await channel.SendMessageAsync($"\"{word}\"에 해당하는 mp3파일을 찾지 못했습니다.");
                        }
                    }

                    if (AudioManager.AudioFiles.ContainsKey(word))
                    {
                        AudioManager.AudioFiles.Remove(word);
                    }
                    else
                    {
                        Console.WriteLine($"No audio file found for word: {word}");
                        await channel.SendMessageAsync($"\"{word}\"는 등록되지 않은 단어입니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : DeleteAudioFileAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<bool> IsWordOwnedByAdmin(string word)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"SELECT IsOwnerOnly FROM AudioFiles WHERE Word = @Word;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Word", word);

                        var result = await command.ExecuteScalarAsync();
                        if (result != null)
                        {
                            return Convert.ToInt32(result) == 1;
                        }
                        else
                        {
                            // 단어가 존재하지 않으면 false 반환
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : IsWordOwnedByAdmin 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false; // 오류 발생 시 false 반환
            }
        }

        public async Task<(List<string> regularWords, List<string> adminWords)> GetAllWordsAsync()
        {
            var regularWords = new List<string>();
            var adminWords = new List<string>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT Word, IsOwnerOnly FROM AudioFiles;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string word = reader.GetString(0);
                                bool isOwnerOnly = reader.GetInt32(1) == 1;

                                if (isOwnerOnly)
                                {
                                    adminWords.Add(word);
                                }
                                else
                                {
                                    regularWords.Add(word);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetAllWordsAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }

            return (regularWords, adminWords);
        }

        public async Task<bool> AddDollarAsync(ulong userId, double amount)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 사용자가 이미 존재하는지 확인
                    string selectQuery = "SELECT Dollar FROM Users WHERE UserId = @UserId;";
                    double currentDollar = 0.0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            currentDollar = Convert.ToDouble(result);
                        }
                    }

                    // 새로운 금액을 계산
                    double newDollarAmount = Math.Round(currentDollar + amount, 2);

                    // 사용자가 이미 존재하면 업데이트, 그렇지 않으면 삽입
                    string upsertQuery = @"INSERT INTO Users (UserId, Dollar) VALUES (@UserId, @Dollar)
                                   ON CONFLICT(UserId) DO UPDATE SET Dollar = @Dollar;";

                    using (var upsertCmd = new SqliteCommand(upsertQuery, connection))
                    {
                        upsertCmd.Parameters.AddWithValue("@UserId", userId);
                        upsertCmd.Parameters.AddWithValue("@Dollar", newDollarAmount);

                        await upsertCmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : AddDollarAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }

        public async Task<bool> UseDollarAsync(ulong userId, double amount)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 사용자의 현재 dollar 확인
                    string selectQuery = "SELECT Dollar FROM Users WHERE UserId = @UserId;";
                    double currentDollar = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result == null || Convert.ToDouble(result) < amount)
                        {
                            // 잔액이 부족하거나 사용자가 존재하지 않음
                            return false;
                        }

                        currentDollar = Convert.ToDouble(result);
                    }

                    // 새로운 금액을 계산
                    double newDollarAmount = Math.Round(currentDollar - amount, 2);

                    // 사용자의 Dollar 업데이트
                    string updateQuery = "UPDATE Users SET Dollar = @Dollar WHERE UserId = @UserId;";

                    using (var updateCmd = new SqliteCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@UserId", userId);
                        updateCmd.Parameters.AddWithValue("@Dollar", newDollarAmount);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : UseDollarAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }

        public async Task<double> GetUserDollarAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT Dollar FROM Users WHERE UserId = @UserId;";
                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        return result != null ? Math.Round(Convert.ToDouble(result), 2) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetUserDollarAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return 0;
            }
        }

        public async Task SaveLottoTicketAsync(ulong userId, List<int> numbers)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                INSERT INTO LottoTickets (UserId, Number1, Number2, Number3, Number4, Number5, Number6)
                VALUES (@UserId, @Number1, @Number2, @Number3, @Number4, @Number5, @Number6);";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", (long)userId);
                        command.Parameters.AddWithValue("@Number1", numbers[0]);
                        command.Parameters.AddWithValue("@Number2", numbers[1]);
                        command.Parameters.AddWithValue("@Number3", numbers[2]);
                        command.Parameters.AddWithValue("@Number4", numbers[3]);
                        command.Parameters.AddWithValue("@Number5", numbers[4]);
                        command.Parameters.AddWithValue("@Number6", numbers[5]);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : SaveLottoTicketAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task LoadLottoTicketsAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT UserId, Number1, Number2, Number3, Number4, Number5, Number6 FROM LottoTickets;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ulong userId = (ulong)reader.GetInt64(0);
                                var numbers = new List<int>
                        {
                            reader.GetInt32(1),
                            reader.GetInt32(2),
                            reader.GetInt32(3),
                            reader.GetInt32(4),
                            reader.GetInt32(5),
                            reader.GetInt32(6)
                        };

                                if (!LottoManager._userTickets.ContainsKey(userId))
                                {
                                    LottoManager._userTickets[userId] = new List<List<int>>();
                                }
                                LottoManager._userTickets[userId].Add(numbers);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : LoadLottoTicketsAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task DeleteAllLottoTicketsAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                var query = "DELETE FROM LottoTickets"; // 테이블 이름이 'LottoTickets'라고 가정
                using (var command = new SqliteCommand(query, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();

                    Console.WriteLine($"{affectedRows}개의 로또 티켓이 삭제되었습니다.");
                }
            }
        }

        public async Task AddDdingAsync(ulong userId, ulong amount)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = "UPDATE Users SET dding = dding + @Amount WHERE UserId = @UserId";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Amount", amount);
                    command.Parameters.AddWithValue("@UserId", userId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<bool> AddSlotCoinAsync(ulong userId, int amount)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 사용자가 이미 존재하는지 확인
                    string selectQuery = "SELECT Coin FROM Users WHERE UserId = @UserId;";
                    int currentCoin = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            currentCoin = Convert.ToInt32(result);
                        }
                    }

                    // 새로운 금액을 계산
                    int newCoinAmount = currentCoin + amount;

                    // 사용자가 이미 존재하면 업데이트, 그렇지 않으면 삽입
                    string upsertQuery = @"INSERT INTO Users (UserId, Coin) VALUES (@UserId, @Coin)
                                   ON CONFLICT(UserId) DO UPDATE SET Coin = @Coin;";

                    using (var upsertCmd = new SqliteCommand(upsertQuery, connection))
                    {
                        upsertCmd.Parameters.AddWithValue("@UserId", userId);
                        upsertCmd.Parameters.AddWithValue("@Coin", newCoinAmount);

                        await upsertCmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : AddSlotCoinAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }

        public async Task<bool> UseSlotCoinAsync(ulong userId, int amount)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 사용자의 현재 coin 확인
                    string selectQuery = "SELECT Coin FROM Users WHERE UserId = @UserId;";
                    int currentCoin = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result == null || Convert.ToInt32(result) < amount)
                        {
                            // 잔액이 부족하거나 사용자가 존재하지 않음
                            return false;
                        }

                        currentCoin = Convert.ToInt32(result);
                    }

                    // 새로운 금액을 계산
                    double newCoinAmount = currentCoin - amount;

                    // 사용자의 Dollar 업데이트
                    string updateQuery = "UPDATE Users SET Coin = @Coin WHERE UserId = @UserId;";

                    using (var updateCmd = new SqliteCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@UserId", userId);
                        updateCmd.Parameters.AddWithValue("@Coin", newCoinAmount);
                        await updateCmd.ExecuteNonQueryAsync();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : UseSlotCoinAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }

        public async Task<int> GetUserSlotCoinAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT Coin FROM Users WHERE UserId = @UserId;";
                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetUserSlotCoinAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return 0;
            }
        }
        public async Task<bool> BuyItemAsync(ulong userId, int itemPrice)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. 사용자의 현재 dding 값을 확인
                        var getDdingQuery = "SELECT dding FROM Users WHERE UserId = @UserId";
                        int currentDding = 0;

                        using (var getDdingCommand = new SqliteCommand(getDdingQuery, connection, transaction))
                        {
                            getDdingCommand.Parameters.AddWithValue("@UserId", userId);
                            var result = await getDdingCommand.ExecuteScalarAsync();

                            if (result != null && int.TryParse(result.ToString(), out currentDding))
                            {
                                // dding 값을 성공적으로 가져옴
                            }
                            else
                            {
                                // 사용자가 존재하지 않거나 dding 값을 가져오는 데 실패함
                                return false;
                            }
                        }

                        // 2. 사용자의 dding이 상품 가격보다 많은지 확인
                        if (currentDding < itemPrice)
                        {
                            // dding이 부족하여 구매를 진행할 수 없음
                            return false;
                        }

                        // 3. 상품 가격만큼 dding을 차감
                        var updateDdingQuery = "UPDATE Users SET dding = dding - @ItemPrice WHERE UserId = @UserId";

                        using (var updateDdingCommand = new SqliteCommand(updateDdingQuery, connection, transaction))
                        {
                            updateDdingCommand.Parameters.AddWithValue("@ItemPrice", itemPrice);
                            updateDdingCommand.Parameters.AddWithValue("@UserId", userId);

                            var rowsAffected = await updateDdingCommand.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                // dding이 성공적으로 차감됨
                                transaction.Commit(); // 트랜잭션 커밋
                                return true;
                            }
                            else
                            {
                                // dding 차감에 실패함
                                return false;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Error message : BuyItemAsync 에서 에러 발생");
                        transaction.Rollback(); // 오류 발생 시 트랜잭션 롤백
                        throw;
                    }
                }
            }
        }

        public async Task<(int ddingBalance, int coinBalance , double dollarBalance)> GetAllBalanceAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT dding, Coin, Dollar FROM Users WHERE UserId = @UserId;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int ddingBalance = reader["dding"] != DBNull.Value ? Convert.ToInt32(reader["dding"]) : 0;
                                int coinBalance = reader["Coin"] != DBNull.Value ? Convert.ToInt32(reader["Coin"]) : 0;
                                double dollarBalance = reader["Dollar"] != DBNull.Value ? Math.Round(Convert.ToDouble(reader["Dollar"]), 2) : 0.0;

                                return (ddingBalance, coinBalance, dollarBalance);
                            }
                            else
                            {
                                // 사용자가 없으면 기본값 0으로 반환
                                return (0, 0, 0.0);
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                await ExceptionManager.HandleExceptionAsync(ex);
                return (0, 0, 0.0);
            }
            
        }

        public async Task LogSlotMachineResultAsync(ulong userId, int result)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = "INSERT INTO SlotMachineResults (UserId, Result) VALUES (@UserId, @Result);";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);  
                    command.Parameters.AddWithValue("@Result", result);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<int> GetTotalAmountAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = "SELECT SUM(Result) FROM SlotMachineResults WHERE UserId = 1;";
                using (var command = new SqliteCommand(query, connection))
                {
                    var result = await command.ExecuteScalarAsync();
                    return result != DBNull.Value ? Convert.ToInt32(result) : 0;
                }
            }
        }
        public async Task<(ulong, int)> GetTopSlotUserAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
            SELECT UserId, SUM(Result) AS TotalAmount
            FROM SlotMachineResults
            WHERE UserId != 1
            GROUP BY UserId
            ORDER BY TotalAmount DESC
            LIMIT 1;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                ulong userId = (ulong)(long)reader["UserId"];
                                int totalAmount = Convert.ToInt32(reader["TotalAmount"]);
                                return (userId, totalAmount);
                            }
                        }
                    }
                }

                return (1,-1); // 결과가 없는 경우
            }
            catch (Exception ex)
            {
                await ExceptionManager.HandleExceptionAsync(ex);
                return (1, -1);
            }
        }
        public async Task<List<(ulong UserId, int TotalAmount)>> GetUserRankingsAsync()
        {
            var rankings = new List<(ulong UserId, int TotalAmount)>();
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                    SELECT UserId, SUM(Result) AS TotalAmount
                    FROM SlotMachineResults
                    WHERE UserId != 1
                    GROUP BY UserId
                    ORDER BY TotalAmount DESC;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ulong userId = (ulong)(long)reader["UserId"];
                                int totalAmount = Convert.ToInt32(reader["TotalAmount"]);
                                rankings.Add((userId, totalAmount));
                            }
                        }
                    }
                }

                return rankings;
            }
            catch(Exception ex)
            {
                await ExceptionManager.HandleExceptionAsync(ex);
                return null;
            }
            
        }
        public async Task RegistHOFAsync(string userName, int coinValue)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // SQL 삽입 쿼리
                string query = @"
                    INSERT INTO SlotMachineHOF (UserName, TotalAmount)
                    VALUES (@UserName, @TotalAmount);";

                using (var command = new SqliteCommand(query, connection))
                {
                    // 매개변수 추가
                    command.Parameters.AddWithValue("@UserName", userName);
                    command.Parameters.AddWithValue("@TotalAmount", coinValue);

                    // 명령 실행
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        public async Task<List<(string UserName, int TotalAmount)>> GetSlotHOFAsync()
        {
            var rankings = new List<(string UserName, int TotalAmount)>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT UserName, TotalAmount
                    FROM SlotMachineHOF
                    ORDER BY HOFId ASC;";

                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string userName = reader["UserName"].ToString();
                            int totalAmount = Convert.ToInt32(reader["TotalAmount"]);
                            rankings.Add((userName, totalAmount));
                        }
                    }
                }
            }

            return rankings;
        }
        public async Task AddSpecialAsync(ulong userId, int amount)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 사용자가 이미 존재하는지 확인
                    string selectQuery = "SELECT special FROM Users WHERE UserId = @UserId;";
                    int currentSpecial = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            currentSpecial = Convert.ToInt32(result);
                        }
                    }

                    // 새로운 special 값을 계산
                    int newSpecialAmount = currentSpecial + amount;

                    // 사용자가 이미 존재하면 업데이트, 그렇지 않으면 삽입
                    string upsertQuery = @"INSERT INTO Users (UserId, special) VALUES (@UserId, @Special)
                           ON CONFLICT(UserId) DO UPDATE SET special = @Special;";

                    using (var upsertCmd = new SqliteCommand(upsertQuery, connection))
                    {
                        upsertCmd.Parameters.AddWithValue("@UserId", userId);
                        upsertCmd.Parameters.AddWithValue("@Special", newSpecialAmount);

                        await upsertCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : AddSpecialAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task AddSlotTicketAsync(ulong userId, int amount)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 사용자가 이미 존재하는지 확인
                    string selectQuery = "SELECT ticket FROM Users WHERE UserId = @UserId;";
                    int currentTicket = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            currentTicket = Convert.ToInt32(result);
                        }
                    }

                    // 새로운 special 값을 계산
                    int newTicketAmount = currentTicket + amount;

                    // 사용자가 이미 존재하면 업데이트, 그렇지 않으면 삽입
                    string upsertQuery = @"INSERT INTO Users (UserId, ticket) VALUES (@UserId, @ticket)
                           ON CONFLICT(UserId) DO UPDATE SET ticket = @ticket;";

                    using (var upsertCmd = new SqliteCommand(upsertQuery, connection))
                    {
                        upsertCmd.Parameters.AddWithValue("@UserId", userId);
                        upsertCmd.Parameters.AddWithValue("@ticket", newTicketAmount);

                        await upsertCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : AddSlotTicketAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<int> GetSpecialValueAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT special FROM Users WHERE UserId = @UserId;";
                    int specialValue = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            specialValue = Convert.ToInt32(result);
                        }
                    }

                    return specialValue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetSpecialValueAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return 0;
            }
        }
        public async Task<int> GetTicketValueAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT ticket FROM Users WHERE UserId = @UserId;";
                    int ticketValue = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            ticketValue = Convert.ToInt32(result);
                        }
                    }

                    return ticketValue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetTicketValueAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return 0;
            }
        }
        public async Task UseSpecialAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT special FROM Users WHERE UserId = @UserId;";
                    int specialValue = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            specialValue = Convert.ToInt32(result);
                        }
                    }

                    if (specialValue > 0)
                    {
                        specialValue--;

                        string updateQuery = "UPDATE Users SET special = @Special WHERE UserId = @UserId;";
                        using (var updateCmd = new SqliteCommand(updateQuery, connection))
                        {
                            updateCmd.Parameters.AddWithValue("@UserId", userId);
                            updateCmd.Parameters.AddWithValue("@Special", specialValue);
                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : UseSpecialAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task UseSlotTicketAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = "SELECT ticket FROM Users WHERE UserId = @UserId;";
                    int ticketValue = 0;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            ticketValue = Convert.ToInt32(result);
                        }
                    }

                    if (ticketValue > 0)
                    {
                        ticketValue--;

                        string updateQuery = "UPDATE Users SET ticket = @ticket WHERE UserId = @UserId;";
                        using (var updateCmd = new SqliteCommand(updateQuery, connection))
                        {
                            updateCmd.Parameters.AddWithValue("@UserId", userId);
                            updateCmd.Parameters.AddWithValue("@ticket", ticketValue);
                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : UseSlotTicketAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<DateTime> RecordLoanAsync(ulong userId, int loanAmount, int isCoin)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    DateTime repaymentDueDate = DateTime.Now.AddDays(5);

                    double rate = InteractionManager.interestRate;
                    decimal interest = loanAmount * ((decimal)rate / 100);

                    // 먼저 해당 유저가 이미 있는지 확인합니다.
                    string selectQuery = @"SELECT COUNT(1) FROM Loans WHERE UserId = @UserId AND IsCoin = @IsCoin";
                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        selectCmd.Parameters.AddWithValue("@IsCoin", isCoin);
                        var result = await selectCmd.ExecuteScalarAsync();
                        int userExists = Convert.ToInt32(result);

                        if (userExists > 0)
                        {
                            //기존 대출과 새 대출이 다르다면 새로 추가하고 아니면 합쳐지도록 수정 필요
                            string updateQuery = @"UPDATE Loans 
                                           SET LoanAmount = LoanAmount + @LoanAmount, 
                                               Interest = Interest + @Interest,
                                               RepaymentDueDate = @RepaymentDueDate 
                                           WHERE UserId = @UserId AND IsCoin = @IsCoin";
                            using (var updateCmd = new SqliteCommand(updateQuery, connection))
                            {
                                updateCmd.Parameters.AddWithValue("@UserId", userId);
                                updateCmd.Parameters.AddWithValue("@LoanAmount", loanAmount);
                                updateCmd.Parameters.AddWithValue("@Interest", interest);
                                updateCmd.Parameters.AddWithValue("@RepaymentDueDate", repaymentDueDate);
                                updateCmd.Parameters.AddWithValue("@IsCoin", isCoin);

                                await updateCmd.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            // 유저가 없으면 새로 삽입합니다.
                            string insertQuery = @"INSERT INTO Loans (UserId, LoanAmount, Interest, RepaymentDueDate, PenaltyCount, IsCoin) 
                                           VALUES (@UserId, @LoanAmount, @Interest, @RepaymentDueDate, 0, @IsCoin);";

                            using (var insertCmd = new SqliteCommand(insertQuery, connection))
                            {
                                insertCmd.Parameters.AddWithValue("@UserId", userId);
                                insertCmd.Parameters.AddWithValue("@LoanAmount", loanAmount);
                                insertCmd.Parameters.AddWithValue("@Interest", interest);
                                insertCmd.Parameters.AddWithValue("@RepaymentDueDate", repaymentDueDate);
                                insertCmd.Parameters.AddWithValue("@IsCoin", isCoin);

                                await insertCmd.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    return repaymentDueDate;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : RecordLoanAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return DateTime.MinValue;
            }
        }

        public async Task IncreaseInterestDailyAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string updateQuery = @"UPDATE Loans 
                                       SET Interest = Interest + LoanAmount * 0.5
                                       WHERE RepaymentDueDate > @CurrentDate;";

                    using (var updateCmd = new SqliteCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@CurrentDate", DateTime.Now);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : IncreaseInterestDailyAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<(bool, string)> ProcessRepaymentAsync(ulong userId, int amountPaid, bool isCoinRepay)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 코인 대출 또는 달러 대출 여부를 결정하는 쿼리
                    string selectQuery = @"SELECT LoanAmount, Interest, isCoin, RepaymentDueDate FROM Loans 
                                   WHERE UserId = @UserId AND isCoin = @IsCoin;";
                    int loanAmount = 0;
                    int interest = 0;
                    bool isCoin = isCoinRepay;
                    DateTime dueDate = DateTime.MinValue;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        selectCmd.Parameters.AddWithValue("@IsCoin", isCoinRepay ? 1 : 0);
                        using (var reader = await selectCmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                loanAmount = reader.GetInt32(0);
                                interest = reader.GetInt32(1);
                                dueDate = reader.GetDateTime(3);
                            }
                        }
                    }

                    int totalDue = loanAmount + interest;

                    if (totalDue == 0)
                    {
                        return (false, "대출을 하지 않았습니다");
                    }

                    if (amountPaid >= totalDue)
                    {
                        bool isSuccess;

                        if (isCoin)
                        {
                            isSuccess = await UseSlotCoinAsync(userId, totalDue);
                        }
                        else
                        {
                            isSuccess = await UseDollarAsync(userId, totalDue);
                        }

                        if (isSuccess)
                        {
                            string deleteQuery = @"DELETE FROM Loans WHERE UserId = @UserId AND isCoin = @IsCoin;";
                            using (var deleteCmd = new SqliteCommand(deleteQuery, connection))
                            {
                                deleteCmd.Parameters.AddWithValue("@UserId", userId);
                                deleteCmd.Parameters.AddWithValue("@IsCoin", isCoinRepay ? 1 : 0);
                                await deleteCmd.ExecuteNonQueryAsync();
                            }

                            return (true, "대출금 상환");
                        }
                        else
                        {
                            return (false, "잔액 부족");
                        }
                    }
                    else if (amountPaid >= interest)
                    {
                        bool isSuccess;

                        if (isCoin)
                        {
                            isSuccess = await UseSlotCoinAsync(userId, interest);
                        }
                        else
                        {
                            isSuccess = await UseDollarAsync(userId, interest);
                        }

                        if (isSuccess)
                        {
                            string updateQuery = @"UPDATE Loans 
                                           SET Interest = 0 
                                           WHERE UserId = @UserId AND isCoin = @IsCoin;";
                            using (var updateCmd = new SqliteCommand(updateQuery, connection))
                            {
                                updateCmd.Parameters.AddWithValue("@UserId", userId);
                                updateCmd.Parameters.AddWithValue("@IsCoin", isCoinRepay ? 1 : 0);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            return (true, "이자 상환");
                        }
                        else
                        {
                            return (false, "잔액 부족");
                        }
                    }
                    else
                    {
                        return (false, "이자보다 작은 금액 입력");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message: ProcessRepaymentAsync에서 에러 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return (false, "에러");
            }
        }

        public async Task<bool> CheckIfLoanExistsAndOverdue(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = @"SELECT 1 FROM Loans 
                                       WHERE UserId = @UserId AND RepaymentDueDate < @CurrentDate;";

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        selectCmd.Parameters.AddWithValue("@CurrentDate", DateTime.Now);

                        var result = await selectCmd.ExecuteScalarAsync();
                        return result != null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : CheckIfLoanExistsAndOverdue 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }

        public async Task SetPenaltyCount(ulong userId, int count)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string updateQuery = @"UPDATE Loans SET PenaltyCount = @PenaltyCount WHERE UserId = @UserId;";
                    using (var updateCmd = new SqliteCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@PenaltyCount", count);
                        updateCmd.Parameters.AddWithValue("@UserId", userId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : SetPenaltyCount 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<int> GetPenaltyCount(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = @"SELECT PenaltyCount FROM Loans WHERE UserId = @UserId;";
                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        var result = await selectCmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetPenaltyCount 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return 0;
            }
        }

        public async Task DecrementPenaltyCount(ulong userId)
        {
            try
            {
                Console.WriteLine("패널티 횟수 1회 제거 실행");
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string updateQuery = @"UPDATE Loans SET PenaltyCount = PenaltyCount - 1 WHERE UserId = @UserId;";
                    using (var updateCmd = new SqliteCommand(updateQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@UserId", userId);
                        await updateCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : DecrementPenaltyCount 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task DeleteLoanRecord(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string deleteQuery = @"DELETE FROM Loans WHERE UserId = @UserId;";
                    using (var deleteCmd = new SqliteCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@UserId", userId);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : DeleteLoanRecord 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<(int, int, bool, DateTime)> GetTotalRepaymentAmountAsync(ulong userId, bool isCoin)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 사용자의 대출금과 이자를 가져오는 쿼리
                    string selectQuery = @"SELECT LoanAmount, Interest, IsCoin, RepaymentDueDate FROM Loans WHERE UserId = @UserId AND IsCoin = @IsCoin;";
                    int loanAmount = -1;
                    int interest = -1;
                    int isCoinValue = isCoin ? 1 : 0;
                    DateTime date = DateTime.MinValue;

                    using (var selectCmd = new SqliteCommand(selectQuery, connection))
                    {
                        selectCmd.Parameters.AddWithValue("@UserId", userId);
                        selectCmd.Parameters.AddWithValue("@IsCoin", isCoinValue);

                        using (var reader = await selectCmd.ExecuteReaderAsync())
                        {
                            if (reader.Read())
                            {
                                loanAmount = reader.GetInt32(0);
                                interest = reader.GetInt32(1);
                                isCoin = reader.GetInt32(2) == 1 ? true : false;
                                date = reader.GetDateTime(3);
                            }
                        }
                    }

                    // 대출금과 이자를 합산하여 반환
                    return (loanAmount, interest, isCoin, date);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetTotalRepaymentAmountAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return (-1,-1, false, DateTime.MinValue);  // 오류 발생 시 -1 반환 (이 값을 사용하여 오류를 확인할 수 있음)
            }
        }

        public async Task<(bool, bool, double)> RepayLoanAsync(ulong userId, bool isCoinRepay)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 유저의 대출금과 이자 총합을 가져온다
                    var (loanAmount, interest, isCoin, date) = await GetTotalRepaymentAmountAsync(userId, isCoinRepay);
                    double totalAmount = loanAmount + interest;

                    if (loanAmount == -1 || interest == -1)
                    {
                        // 오류가 발생했으면 false 반환
                        return (false, false, -2);
                    }

                    double repaymentAmount = 0;
                    int newCoinAmount = 0;
                    double newDollarAmount = 0;
                    double remainingAmount = 0;

                    if (isCoinRepay)
                    {
                        // 코인으로 갚을 경우
                        int userCoin = await GetUserSlotCoinAsync(userId);

                        if (userCoin >= totalAmount)
                        {
                            repaymentAmount = totalAmount;
                            newCoinAmount = userCoin - (int)totalAmount;
                        }
                        else
                        {
                            repaymentAmount = userCoin;
                            newCoinAmount = 0;
                        }
                        remainingAmount = totalAmount - repaymentAmount;
                        // 유저의 코인 잔액을 업데이트
                        string updateUserQuery = "UPDATE Users SET Coin = @Coin WHERE UserId = @UserId;";
                        using (var updateUserCmd = new SqliteCommand(updateUserQuery, connection))
                        {
                            updateUserCmd.Parameters.AddWithValue("@Coin", newCoinAmount);
                            updateUserCmd.Parameters.AddWithValue("@UserId", userId);
                            await updateUserCmd.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // 달러로 갚을 경우
                        double userDollar = await GetUserDollarAsync(userId);
                        
                        if (userDollar >= totalAmount)
                        {
                            repaymentAmount = totalAmount;
                            newDollarAmount = userDollar - totalAmount;
                        }
                        else
                        {
                            repaymentAmount = userDollar;
                            newDollarAmount = 0;
                        }                       

                        // 코인을 팔아 부족한 금액을 충당
                        remainingAmount = totalAmount - repaymentAmount;                        
                        double useDollar = 0;
                        while (remainingAmount > 0)
                        {                            
                            var userHoldings = await GetUserCoinHoldingsForAllCoinsAsync(userId);

                            foreach (var (coinName, totalQuantity, averagePrice) in userHoldings)
                            {
                                if (remainingAmount <= 0) break;

                                double currentPrice = (await GetCoinPriceAsync(coinName)).Price;
                                double portionQuantity = remainingAmount / currentPrice;
                                double sellQuantity = Math.Min(totalQuantity, Math.Ceiling(portionQuantity * 100) / 100);

                                if (sellQuantity > 0)
                                {
                                    string sellResult = await _coinManager.SellCoinAsync(userId, coinName, sellQuantity.ToString());
                                    Console.WriteLine(sellResult);

                                    double sellAmount = sellQuantity * currentPrice;
                                    remainingAmount -= sellAmount;
                                    useDollar += sellAmount;
                                }
                            }
                        }
                        // 유저의 달러 잔액을 업데이트
                        string updateUserQuery = "UPDATE Users SET Dollar = @Dollar WHERE UserId = @UserId;";
                        using (var updateUserCmd = new SqliteCommand(updateUserQuery, connection))
                        {
                            updateUserCmd.Parameters.AddWithValue("@Dollar", newDollarAmount);
                            updateUserCmd.Parameters.AddWithValue("@UserId", userId);
                            await updateUserCmd.ExecuteNonQueryAsync();
                        }
                        await UseDollarAsync(userId, useDollar);
                    }
                                        
                    if (remainingAmount <= 0)
                    {
                        // 대출 상환 처리를 완료한 후 대출 기록을 삭제한다
                        string updateLoanQuery = "DELETE FROM Loans WHERE UserId = @UserId AND IsCoin = @IsCoin;";
                        using (var updateLoanCmd = new SqliteCommand(updateLoanQuery, connection))
                        {
                            updateLoanCmd.Parameters.AddWithValue("@UserId", userId);
                            updateLoanCmd.Parameters.AddWithValue("@IsCoin", isCoinRepay);
                            await updateLoanCmd.ExecuteNonQueryAsync();
                        }

                        return (true, true, 0);
                    }
                    else
                    {
                        return (true, false, remainingAmount);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message: RepayLoanAsync에서 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return (false, false, -3);
            }
        }

        public async Task<(double PreviousPrice, string CoinName)> GetLatestCoinHistoryAsync(int coinId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // `CoinPriceHistory`와 `Coins` 테이블을 JOIN하여 CoinName과 ClosePrice를 가져옴
                    string query = @"
                        SELECT cph.ClosePrice, c.CoinName 
                        FROM CoinPriceHistory cph
                        JOIN Coins c ON cph.CoinId = c.CoinId
                        WHERE cph.CoinId = @CoinId
                        ORDER BY cph.Timestamp DESC
                        LIMIT 1;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CoinId", coinId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                double previousPrice = reader.GetDouble(0); // ClosePrice
                                string coinName = reader.GetString(1); // CoinName
                                return (previousPrice, coinName);
                            }
                            throw new Exception("No price history found for the coin.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetLatestCoinHistoryAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return (0, "");
            }
        }
        public async Task<List<(int CoinId, string CoinName, double CurrentPrice)>> GetAllCoinsAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT CoinId, CoinName, CurrentPrice FROM Coins;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var coins = new List<(int CoinId, string CoinName, double CurrentPrice)>();
                            while (await reader.ReadAsync())
                            {
                                int coinId = reader.GetInt32(0);
                                string coinName = reader.GetString(1);
                                double currentPrice = reader.GetDouble(2);
                                coins.Add((coinId, coinName, currentPrice));
                            }
                            return coins;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error message : GetAllCoinsAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                var coins = new List<(int CoinId, string CoinName, double CurrentPrice)>();
                return coins;
            }            
        }
        // 특정 코인의 현재 가격을 가져오는 메서드
        public async Task<(int CoinId, double Price, string Symbol, string FullCoinName)> GetCoinPriceAsync(string coinName)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 입력된 이름이 두 글자일 때는 간단한 이름 탐색을, 그렇지 않으면 풀 네임 탐색을 한다
                string query = (coinName.Length == 2)
                    ? @"SELECT CoinId, CoinName, CurrentPrice, Symbol FROM Coins WHERE CoinName LIKE @ShortCoinName"
                    : @"SELECT CoinId, CoinName, CurrentPrice, Symbol FROM Coins WHERE CoinName = @CoinName";

                using (var command = new SqliteCommand(query, connection))
                {
                    if (coinName.Length == 2)
                    {
                        // 입력된 이름이 두 글자일 경우 앞 두 글자에 %를 붙여서 LIKE 검색
                        string shortCoinName = coinName + "%";
                        command.Parameters.AddWithValue("@ShortCoinName", shortCoinName);
                    }
                    else
                    {
                        // 입력된 이름이 두 글자가 아닐 경우, 풀 네임으로 정확하게 검색
                        command.Parameters.AddWithValue("@CoinName", coinName);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int coinId = reader.GetInt32(0);
                            string fullCoinName = reader.GetString(1);
                            double price = reader.GetDouble(2);
                            string symbol = reader.GetString(3);
                            return (coinId, price, symbol, fullCoinName);
                        }
                        throw new Exception("Coin not found");
                    }
                }
            }
        }

        public async Task<double> GetCoinCurrentPriceAsync(int coinId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT CurrentPrice
                        FROM Coins
                        WHERE CoinId = @CoinId;"; 

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CoinId", coinId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                double currentPrice = reader.GetDouble(0);
                                return currentPrice;
                            }
                        }
                    }
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetCoinCurrentPriceAsync 에서 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return -1;
            }
        }

        public async Task<Dictionary<int, double>> GetCoinPrevPriceAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();                   
                    string query = @"
                        SELECT CoinId, CurrentPrice
                        FROM Coins;
                        ";

                    Dictionary<int, double> result = new Dictionary<int, double>();

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            // 모든 데이터를 반복적으로 읽음
                            while (await reader.ReadAsync())
                            {
                                int coinId = reader.GetInt32(0);
                                double currentPrice = reader.GetDouble(1);
                                result[coinId] = currentPrice;
                            }
                        }
                    }

                    return result; // 결과 반환
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetCoinPrevPriceAsync 에서 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return new Dictionary<int, double>();
            }
        }

        // 유저가 코인을 매수할 때 기록
        public async Task BuyUserCoinAsync(ulong userId, int coinId, double quantity, double price)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 1. 기존 보유 데이터를 확인
                string selectQuery = @"SELECT Quantity, Price FROM UserCoinHoldings WHERE UserId = @UserId AND CoinId = @CoinId;";
                using (var selectCommand = new SqliteCommand(selectQuery, connection))
                {
                    selectCommand.Parameters.AddWithValue("@UserId", userId);
                    selectCommand.Parameters.AddWithValue("@CoinId", coinId);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // 기존에 보유한 수량과 가격이 있는 경우
                            double existingQuantity = reader.GetDouble(0);
                            double existingPrice = reader.GetDouble(1);

                            // 새로운 매수량과 가격으로 가중 평균 계산
                            double totalQuantity = existingQuantity + quantity;
                            double newAveragePrice = ((existingQuantity * existingPrice) + (quantity * price)) / totalQuantity;

                            // 기존 데이터 업데이트 (수량과 평균 가격)
                            string updateQuery = @"UPDATE UserCoinHoldings 
                                           SET Quantity = @NewQuantity, Price = @NewPrice 
                                           WHERE UserId = @UserId AND CoinId = @CoinId;";
                            using (var updateCommand = new SqliteCommand(updateQuery, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@NewQuantity", totalQuantity);
                                updateCommand.Parameters.AddWithValue("@NewPrice", newAveragePrice);
                                updateCommand.Parameters.AddWithValue("@UserId", userId);
                                updateCommand.Parameters.AddWithValue("@CoinId", coinId);

                                await updateCommand.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            // 기존 데이터가 없을 경우, 새로 추가
                            string insertQuery = @"INSERT INTO UserCoinHoldings (UserId, CoinId, Price, Quantity) 
                                           VALUES (@UserId, @CoinId, @Price, @Quantity);";
                            using (var insertCommand = new SqliteCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@UserId", userId);
                                insertCommand.Parameters.AddWithValue("@CoinId", coinId);
                                insertCommand.Parameters.AddWithValue("@Price", price);  // 매수 시점의 가격
                                insertCommand.Parameters.AddWithValue("@Quantity", quantity);  // 매수한 수량
                                await insertCommand.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
        }

        public async Task<(int CoinId, double TotalQuantity, double AveragePrice)> GetUserCoinHoldingsForSpecificCoinAsync(ulong userId, int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
            SELECT 
                SUM(UCH.Quantity) as TotalQuantity, 
                SUM(UCH.Quantity * UCH.Price) / SUM(UCH.Quantity) as AveragePrice 
            FROM 
                UserCoinHoldings UCH 
            WHERE 
                UCH.UserId = @UserId AND UCH.CoinId = @CoinId
            GROUP BY 
                UCH.CoinId;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@CoinId", coinId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            double totalQuantity = reader.GetDouble(0);
                            double averagePrice = reader.GetDouble(1);

                            return (coinId, totalQuantity, averagePrice);
                        }
                        else
                        {
                            return (-1, 0, 0); // 데이터를 찾지 못한 경우 기본값 반환
                        }
                    }
                }
            }
        }

        // 유저가 보유한 모든 코인에 대한 정보 가져오기
        public async Task<List<(string CoinName, double TotalQuantity, double AveragePrice)>> GetUserCoinHoldingsForAllCoinsAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
                    SELECT 
                        C.CoinName, 
                        SUM(UCH.Quantity) as TotalQuantity, 
                        SUM(UCH.Quantity * UCH.Price) / SUM(UCH.Quantity) as AveragePrice 
                    FROM 
                        UserCoinHoldings UCH 
                    JOIN 
                        Coins C ON UCH.CoinId = C.CoinId
                    WHERE 
                        UCH.UserId = @UserId
                    GROUP BY 
                        C.CoinName;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var holdings = new List<(string CoinName, double TotalQuantity, double AveragePrice)>();
                            while (await reader.ReadAsync())
                            {
                                string coinName = reader.GetString(0);
                                double totalQuantity = reader.GetDouble(1);
                                double averagePrice = reader.GetDouble(2);
                                holdings.Add((coinName, totalQuantity, averagePrice));
                            }
                            return holdings;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"GetUserCoinHoldingsForAllCoinsAsync에서 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return null;               
            }
        }

        // 유저가 코인을 매도할 때, 특정 가격의 코인을 매도
        public async Task SellUserCoinAsync(ulong userId, int coinId, double quantity, double currentPrice)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 해당 유저가 보유한 코인 중에서 가장 오래된 구매 기록을 우선적으로 가져옴 (FIFO 방식)
                string selectQuery = @"SELECT HoldingId, Quantity, Price FROM UserCoinHoldings WHERE UserId = @UserId AND CoinId = @CoinId ORDER BY HoldingId ASC;";
                using (var command = new SqliteCommand(selectQuery, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@CoinId", coinId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var holdings = new List<(int HoldingId, double Quantity, double Price)>();
                        while (await reader.ReadAsync())
                        {
                            int holdingId = reader.GetInt32(0);
                            double availableQuantity = reader.GetDouble(1);
                            double purchasePrice = reader.GetDouble(2); // 매입 가격
                            holdings.Add((holdingId, availableQuantity, purchasePrice));
                        }

                        // 매도 수량이 보유한 수량보다 많은 경우 예외 처리
                        double totalAvailableQuantity = holdings.Sum(h => h.Quantity);
                        if (totalAvailableQuantity < quantity)
                        {
                            throw new Exception("매도할 수량이 보유한 수량보다 많습니다.");
                        }

                        // 매도할 수량만큼 차례대로 차감 (FIFO 방식)하고, 각 기록에 대해 차익 계산
                        double totalProfitOrLoss = 0;

                        foreach (var (holdingId, availableQuantity, purchasePrice) in holdings)
                        {
                            if (quantity <= 0)
                                break;

                            // 현재 기록에서 판매할 수량 계산
                            double sellQuantity = Math.Min(quantity, availableQuantity);

                            // 매입 가격과 현재 가격을 이용한 차익 계산
                            double profitOrLoss = (currentPrice - purchasePrice) * sellQuantity;
                            totalProfitOrLoss += profitOrLoss; // 총 차익/손실

                            // 매도 후 수량 차감
                            if (availableQuantity <= sellQuantity)
                            {
                                // 매도할 수량이 보유 수량과 같거나 클 경우 해당 기록 삭제
                                string deleteQuery = @"DELETE FROM UserCoinHoldings WHERE HoldingId = @HoldingId;";
                                using (var deleteCommand = new SqliteCommand(deleteQuery, connection))
                                {
                                    deleteCommand.Parameters.AddWithValue("@HoldingId", holdingId);
                                    await deleteCommand.ExecuteNonQueryAsync();
                                }
                            }
                            else
                            {
                                // 매도할 수량이 보유 수량보다 적을 경우 수량만 차감
                                string updateQuery = @"UPDATE UserCoinHoldings SET Quantity = Quantity - @Quantity WHERE HoldingId = @HoldingId;";
                                using (var updateCommand = new SqliteCommand(updateQuery, connection))
                                {
                                    updateCommand.Parameters.AddWithValue("@Quantity", sellQuantity);
                                    updateCommand.Parameters.AddWithValue("@HoldingId", holdingId);
                                    await updateCommand.ExecuteNonQueryAsync();
                                }
                            }

                            // 매도 후 남은 수량 업데이트
                            quantity -= sellQuantity;

                            // 판매 금액을 달러로 추가
                            double totalValue = sellQuantity * currentPrice;
                            await AddDollarAsync(userId, totalValue);
                        }

                        // 총 수익/손실을 기록
                        await RecordUserProfitAsync(userId, totalProfitOrLoss);
                    }
                }
            }
        }

        public async Task SaveCoinPriceHistoryAsync(int coinId, double openPrice, double highPrice, double lowPrice, double closePrice, DateTime timestamp)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                INSERT INTO CoinPriceHistory (CoinId, OpenPrice, HighPrice, LowPrice, ClosePrice, Timestamp)
                VALUES (@CoinId, @OpenPrice, @HighPrice, @LowPrice, @ClosePrice, @Timestamp);";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CoinId", coinId);
                        command.Parameters.AddWithValue("@OpenPrice", openPrice);
                        command.Parameters.AddWithValue("@HighPrice", highPrice);
                        command.Parameters.AddWithValue("@LowPrice", lowPrice);
                        command.Parameters.AddWithValue("@ClosePrice", closePrice);
                        command.Parameters.AddWithValue("@Timestamp", timestamp);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveCoinPriceHistoryAsync 오류 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task DeleteOldCoinPriceHistoryAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"DELETE FROM CoinPriceHistory 
                         WHERE CoinId = @CoinId 
                         AND Timestamp < DATE('now', '-10 days');";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<(ulong UserId, double TotalProfit)>> GetUserProfitRankingAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    // UserProfit 테이블에서 유저별로 총 수익을 계산
                    string query = @"
                    SELECT UserId, SUM(Profit) AS TotalProfit
                    FROM UserProfit
                    GROUP BY UserId
                    ORDER BY TotalProfit DESC;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var rankings = new List<(ulong UserId, double TotalProfit)>();
                            while (await reader.ReadAsync())
                            {
                                ulong userId = (ulong)reader.GetInt64(0);  // UserId
                                double totalProfit = reader.GetDouble(1);   // 총 수익
                                rankings.Add((userId, totalProfit));        // 리스트에 추가
                            }
                            return rankings; // 유저별 수익 리스트 반환
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error message : GetUserProfitRankingAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return null;
            }
        }

        public async Task<List<(string CoinName, double Quantity, double CurrentValue)>> GetUserCoinHoldingsAsync(ulong userId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"SELECT Coins.CoinName, UserCoinHoldings.Quantity, (UserCoinHoldings.Quantity * Coins.CurrentPrice) AS CurrentValue
                         FROM UserCoinHoldings
                         JOIN Coins ON UserCoinHoldings.CoinId = Coins.CoinId
                         WHERE UserCoinHoldings.UserId = @UserId;";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var holdings = new List<(string CoinName, double Quantity, double CurrentValue)>();
                        while (await reader.ReadAsync())
                        {
                            string coinName = reader.GetString(0);
                            double quantity = reader.GetDouble(1);
                            double currentValue = reader.GetDouble(2);
                            holdings.Add((coinName, quantity, currentValue));
                        }
                        return holdings;
                    }
                }
            }
        }

        public async Task<Dictionary<ulong, List<(string CoinName, double TotalQuantity, double AveragePrice)>>> GetAllUserCoinHoldingsAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = @"
            SELECT 
                UCH.UserId,
                C.CoinName, 
                SUM(UCH.Quantity) as TotalQuantity, 
                SUM(UCH.Quantity * UCH.Price) / SUM(UCH.Quantity) as AveragePrice 
            FROM 
                UserCoinHoldings UCH 
            JOIN 
                Coins C ON UCH.CoinId = C.CoinId
            GROUP BY 
                UCH.UserId, C.CoinName;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var allHoldings = new Dictionary<ulong, List<(string CoinName, double TotalQuantity, double AveragePrice)>>();

                            while (await reader.ReadAsync())
                            {
                                ulong userId = (ulong)reader.GetInt64(0);
                                string coinName = reader.GetString(1);
                                double totalQuantity = reader.GetDouble(2);
                                double averagePrice = reader.GetDouble(3);

                                if (!allHoldings.ContainsKey(userId))
                                {
                                    allHoldings[userId] = new List<(string CoinName, double TotalQuantity, double AveragePrice)>();
                                }
                                allHoldings[userId].Add((coinName, totalQuantity, averagePrice));
                            }

                            return allHoldings;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAllUserCoinHoldingsAsync에서 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return null;
            }
        }
        public async Task UpdateCoinPriceAsync(int coinId, double newPrice)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = "UPDATE Coins SET CurrentPrice = @NewPrice WHERE CoinId = @CoinId;";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@NewPrice", newPrice);
                    command.Parameters.AddWithValue("@CoinId", coinId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task SavePreviousPriceAsync(int coinId, double previousPrice)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 코인 ID가 존재하면 수정, 존재하지 않으면 삽입하는 쿼리
                string query = @"
                    INSERT INTO PreviousCoinPrices (CoinId, PreviousPrice, Timestamp)
                    VALUES (@CoinId, @PreviousPrice, CURRENT_TIMESTAMP)
                    ON CONFLICT(CoinId)
                    DO UPDATE SET
                        PreviousPrice = @PreviousPrice,
                        Timestamp = CURRENT_TIMESTAMP;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);
                    command.Parameters.AddWithValue("@PreviousPrice", previousPrice);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<double> GetPreviousPriceAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 이전 가격을 가져오는 쿼리
                string query = @"SELECT PreviousPrice FROM PreviousCoinPrices WHERE CoinId = @CoinId;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);

                    // 쿼리 실행 및 결과 읽기
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // 이전 가격이 있으면 반환
                            return reader.GetDouble(0);
                        }
                        else
                        {
                            // 이전 가격이 없으면 null 반환
                            return 0;
                        }
                    }
                }
            }
        }

        public async Task<(bool, string)> SaveAutoTradeConditionAsync(ulong userId, string coinName, double targetPrice, double quantity, bool isBuying)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 코인 이름이 존재하는지 확인하는 쿼리 (존재 여부 체크)
                    string checkCoinQuery = @"SELECT COUNT(1) FROM Coins WHERE CoinName = @CoinName;";

                    using (var checkCoinCommand = new SqliteCommand(checkCoinQuery, connection))
                    {
                        checkCoinCommand.Parameters.AddWithValue("@CoinName", coinName);
                        var coinExists = (long)await checkCoinCommand.ExecuteScalarAsync();

                        if (coinExists == 0)
                        {
                            // 코인이 존재하지 않는 경우 메시지 반환
                            return (false, $"코인 '{coinName}'을(를) 찾을 수 없습니다.");
                        }
                    }

                    // 자동 매매 조건 저장 쿼리
                    string query = @"INSERT INTO AutoTradeConditions (UserId, CoinName, TargetPrice, Quantity, IsBuying) 
                             VALUES (@UserId, @CoinName, @TargetPrice, @Quantity, @IsBuying);";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@CoinName", coinName);
                        command.Parameters.AddWithValue("@TargetPrice", targetPrice);
                        command.Parameters.AddWithValue("@Quantity", quantity);
                        command.Parameters.AddWithValue("@IsBuying", isBuying);

                        await command.ExecuteNonQueryAsync();
                    }

                    // 저장 성공 시 메시지 반환
                    return (true, $"자동 매매가 성공적으로 설정되었습니다: {coinName}, 목표 가격: {targetPrice}, 수량: {quantity}, 매매 타입: {(isBuying ? "매수" : "매도")}");
                }
            }
            catch (Exception ex)
            {
                // 에러 발생 시 메시지 반환
                Console.WriteLine($"자동 매매 조건 저장 중 오류 발생: {ex.Message}");
                return (false, "자동 매매 조건을 설정하는 중 문제가 발생했습니다.");
            }
        }

        public async Task<(bool, List<string>)> GetAllAutoTradeConditionsAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // userId에 해당하는 데이터를 조회하는 쿼리
                    string selectQuery = @"SELECT CoinName, TargetPrice, Quantity, IsBuying FROM AutoTradeConditions WHERE UserId = @UserId;";

                    using (var command = new SqliteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);

                        var tradeConditions = new List<string>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string coinName = reader.GetString(0);
                                double targetPrice = reader.GetDouble(1);
                                double quantity = reader.GetDouble(2);
                                bool isBuying = reader.GetBoolean(3);

                                string condition = $"{coinName}, 목표 가격: {targetPrice}, 수량: {quantity}, 매매 타입: {(isBuying ? "매수" : "매도")}";
                                tradeConditions.Add(condition);
                            }
                        }

                        if (tradeConditions.Count > 0)
                        {
                            return (true, tradeConditions);
                        }
                        else
                        {
                            return (false, new List<string> { "설정한 자동매매 기록이 없습니다." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"자동 매매 조건 조회 중 오류 발생: {ex.Message}");
                return (false, new List<string> { "자동 매매 조건을 조회하는 중 문제가 발생했습니다." });
            }
        }

        public async Task<(bool, string)> DeleteAllUserAutoTradeConditionsAsync(ulong userId)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // userId에 해당하는 데이터를 삭제하는 쿼리
                    string deleteQuery = @"DELETE FROM AutoTradeConditions WHERE UserId = @UserId;";

                    using (var command = new SqliteCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return (true, $"{rowsAffected}개의 자동매매 데이터가 삭제되었습니다.");
                        }
                        else
                        {
                            return (false, "삭제할 자동 매매 조건이 없습니다.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"자동 매매 조건 삭제 중 오류 발생: {ex.Message}");
                return (false, "자동 매매 조건을 삭제하는 중 문제가 발생했습니다.");
            }
        }
        public async Task<List<(ulong UserId, string CoinName, double TargetPrice, double Quantity, bool IsBuying)>> GetAutoTradeConditionsByCoinIdAsync(string name)
        {
            var conditions = new List<(ulong, string, double, double, bool)>();

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT UserId, CoinName, TargetPrice, Quantity, IsBuying 
                         FROM AutoTradeConditions
                         WHERE CoinName = @CoinName;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinName", name);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            ulong userId = (ulong)reader.GetInt64(0);
                            string coinName = reader.GetString(1);
                            double targetPrice = reader.GetDouble(2);
                            double quantity = reader.GetDouble(3);
                            bool isBuying = reader.GetBoolean(4);

                            conditions.Add((userId, coinName, targetPrice, quantity, isBuying));
                        }
                    }
                }
            }

            return conditions;
        }
        public async Task<bool> DeleteAutoTradeConditionAsync(ulong userId, string coinName, bool isBuying)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"DELETE FROM AutoTradeConditions 
                             WHERE UserId = @UserId 
                             AND CoinName = @CoinName 
                             AND IsBuying = @IsBuying;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@CoinName", coinName);
                        command.Parameters.AddWithValue("@IsBuying", isBuying);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"자동 매매 조건 삭제 완료: UserId={userId}, CoinName={coinName}, IsBuying={isBuying}");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine($"삭제할 자동 매매 조건을 찾을 수 없음: UserId={userId}, CoinName={coinName}, IsBuying={isBuying}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"자동 매매 조건 삭제 중 오류 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }
        public async Task<bool> DeleteAllAutoTradeConditionAsync()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"DELETE FROM AutoTradeConditions;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"자동 매매 모든데이터 삭제 중 오류 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }
        public async Task<List<(DateTime Timestamp, double Open, double High, double Low, double Close)>> GetCoinPriceHistoryAsync(int coinId, DateTime startTime, DateTime endTime)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 코인의 가격 변동 정보 (Open, High, Low, Close)를 가져오는 쿼리
                    // 2시 ~ 8시 시간대의 데이터는 제외하고, 지정된 기간 내의 데이터만 가져옴
                    //string query = @"
                    //    SELECT Timestamp, OpenPrice, HighPrice, LowPrice, ClosePrice
                    //    FROM CoinPriceHistory
                    //    WHERE CoinId = @CoinId
                    //    AND Timestamp BETWEEN @StartTime AND @EndTime
                    //    AND (strftime('%H', Timestamp) NOT BETWEEN '02' AND '08')
                    //    ORDER BY Timestamp ASC;";
                    string query = @"
                        SELECT Timestamp, OpenPrice, HighPrice, LowPrice, ClosePrice
                        FROM CoinPriceHistory
                        WHERE CoinId = @CoinId
                        AND Timestamp BETWEEN @StartTime AND @EndTime
                        ORDER BY Timestamp ASC;";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        // 파라미터 바인딩
                        command.Parameters.AddWithValue("@CoinId", coinId);
                        command.Parameters.AddWithValue("@StartTime", startTime);
                        command.Parameters.AddWithValue("@EndTime", endTime);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var priceHistory = new List<(DateTime Timestamp, double Open, double High, double Low, double Close)>();
                            while (await reader.ReadAsync())
                            {
                                DateTime timestamp = reader.GetDateTime(0);
                                double open = reader.GetDouble(1);
                                double high = reader.GetDouble(2);
                                double low = reader.GetDouble(3);
                                double close = reader.GetDouble(4);

                                // 필터링된 시간대의 데이터만 추가
                                priceHistory.Add((timestamp, open, high, low, close));
                            }
                            return priceHistory;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCoinPriceHistoryAsync 오류 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return new List<(DateTime Timestamp, double Open, double High, double Low, double Close)>();
            }
        }

        // CoinId에 해당하는 코인의 이름을 가져오는 메서드
        public async Task<string> GetCoinNameByIdAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = "SELECT CoinName FROM Coins WHERE CoinId = @CoinId;";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);

                    var result = await command.ExecuteScalarAsync();
                    if (result != null)
                    {
                        return result.ToString();
                    }
                    else
                    {
                        throw new Exception($"Coin with ID {coinId} not found.");
                    }
                }
            }
        }
        public async Task<(string, int)> GetCoinIdByNameAsync(string coinName)
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // 입력된 이름이 두 글자일 때는 간단한 이름 탐색을, 그렇지 않으면 풀 네임 탐색을 한다
                    string query = (coinName.Length == 2)
                        ? @"SELECT CoinName, CoinId FROM Coins WHERE CoinName LIKE @ShortCoinName"
                        : @"SELECT CoinName, CoinId FROM Coins WHERE CoinName = @CoinName";

                    using (var command = new SqliteCommand(query, connection))
                    {
                        if (coinName.Length == 2)
                        {
                            // 입력된 이름이 두 글자일 경우 앞 두 글자에 %를 붙여서 LIKE 검색
                            string shortCoinName = coinName + "%";
                            command.Parameters.AddWithValue("@ShortCoinName", shortCoinName);
                        }
                        else
                        {
                            // 입력된 이름이 두 글자가 아닐 경우, 풀 네임으로 정확하게 검색
                            command.Parameters.AddWithValue("@CoinName", coinName);
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string fullCoinName = reader.GetString(0); // CoinName
                                int coinId = reader.GetInt32(1); // CoinId
                                return (fullCoinName, coinId);
                            }
                            else
                            {
                                return (null, -1);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message: GetCoinIdByNameAsync 에서 에러 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                throw;
            }
        }
        public async Task DeleteCoinAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // SqliteTransaction 타입으로 트랜잭션 시작
                using (var transaction = await connection.BeginTransactionAsync() as SqliteTransaction)
                {
                    try
                    {
                        // 1. 사용자 코인 보유 기록 삭제
                        string deleteUserHoldingsQuery = "DELETE FROM UserCoinHoldings WHERE CoinId = @CoinId;";
                        using (var deleteUserHoldingsCommand = new SqliteCommand(deleteUserHoldingsQuery, connection, transaction))
                        {
                            deleteUserHoldingsCommand.Parameters.AddWithValue("@CoinId", coinId);
                            await deleteUserHoldingsCommand.ExecuteNonQueryAsync();
                        }

                        // 2. 코인 가격 히스토리 삭제
                        string deleteCoinHistoryQuery = "DELETE FROM CoinPriceHistory WHERE CoinId = @CoinId;";
                        using (var deleteCoinHistoryCommand = new SqliteCommand(deleteCoinHistoryQuery, connection, transaction))
                        {
                            deleteCoinHistoryCommand.Parameters.AddWithValue("@CoinId", coinId);
                            await deleteCoinHistoryCommand.ExecuteNonQueryAsync();
                        }

                        // 3. 코인 정보 삭제
                        string deleteCoinQuery = "DELETE FROM Coins WHERE CoinId = @CoinId;";
                        using (var deleteCoinCommand = new SqliteCommand(deleteCoinQuery, connection, transaction))
                        {
                            deleteCoinCommand.Parameters.AddWithValue("@CoinId", coinId);
                            await deleteCoinCommand.ExecuteNonQueryAsync();
                        }

                        // 트랜잭션 커밋
                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        // 트랜잭션 롤백
                        Console.WriteLine($"Error message : DeleteCoinAsync 에서 에러 발생");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task DeleteUserCoinHoldingAsync(ulong userId, int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"DELETE FROM UserCoinHoldings 
                         WHERE UserId = @UserId AND CoinId = @CoinId;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@CoinId", coinId);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"UserCoinHoldings에서 UserId={userId}, CoinId={coinId}에 해당하는 데이터가 삭제되었습니다.");
                    }
                    else
                    {
                        Console.WriteLine($"UserId={userId}, CoinId={coinId}에 해당하는 데이터를 찾을 수 없습니다.");
                    }
                }
            }
        }
        public async Task RecordUserProfitAsync(ulong userId, double profit)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"INSERT INTO UserProfit (UserId, Profit, Timestamp) 
                         VALUES (@UserId, @Profit, @Timestamp);";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Profit", profit);
                    command.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<(ulong UserId, double Quantity, double BuyPrice)>> GetUsersHoldingCoinAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT UserId, Quantity, Price
                         FROM UserCoinHoldings
                         WHERE CoinId = @CoinId AND Quantity > 0;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        var users = new List<(ulong UserId, double Quantity, double BuyPrice)>();

                        while (await reader.ReadAsync())
                        {
                            ulong userId = (ulong)reader.GetInt64(0);
                            double quantity = reader.GetDouble(1);
                            double buyPrice = reader.GetDouble(2);

                            users.Add((userId, quantity, buyPrice));
                        }

                        return users;
                    }
                }
            }
        }

        public async Task AddCoinAsync(string coinName, double initialPrice, string symbol)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 코인을 추가하는 SQL 쿼리
                string query = @"INSERT INTO Coins (CoinName, CurrentPrice, Symbol) 
                         VALUES (@CoinName, @CurrentPrice, @Symbol);";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinName", coinName);
                    command.Parameters.AddWithValue("@CurrentPrice", initialPrice);
                    command.Parameters.AddWithValue("@Symbol", symbol);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeleteCoinAsync(string coinName)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 코인을 삭제하는 SQL 쿼리
                string query = @"DELETE FROM Coins 
                         WHERE CoinName = @CoinName;";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinName", coinName);
                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        Console.WriteLine($"코인 '{coinName}'이(가) 성공적으로 삭제되었습니다.");
                    }
                    else
                    {
                        Console.WriteLine($"코인 '{coinName}'을(를) 찾을 수 없습니다.");
                    }
                }
            }
        }
        public async Task DeleteCoinPriceHistoryAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"DELETE FROM CoinPriceHistory WHERE CoinId = @CoinId";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        public async Task SaveCoinEventAsync(int coinId, int eventCount, bool isSurge, bool isDelisted)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 이벤트가 이미 있는지 확인
                string checkQuery = @"SELECT COUNT(1) FROM CoinEvents WHERE CoinId = @CoinId";
                using (var checkCommand = new SqliteCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@CoinId", coinId);
                    var exists = (long)await checkCommand.ExecuteScalarAsync();

                    // 이벤트가 존재하면 업데이트, 없으면 삽입
                    string query;
                    if (exists > 0)
                    {
                        query = @"UPDATE CoinEvents 
                          SET EventCount = @EventCount, IsSurge = @IsSurge, IsDelisted = @IsDelisted
                          WHERE CoinId = @CoinId";
                    }
                    else
                    {
                        query = @"INSERT INTO CoinEvents (CoinId, EventCount, IsSurge, IsDelisted) 
                          VALUES (@CoinId, @EventCount, @IsSurge, @IsDelisted)";
                    }

                    using (var command = new SqliteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@CoinId", coinId);
                        command.Parameters.AddWithValue("@EventCount", eventCount);
                        command.Parameters.AddWithValue("@IsSurge", isSurge ? 1 : 0);
                        command.Parameters.AddWithValue("@IsDelisted", isDelisted ? 1 : 0);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        public async Task<(int EventCount, bool IsSurge, bool IsDelisted)> GetCoinEventAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT EventCount, IsSurge, IsDelisted FROM CoinEvents WHERE CoinId = @CoinId";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int eventCount = reader.GetInt32(0);
                            bool isSurge = reader.GetInt32(1) == 1;
                            bool isDelisted = reader.GetInt32(2) == 1;
                            return (eventCount, isSurge, isDelisted);
                        }
                        else
                        {
                            // 이벤트가 없으면 null 반환
                            return (0, false, false);
                        }
                    }
                }
            }
        }

        public async Task DeleteCoinEventAsync(int coinId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"DELETE FROM CoinEvents WHERE CoinId = @CoinId";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CoinId", coinId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        // 슬롯머신 기록 삭제
        public async Task<bool> DeleteAllSlotMachineResultsAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string deleteQuery = "DELETE FROM SlotMachineResults;";
                using (var command = new SqliteCommand(deleteQuery, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }

        // 코인 보유 기록 삭제
        public async Task<bool> DeleteAllUserCoinHoldingsAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string deleteQuery = "DELETE FROM UserCoinHoldings;";
                using (var command = new SqliteCommand(deleteQuery, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }

        // 유저 수익 기록 삭제
        public async Task<bool> DeleteAllUserProfitRecordsAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string deleteQuery = "DELETE FROM UserProfit;";
                using (var command = new SqliteCommand(deleteQuery, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }

        // 사용자 데이터에서 Coin 값을 0으로 업데이트
        public async Task<bool> ResetCoinBalancesAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string updateQuery = "UPDATE Users SET Coin = 500;";
                using (var command = new SqliteCommand(updateQuery, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }
        // 사용자 데이터에서 Coin 값을 0으로 업데이트
        public async Task<bool> ResetSlotItemcesAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string updateQuery = "UPDATE Users SET special = 0, ticket = 0;";
                using (var command = new SqliteCommand(updateQuery, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }
        // 사용자 데이터에서 Dollar 값을 0으로 업데이트
        public async Task<bool> ResetDollarBalancesAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string updateQuery = "UPDATE Users SET Dollar = 0;";
                using (var command = new SqliteCommand(updateQuery, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }

        public async Task<bool> ResetDdingBalancesAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string updateQuery = "UPDATE Users SET dding = 0;";
                using (var command = new SqliteCommand(updateQuery, connection))
                {
                    int affectedRows = await command.ExecuteNonQueryAsync();
                    return affectedRows > 0;
                }
            }
        }
    }
}

