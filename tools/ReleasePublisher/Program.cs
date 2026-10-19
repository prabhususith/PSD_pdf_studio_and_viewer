using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ReleasePublisher
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("    PSD PDF STUDIO - GITHUB RELEASE AUTOMATION ENGINE     ");
            Console.WriteLine("==========================================================");

            // 1. Get credentials from git credential-manager
            string token = "";
            string username = "";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "credential-manager get",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi)!;
                proc.StandardInput.WriteLine("protocol=https");
                proc.StandardInput.WriteLine("host=github.com");
                proc.StandardInput.WriteLine("");
                proc.StandardInput.Close();

                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                using var reader = new StringReader(output);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("username="))
                        username = line.Substring("username=".Length).Trim();
                    else if (line.StartsWith("password="))
                        token = line.Substring("password=".Length).Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving git credentials: {ex.Message}");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Failed to get GitHub token from git credential manager.");
                return 1;
            }

            Console.WriteLine($"GitHub Authenticated as: {username}");

            string repoOwner = username;
            string repoName = "PSD_pdf_studio_and_viewer";
            string tagName = "v3.0.0";
            string releaseTitle = "PSD PDF Studio & Viewer v3.0.0 (Initial Release)";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30);
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PSD-Studio-Publisher", "1.0"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            // 2. Get or create release
            long releaseId = 0;
            string releaseHtmlUrl = "";
            string getReleaseUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/tags/{tagName}";

            try
            {
                var getResp = await httpClient.GetAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/releases/tags/{tagName}");
                if (getResp.IsSuccessStatusCode)
                {
                    var jsonStr = await getResp.Content.ReadAsStringAsync();
                    var node = JsonNode.Parse(jsonStr);
                    releaseId = node!["id"]!.GetValue<long>();
                    releaseHtmlUrl = node["html_url"]!.GetValue<string>();
                    Console.WriteLine($"Found existing release '{tagName}' (ID: {releaseId}) at {releaseHtmlUrl}");
                }
            }
            catch { }

            if (releaseId == 0)
            {
                Console.WriteLine($"Creating new release '{tagName}'...");
                var releaseBody = new StringBuilder();
                releaseBody.AppendLine("## PSD PDF Studio & Viewer Enterprise v3.0.0 (Initial Release)");
                releaseBody.AppendLine();
                releaseBody.AppendLine("Welcome to the official **v3.0.0 Initial Release** of **PSD PDF Studio & Viewer**!");
                releaseBody.AppendLine();
                releaseBody.AppendLine("### Key Workstation Capabilities");
                releaseBody.AppendLine("- **High-Performance Multi-Page Vector Canvas**: Seamless hardware-accelerated continuous scrolling across documents of any length.");
                releaseBody.AppendLine("- **Microsoft Word-Compatible Document Editor**: Full WYSIWYG rich text formatting, case converter, enterprise templates, and readability scoring.");
                releaseBody.AppendLine("- **Cryptographic E-Signature & PKI Certification Studio**: Verifiable SHA-256 digital seals with audit timestamps.");
                releaseBody.AppendLine("- **Air-Gapped Sovereign Offline AI Copilot**: 100% private semantic search and Q&A powered by local vector embeddings.");
                releaseBody.AppendLine("- **10-Format Document Pipeline**: PDF, DOCX, XLSX, PPTX, EPUB, MOBI, RTF, ODT, HTML, and Markdown.");
                releaseBody.AppendLine("- **Modern Windows Fluent & Glass Themes**: Frameless native titlebar, Acrylic/Mica effects, and Fullscreen presentation mode.");
                releaseBody.AppendLine();
                releaseBody.AppendLine("### Distribution Packages");
                releaseBody.AppendLine("- `PSD-PDF-Studio-Setup.exe`: Windows installer with desktop & start menu shortcuts.");
                releaseBody.AppendLine("- `PSD-PDF-Studio-Portable.zip`: Zero-install portable zip archive.");
                releaseBody.AppendLine("- `PSD-PDF-Studio-Enterprise.msi`: Silent IT enterprise installer (`msiexec /i PSD-PDF-Studio-Enterprise.msi /qn`).");
                releaseBody.AppendLine("- `psd_pdf.exe`: Standalone direct executable.");
                releaseBody.AppendLine("- `PSD-PDF-Studio-Release-universal.apk`: Android universal application package.");
                releaseBody.AppendLine("- `PSD_PDF_Studio_Hardware_Requirements.pdf`: Technical hardware specification sheet.");
                releaseBody.AppendLine("- `PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf`: Complete user manual & help guide.");
                releaseBody.AppendLine();
                releaseBody.AppendLine("Licensed under the **MIT License**.");

                var payloadObj = new
                {
                    tag_name = tagName,
                    target_commitish = "main",
                    name = releaseTitle,
                    body = releaseBody.ToString(),
                    draft = false,
                    prerelease = false
                };

                string payloadJson = JsonSerializer.Serialize(payloadObj);
                using var postContent = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                var createResp = await httpClient.PostAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/releases", postContent);
                string respContent = await createResp.Content.ReadAsStringAsync();

                if (!createResp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to create release: {createResp.StatusCode}\n{respContent}");
                    return 1;
                }

                var createNode = JsonNode.Parse(respContent);
                releaseId = createNode!["id"]!.GetValue<long>();
                releaseHtmlUrl = createNode["html_url"]!.GetValue<string>();
                Console.WriteLine($"Successfully created release '{tagName}' (ID: {releaseId}) at {releaseHtmlUrl}");
            }

            // 3. Query existing assets on this release
            var existingAssetsResp = await httpClient.GetAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/releases/{releaseId}/assets");
            var existingAssets = new Dictionary<string, long>();
            if (existingAssetsResp.IsSuccessStatusCode)
            {
                var assetsArr = JsonNode.Parse(await existingAssetsResp.Content.ReadAsStringAsync()) as JsonArray;
                if (assetsArr != null)
                {
                    foreach (var asset in assetsArr)
                    {
                        string name = asset!["name"]!.GetValue<string>();
                        long id = asset["id"]!.GetValue<long>();
                        existingAssets[name] = id;
                    }
                }
            }

            // 4. Asset files to upload
            string baseDir = @"P:\prabhu\projects\git\Omni PDF Desktop";
            string[] filesToUpload = new[]
            {
                Path.Combine(baseDir, @"build\dist\PSD_PDF_Studio_Hardware_Requirements.pdf"),
                Path.Combine(baseDir, @"build\dist\PSD_PDF_Studio_User_Manual_and_Help_Guide.pdf"),
                Path.Combine(baseDir, @"build\dist\PSD-PDF-Studio-Release-universal.apk"),
                Path.Combine(baseDir, @"build\dist\PSD-PDF-Studio-Enterprise.msi"),
                Path.Combine(baseDir, @"build\dist\psd_pdf.exe"),
                Path.Combine(baseDir, @"build\dist\PSD-PDF-Studio-Setup.exe"),
                Path.Combine(baseDir, @"build\dist\PSD-PDF-Studio-Portable.zip")
            };

            for (int i = 0; i < filesToUpload.Length; i++)
            {
                string filePath = filesToUpload[i];
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[WARN] File not found: {filePath} - skipping.");
                    continue;
                }

                string fileName = Path.GetFileName(filePath);
                var fi = new FileInfo(filePath);
                double mb = Math.Round(fi.Length / (1024.0 * 1024.0), 2);

                if (existingAssets.TryGetValue(fileName, out long existingId))
                {
                    Console.WriteLine($"Asset '{fileName}' already exists (ID: {existingId}). Removing old asset...");
                    await httpClient.DeleteAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/releases/assets/{existingId}");
                }

                Console.WriteLine($"[{i + 1}/{filesToUpload.Length}] Uploading {fileName} ({mb} MB)...");
                string uploadUrl = $"https://uploads.github.com/repos/{repoOwner}/{repoName}/releases/{releaseId}/assets?name={Uri.EscapeDataString(fileName)}";

                using var fileStream = File.OpenRead(filePath);
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var uploadResp = await httpClient.PostAsync(uploadUrl, streamContent);
                if (uploadResp.IsSuccessStatusCode)
                {
                    var upNode = JsonNode.Parse(await uploadResp.Content.ReadAsStringAsync());
                    string downloadUrl = upNode!["browser_download_url"]!.GetValue<string>();
                    Console.WriteLine($" -> Uploaded: {downloadUrl}");
                }
                else
                {
                    string err = await uploadResp.Content.ReadAsStringAsync();
                    Console.WriteLine($" -> Upload FAILED: {uploadResp.StatusCode} - {err}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("==========================================================");
            Console.WriteLine("    RELEASE AND ALL ASSETS PUBLISHED SUCCESSFULLY!       ");
            Console.WriteLine($"    Release URL: {releaseHtmlUrl}");
            Console.WriteLine("==========================================================");

            return 0;
        }
    }
}
