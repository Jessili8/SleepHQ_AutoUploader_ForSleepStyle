using Newtonsoft.Json;
using SleepHQ_AutoUploader_ForSleepStyle;
using System.Security.Cryptography;

internal class Program
{
    public static string? DeviceId { get; set; }
    public static string? ClientId { get; set; }
    public static string? ClientSecret { get; set; }
    public static string? SubPath { get; set; }
    public static string? DirPath { get; set; }

    private static void Main()
    {
        EnvironmentVariables();
        CheckUSBDrive();
    }

    private static void CheckUSBDrive()
    {
        DriveInfo[] drives = DriveInfo.GetDrives();
        List<DriveInfo> usbDrives = drives.Where(drive => drive.DriveType == DriveType.Removable && drive.IsReady).ToList();

        if (usbDrives.Count == 0)
        {
            Console.WriteLine("No external Drive found. ");
            return;
        }

        foreach (DriveInfo drive in usbDrives)
        {
            Console.WriteLine($"USB Drive found: {drive.Name}");
            string folderPath = Path.Combine(drive.Name, "FPHCARE");

            if (Directory.Exists(folderPath))
            {
                Console.WriteLine($"FPHCARE folder found in：{folderPath}");
                Dictionary<string, List<string>> CollectedFiles = CollectFiles(folderPath);
                List<Dictionary<string, string>> orderedImportFiles = CollectedFiles["path"].Select((t, i) => new Dictionary<string, string>
                {
                    { "path", t },
                    { "name", CollectedFiles["name"][i] },
                    { "content_hash", CollectedFiles["content_hash"][i] }
                }).ToList();
                string finalImportFilesJson = JsonConvert.SerializeObject(orderedImportFiles);
                string authorization = SleepHQClientService.GetAccessToken(ClientId, ClientSecret);
                string teamId = SleepHQClientService.GetTeamId(authorization);
                string importId = SleepHQClientService.ReserveImportId(teamId, authorization);
                Console.WriteLine($"Reserve Team ID: {importId} successfully. ");
                SleepHQClientService.UploadFiles(importId, authorization, orderedImportFiles, DirPath);
                SleepHQClientService.ProcessImportedFiles(importId, authorization);
                Console.WriteLine($"Processing Files...");
                Task.Delay(30000);
                SleepHQClientService.CheckImportedFiles(importId, authorization);
            }
            else
            {
                Console.WriteLine($"We cannot found FPHCARE folder in {drive.Name}.");
            }
        }
    }

    static Dictionary<string, List<string>> CollectFiles(string folderPath)
    {
        IEnumerable<FileInfo> allFiles = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Select(f => new FileInfo(f));

        Dictionary<string, List<string>> importFiles = new Dictionary<string, List<string>>
            {
                { "path", [] },
                { "name", [] },
                { "content_hash", [] }
            };

        foreach (var file in allFiles)
        {
            string finalPath = Path.GetFullPath(file.DirectoryName);
            importFiles["path"].Add(finalPath);
            importFiles["name"].Add(file.Name);
            importFiles["content_hash"].Add(CalculateMD5(file.FullName));
        }

        return importFiles;
    }

    private static string CalculateMD5(string filename)
    {
        using MD5 md5 = MD5.Create();
        using FileStream stream = File.OpenRead(filename);
        byte[] hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static void EnvironmentVariables()
    {
        ClientId = Environment.GetEnvironmentVariable("CLIENT_ID");
        ClientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
    }
}