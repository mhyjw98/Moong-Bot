
using System.Text;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

using Newtonsoft.Json;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using System.IO;
using Discord;

namespace MoongBot.Core.Manager
{
    public class GmailManager
    {
        private static string[] Scopes = { GmailService.Scope.GmailReadonly };
        private static string ApplicationName = "MoongBot";
        private GmailService _service;
        private string _lastEmailId;
        private string _lastEmailFilePath = Path.Combine("jsonFiles", "lastEmailId.json");

        public GmailManager()
        {
            InitializeService().Wait();
        }

        private async Task InitializeService()
        {
            try
            {
                UserCredential credential;

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    installed = new
                    {
                        client_id = ConfigManager.Config.GmailClientId,
                        client_secret = ConfigManager.Config.GmailClientSecret,
                        redirect_uris = new[] { "urn:ietf:wg:oauth:2.0:oob", "http://localhost" },
                        auth_uri = "https://accounts.google.com/o/oauth2/auth",
                        token_uri = "https://oauth2.googleapis.com/token"
                    }
                }))))
                {
                    string credPath = "token.json";
                    credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        Scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true));
                    Console.WriteLine("Credential file saved to: " + credPath);
                }

                _service = new GmailService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : InitializeService 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }

        public async Task<string> GetMessageContent(Message message)
        {
            try
            {
                Console.WriteLine($"GetMessageContent 실행");
                string from = message.Payload.Headers.FirstOrDefault(h => h.Name == "From")?.Value;
                string subject = message.Payload.Headers.FirstOrDefault(h => h.Name == "Subject")?.Value;
                string body = message.Snippet;

                return $"보낸 사람: {from}\n메일 제목: {subject}\n\n{body}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetMessageContent 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return "GetMessageContent에서 메일 내용을 읽다 오류 발생"; 
            }      
        }

        public async Task<List<(string, string)>> GetMessagesWithScreenshotsAsync()
        {
            var result = new List<(string, string)>();

            try
            {
                UsersResource.MessagesResource.ListRequest request = _service.Users.Messages.List("me");
                request.Q = "from:(tobyfox) OR from:(noreply@fangamer.com) OR subject:(undertale) OR subject:(deltarune)";

                Console.WriteLine($"Gmail query: {request.Q}");
                ListMessagesResponse response;

                try
                {
                    response = await request.ExecuteAsync();
                }
                catch (Google.GoogleApiException ex) when (ex.Message.Contains("invalid_grant"))
                {
                    Console.WriteLine("토큰이 만료되어 토큰 갱신요청 중...");
                    await InitializeService();
                    request = _service.Users.Messages.List("me");
                    response = await request.ExecuteAsync();
                    Console.WriteLine("토큰 갱신 후 Gmail API 요청 성공");
                }

                IList<Message> messages = response.Messages;
                Console.WriteLine($"Messages found: {messages?.Count ?? 0}");

                if (messages != null && messages.Count > 0)
                {
                    foreach (var messageItem in messages)
                    {
                        if (_lastEmailId == null || messageItem.Id != _lastEmailId)
                        {
                            var emailInfoReq = _service.Users.Messages.Get("me", messageItem.Id);
                            var emailInfoResponse = await emailInfoReq.ExecuteAsync();

                            if (emailInfoResponse != null)
                            {
                                string headerContent = await GetMessageContent(emailInfoResponse);
                                string screenshotPath = await CaptureScreenshotOfEmail(emailInfoResponse);

                                if (!string.IsNullOrEmpty(screenshotPath))
                                {
                                    Console.WriteLine($"Screenshot saved at: {screenshotPath}");
                                    result.Add((headerContent, screenshotPath));
                                }
                                else
                                {
                                    Console.WriteLine("Failed to capture screenshot");
                                }
                            }
                        }
                        else
                        {
                            break; // 이미 확인한 메일에 도달하면 더 이상 확인하지 않음
                        }
                    }

                    if (messages.Count > 0)
                    {
                        _lastEmailId = messages.First().Id;
                        SaveLastEmailId(_lastEmailId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GetMessagesWithScreenshotsAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }

            return result;
        }

        private async Task<string> CaptureScreenshotOfEmail(Message message)
        {
            try
            {
                string htmlContent = null;

                foreach (var part in message.Payload.Parts)
                {
                    if (part.MimeType == "text/html")
                    {
                        var data = part.Body.Data;
                        var decodedBytes = Convert.FromBase64String(data.Replace('-', '+').Replace('_', '/'));
                        htmlContent = Encoding.UTF8.GetString(decodedBytes);
                        break;
                    }
                }

                if (htmlContent == null) return null;

                var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }

                string screenshotPath = Path.Combine(tempDir, $"{Guid.NewGuid()}.png");

                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArgument("--headless");
                chromeOptions.AddArgument("--disable-gpu");
                chromeOptions.AddArgument("--window-size=1280,1024");

                new DriverManager().SetUpDriver(new ChromeConfig());

                using (var driver = new ChromeDriver(chromeOptions))
                {
                    driver.Navigate().GoToUrl("data:text/html;charset=utf-8," + Uri.EscapeDataString(htmlContent));

                    // 자바스크립트를 사용하여 페이지 전체 높이를 계산
                    var totalHeight = (long)((IJavaScriptExecutor)driver).ExecuteScript(
                        "return Math.max(document.body.scrollHeight, document.documentElement.scrollHeight);"
                    );

                    // 페이지의 크기를 뷰포트 크기로 조정
                    ((IJavaScriptExecutor)driver).ExecuteScript($"document.body.style.height = '{totalHeight}px'");

                    // 창 크기를 페이지 전체 높이에 맞게 조정
                    driver.Manage().Window.Size = new System.Drawing.Size(1280, (int)totalHeight);

                    // 스크린샷 찍기
                    var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
                    File.WriteAllBytes(screenshotPath, screenshot.AsByteArray);
                }

                return screenshotPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : CaptureScreenshotOfEmail 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return null; 
            }           
        }

        private void SaveLastEmailId(string lastEmailId)
        {
            try
            {
                var data = new Dictionary<string, string>
            {
                { "LastEmailId", lastEmailId }
            };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_lastEmailFilePath, json);
                Console.WriteLine("마지막 이메일 ID 저장됨: " + lastEmailId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("마지막 이메일 ID 저장 중 오류 발생: " + ex.Message);
            }
        }

        public void LoadLastEmailId()
        {
            if (File.Exists(_lastEmailFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_lastEmailFilePath);
                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (data != null && data.ContainsKey("LastEmailId"))
                    {
                        _lastEmailId = data["LastEmailId"];
                        Console.WriteLine("마지막 이메일 ID 로드됨: " + _lastEmailId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("마지막 이메일 ID 로드 중 오류 발생: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("이전에 저장된 이메일 ID를 찾을 수 없습니다. 새로운 검색을 시작합니다.");
            }
        }       
    }
}
