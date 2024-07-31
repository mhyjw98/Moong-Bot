using Discord;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


namespace MoongBot.Core.Manager
{
    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager()
        {
            _connectionString = @"Data Source=C:\Users\rhaps\SQLite\moongbot.sqlite";
        }

        public async Task RegisterNotificationAsync(ulong userId, string city, ITextChannel channel)
        {
            Console.WriteLine($"데이터베이스에 등록하는 RegisterNotificationAsync 함수 실행");
            try
            {
                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine($"데이터베이스가 성공적으로 연결되었습니다.");

                    string query = $"INSERT INTO users (userId, city) VALUES ({userId}, '{city}');";
                    SqliteCommand cmd = new SqliteCommand(query, connection);
                    cmd.ExecuteNonQuery();
                    await channel.SendMessageAsync($"<@{userId}> 알림이 {city} 지역으로 등록되었습니다.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while registering notification: {ex.Message}");
            }
        }

        public async Task<Dictionary<ulong, string>> GetNotificationsAsync()
        {
            var notifications = new Dictionary<ulong, string>();

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine("데이터베이스가 성공적으로 연결되었습니다");

                    string query = "SELECT userId, city FROM users;";
                    using (var command = new SqliteCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                ulong userId = (ulong)reader.GetInt64("userId");
                                string city = reader.GetString("city");
                                Console.WriteLine($"탐색된 유저 : {userId}, 도시명 : {city}");
                                notifications[userId] = city;
                            }
                        }
                    }
                    Console.WriteLine("Notifications retrieved successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while retrieving notifications: {ex.Message}");
            }

            return notifications;
        }

        public async Task DeleteNotification(ulong userId, ITextChannel channel)
        {
            Console.WriteLine($"데이터베이스에 등록하는 RegisterNotificationAsync 함수 실행");
            try
            {
                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine($"데이터베이스가 성공적으로 연결되었습니다.");

                    string query = $"DELETE FROM users WHERE userId = {userId};";
                    SqliteCommand cmd = new SqliteCommand(query, connection);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        await channel.SendMessageAsync($"<@{userId}>의 알림이 해제되었습니다.");
                    }
                    else
                    {
                        await channel.SendMessageAsync($"<@{userId}> 알림이 등록되어있지 않습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while registering notification: {ex.Message}");
            }
        }

        public async Task UpdateNotificationAsync(ulong userId, string city, ITextChannel channel)
        {
            Console.WriteLine($"데이터베이스에 업데이트하는 UpdateNotificationAsync 함수 실행");
            try
            {
                using (SqliteConnection connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    Console.WriteLine($"데이터베이스가 성공적으로 연결되었습니다.");

                    string checkQuery = $"SELECT city FROM users WHERE userId = {userId};";
                    SqliteCommand checkCmd = new SqliteCommand(checkQuery, connection);
                    var result = await checkCmd.ExecuteScalarAsync();

                    if (result != null)
                    {
                        string existingCity = result.ToString();
                        if (WeatherManager.IsEqualCity(existingCity, city))
                        {
                            await channel.SendMessageAsync($"<@{userId}> 이미 {city}로 등록되어있습니다.");
                        }
                        else
                        {
                            string updateQuery = $"UPDATE users SET city = '{city}' WHERE userId = {userId};";
                            SqliteCommand updateCmd = new SqliteCommand(updateQuery, connection);
                            await updateCmd.ExecuteNonQueryAsync();

                            await channel.SendMessageAsync($"<@{userId}>의 알림이 {city}로 변경되었습니다.");
                        }
                    }
                    else
                    {
                        await channel.SendMessageAsync($"<@{userId}> 알림이 등록되어있지 않습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while updating notification: {ex.Message}");
            }
        }
    }
 }
