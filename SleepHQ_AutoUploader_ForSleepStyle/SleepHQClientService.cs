using Newtonsoft.Json.Linq;

namespace SleepHQ_AutoUploader_ForSleepStyle
{
    public class SleepHQClientService
    {
        public static string GetAccessToken(string clientId, string clientSecret)
        {
            string url = "https://sleephq.com/oauth/token";
            Dictionary<string, string> payload = new()
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "password" },
                { "scope", "read write" }
            };

            using HttpClient httpClient = new();
            try
            {
                var response = httpClient.PostAsync(url, new FormUrlEncodedContent(payload)).Result;
                response.EnsureSuccessStatusCode();
                Console.WriteLine("Get access token successfully.");

                var responseBody = response.Content.ReadAsStringAsync().Result;
                var jsonResponse = JObject.Parse(responseBody);
                return "Bearer " + jsonResponse["access_token"].ToString();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Access Denied：{e.Message}");
                Environment.Exit(1);
                return null;
            }
        }

        public static string GetTeamId(string authorization)
        {
            string url = "https://sleephq.com/api/v1/me";
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                HttpResponseMessage response = httpClient.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();

                string responseBody = response.Content.ReadAsStringAsync().Result;
                JObject jsonResponse = JObject.Parse(responseBody);
                return jsonResponse["data"]["current_team_id"].ToString();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Failed to retrieve TeamId：{e.Message}");
                Environment.Exit(1);
                return null;
            }
        }

        public static string ReserveImportId(string teamId, string authorization)
        {
            string url = $"https://sleephq.com/api/v1/teams/{teamId}/imports";
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var payload = new Dictionary<string, string>
            {
                { "programatic", "false" }
            };

            try
            {
                var response = httpClient.PostAsync(url, new FormUrlEncodedContent(payload)).Result;
                response.EnsureSuccessStatusCode();

                var responseBody = response.Content.ReadAsStringAsync().Result;
                var jsonResponse = JObject.Parse(responseBody);
                return jsonResponse["data"]["id"].ToString();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"預留導入 ID 失敗：{e.Message}");
                Environment.Exit(1);
                return null;
            }
        }

        public static void UploadFiles(string importId, string authorization, List<Dictionary<string, string>> finalImportFilesList, string dirPath)
        {
            string url = $"https://sleephq.com/api/v1/imports/{importId}/files";
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);

            foreach (var value in finalImportFilesList)
            {
                string osPath = value["path"].Trim();
                string filePath = Path.Combine(osPath, value["name"]);

                using (var content = new MultipartFormDataContent())
                {
                    ByteArrayContent fileContent = new(File.ReadAllBytes(filePath));
                    content.Add(fileContent, "file", value["name"]);

                    foreach (var kvp in value)
                    {
                        content.Add(new StringContent(kvp.Value), kvp.Key);
                    }

                    try
                    {
                        var response = httpClient.PostAsync(url, content).Result;
                        response.EnsureSuccessStatusCode();
                        Console.WriteLine($"File {value["name"]} have been imported to SleepHQ");
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine($"Upload {value["name"]} failed：{e.Message}");
                        Environment.Exit(1);
                    }
                }

                Thread.Sleep(1500);
            }
        }

        public static void ProcessImportedFiles(string importId, string authorization)
        {
            string url = $"https://sleephq.com/api/v1/imports/{importId}/process_files";
            using HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var response = httpClient.PostAsync(url, null).Result;
                response.EnsureSuccessStatusCode();
                Console.WriteLine($"File have been processed by SleepHQ with Import ID：{importId}");
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Failed to upload the file：{e.Message}");
                Console.WriteLine($"You can try to import the files with this link：{url}");
                Environment.Exit(1);
            }
        }

        public static void CheckImportedFiles(string importId, string authorization)
        {
            string url = $"https://sleephq.com/api/v1/imports/{importId}";
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", authorization);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var response = httpClient.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();

                var responseBody = response.Content.ReadAsStringAsync().Result;
                var jsonResponse = JObject.Parse(responseBody);
                string failedReason = jsonResponse["data"]["attributes"]["failed_reason"].ToString();

                if (!string.IsNullOrEmpty(failedReason))
                {
                    Console.WriteLine($"Failed to process：{failedReason}");
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Failed to check the file：{e.Message}");
                Console.WriteLine($"You can try to import the files with this link：{url}");
                Environment.Exit(1);
            }
        }
    }
}