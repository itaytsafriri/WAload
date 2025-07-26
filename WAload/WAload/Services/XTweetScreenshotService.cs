using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WAload.Services
{
    public class XTweetScreenshotService
    {
        private readonly string _nodeScriptPath;
        private readonly string _tempFolder;
        private readonly object _processLock = new object();
        private string _downloadFolder = string.Empty;

        public XTweetScreenshotService()
        {
            var nodeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node");
            _nodeScriptPath = Path.Combine(nodeDir, "x_tweet_screenshot.js");
            _tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WAload", ".temp");
            
            if (!Directory.Exists(_tempFolder))
            {
                Directory.CreateDirectory(_tempFolder);
                // Make the temp folder hidden
                File.SetAttributes(_tempFolder, File.GetAttributes(_tempFolder) | FileAttributes.Hidden);
            }
        }

        public bool IsTweetUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // Match X/Twitter URLs
            var patterns = new[]
            {
                @"https?://(?:www\.)?(?:twitter\.com|x\.com)/\w+/status/\d+",
                @"https?://(?:www\.)?(?:twitter\.com|x\.com)/i/status/\d+"
            };

            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }

        public async Task<string?> TakeTweetScreenshotAsync(string tweetUrl, string senderName, DateTime timestamp)
        {
            Process? nodeProcess = null;
            try
            {
                System.Diagnostics.Debug.WriteLine($"Starting X tweet screenshot for URL: {tweetUrl}");

                // Create the Node.js script if it doesn't exist
                await CreateNodeScriptAsync();

                // Generate unique filename to avoid conflicts
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var fileName = $"{senderName}_{timestamp:yyyyMMdd_HHmmss}_{uniqueId}.png";
                var tempFilePath = Path.Combine(_tempFolder, fileName);
                var finalFilePath = Path.Combine(GetDownloadFolder(), fileName);

                // Start Node.js process
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node", "node.exe"),
                    Arguments = $"\"{_nodeScriptPath}\" \"{tweetUrl}\" \"{tempFilePath}\"",
                    WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Node"),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                nodeProcess = new Process { StartInfo = startInfo };
                nodeProcess.Start();

                // Wait for completion with timeout
                var completed = await Task.Run(() => nodeProcess.WaitForExit(30000)); // 30 second timeout

                if (!completed)
                {
                    System.Diagnostics.Debug.WriteLine($"X tweet screenshot process timed out for URL: {tweetUrl}");
                    nodeProcess?.Kill();
                    return null;
                }

                var exitCode = nodeProcess.ExitCode;
                var output = await nodeProcess.StandardOutput.ReadToEndAsync();
                var error = await nodeProcess.StandardError.ReadToEndAsync();

                System.Diagnostics.Debug.WriteLine($"X tweet screenshot process output for {tweetUrl}: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    System.Diagnostics.Debug.WriteLine($"X tweet screenshot process error for {tweetUrl}: {error}");
                }

                if (exitCode == 0 && File.Exists(tempFilePath))
                {
                    // Move file to download folder
                    if (File.Exists(finalFilePath))
                    {
                        File.Delete(finalFilePath);
                    }
                    
                    File.Move(tempFilePath, finalFilePath);
                    System.Diagnostics.Debug.WriteLine($"X tweet screenshot saved: {finalFilePath}");
                    return finalFilePath;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"X tweet screenshot failed with exit code: {exitCode} for URL: {tweetUrl}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error taking X tweet screenshot for {tweetUrl}: {ex.Message}");
                return null;
            }
            finally
            {
                nodeProcess?.Dispose();
            }
        }

        private async Task CreateNodeScriptAsync()
        {
            if (File.Exists(_nodeScriptPath))
                return;

            var scriptContent = @"
const puppeteer = require('puppeteer');

async function takeTweetScreenshot(tweetUrl, outputPath) {
    let browser;
    try {
        console.log('Starting browser...');
        browser = await puppeteer.launch({
            headless: true,
            args: [
                '--no-sandbox',
                '--disable-setuid-sandbox',
                '--disable-dev-shm-usage',
                '--disable-accelerated-2d-canvas',
                '--no-first-run',
                '--no-zygote',
                '--disable-gpu'
            ]
        });

        const page = await browser.newPage();
        
        // Set viewport for better screenshot quality
        await page.setViewport({
            width: 1200,
            height: 800,
            deviceScaleFactor: 2
        });

        console.log('Navigating to tweet...');
        await page.goto(tweetUrl, { 
            waitUntil: 'networkidle2',
            timeout: 30000 
        });

        // Wait for tweet content to load
        await page.waitForTimeout(3000);

        // Try to find the tweet content
        const tweetSelectors = [
            'article[data-testid=""tweet""]',
            '[data-testid=""tweet""]',
            'article',
            '.tweet'
        ];

        let tweetElement = null;
        for (const selector of tweetSelectors) {
            try {
                tweetElement = await page.$(selector);
                if (tweetElement) {
                    console.log('Found tweet element with selector:', selector);
                    break;
                }
            } catch (e) {
                console.log('Selector not found:', selector);
            }
        }

        if (tweetElement) {
            console.log('Found tweet element, taking cropped screenshot...');
            
            // Get the bounding box of the tweet element
            const boundingBox = await tweetElement.boundingBox();
            
            // Calculate crop dimensions to remove the blue banner
            // The banner is typically about 80-120px tall at the bottom
            const bannerHeight = 120; // Increased height to ensure we crop enough
            const croppedHeight = Math.max(boundingBox.height - bannerHeight, 200); // Ensure minimum height
            
            console.log(`Original tweet height: ${boundingBox.height}, Cropped height: ${croppedHeight}`);
            
            // Take screenshot of the tweet element with cropping
            await tweetElement.screenshot({
                path: outputPath,
                type: 'png',
                clip: {
                    x: boundingBox.x,
                    y: boundingBox.y,
                    width: boundingBox.width,
                    height: croppedHeight
                }
            });
        } else {
            // Fallback: take screenshot of the whole page with cropping
            console.log('Tweet element not found, taking full page screenshot with cropping');
            
            // Get page dimensions for fallback cropping
            const pageHeight = await page.evaluate(() => document.body.scrollHeight);
            const viewportHeight = await page.evaluate(() => window.innerHeight);
            
            // Crop the bottom portion to remove the blue banner
            const bannerHeight = 120;
            const croppedHeight = Math.max(pageHeight - bannerHeight, viewportHeight);
            
            console.log(`Fallback - Page height: ${pageHeight}, Cropped height: ${croppedHeight}`);
            
            await page.screenshot({
                path: outputPath,
                type: 'png',
                clip: {
                    x: 0,
                    y: 0,
                    width: await page.evaluate(() => window.innerWidth),
                    height: croppedHeight
                }
            });
        }

        console.log('Screenshot taken successfully');
        process.exit(0);
    } catch (error) {
        console.error('Error taking screenshot:', error.message);
        process.exit(1);
    } finally {
        if (browser) {
            await browser.close();
        }
    }
}

// Get command line arguments
const tweetUrl = process.argv[2];
const outputPath = process.argv[3];

if (!tweetUrl || !outputPath) {
    console.error('Usage: node x_tweet_screenshot.js <tweet_url> <output_path>');
    process.exit(1);
}

takeTweetScreenshot(tweetUrl, outputPath);
";

            await File.WriteAllTextAsync(_nodeScriptPath, scriptContent);
            System.Diagnostics.Debug.WriteLine($"Created X tweet screenshot script: {_nodeScriptPath}");
        }

        private string GetDownloadFolder()
        {
            if (!string.IsNullOrEmpty(_downloadFolder) && Directory.Exists(_downloadFolder))
            {
                return _downloadFolder;
            }
            
            // Fallback to default location
            var defaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "WAload");
            
            if (!Directory.Exists(defaultFolder))
            {
                Directory.CreateDirectory(defaultFolder);
            }
            
            return defaultFolder;
        }

        public void SetDownloadFolder(string folder)
        {
            _downloadFolder = folder;
        }
    }
} 