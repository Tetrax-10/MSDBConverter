using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;

class Program
{
    private const string DefaultLegacyInputFolderName = "ToConvert";
    private const string DefaultOutputSubfolderName = "Converted";
    // [custom] changed these 4 variables to handle cli arguments
    private const double DefaultMaxSizeInMB = 7.5;
    private static double MaxSizeInMB = DefaultMaxSizeInMB;
    private const int DefaultMaxDimension = 7500;
    private static int MaxDimension = DefaultMaxDimension;

    static void Main(string[] args)
    {
        string sourceDirectoryToProcess;
        string outputFolderFullPath;
        string operationModeMessage;
        bool useLegacyExitPrompt = false;

        string exeDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        string currentWorkingDirectory = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar);

        string legacyInputPath = Path.Combine(exeDirectory, DefaultLegacyInputFolderName);
        string legacyOutputParentPath = Path.Combine(exeDirectory, DefaultOutputSubfolderName);

        if (args.Length == 0)
        {
            bool isExecutingFromExeDirectory = string.Equals(exeDirectory, currentWorkingDirectory, StringComparison.OrdinalIgnoreCase);

            if (isExecutingFromExeDirectory)
            {
                if (!Directory.Exists(legacyInputPath))
                {
                    Directory.CreateDirectory(legacyInputPath);
                    Console.WriteLine($"'{legacyInputPath}' folder did not exist. It has been created for you.");
                    Console.WriteLine($"Place your images in this folder and run the program again.");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    return;
                }
                else
                {
                    sourceDirectoryToProcess = legacyInputPath;
                    outputFolderFullPath = legacyOutputParentPath;
                    operationModeMessage = $"MSDBConverter :: Legacy mode. Input: '{sourceDirectoryToProcess}'";
                    useLegacyExitPrompt = true;
                }
            }
            else
            {
                sourceDirectoryToProcess = currentWorkingDirectory;
                outputFolderFullPath = Path.Combine(currentWorkingDirectory, DefaultOutputSubfolderName);
                operationModeMessage = $"MSDBConverter :: CLI mode. Input: Current directory '{sourceDirectoryToProcess}'";
            }
        }
        else
        {
            // [custom] assign MaxSizeInMB and MaxDimension from cli argument(s)
            foreach (var arg in args)
            {
                if (arg.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
                {
                    double.TryParse(arg[..^2], out MaxSizeInMB);
                }
                else if (arg.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(arg[..^2], out MaxDimension);
                }
            }

            Console.WriteLine("Command-line arguments detected.");
            Console.WriteLine("Using CLI mode on the current working directory.");
            sourceDirectoryToProcess = currentWorkingDirectory;
            outputFolderFullPath = Path.Combine(currentWorkingDirectory, DefaultOutputSubfolderName);
            operationModeMessage = $"MSDBConverter :: CLI mode (args given). Input: Current directory '{sourceDirectoryToProcess}'";
        }

        try
        {
            RunImageConversionLogic(sourceDirectoryToProcess, outputFolderFullPath, operationModeMessage);
        }
        catch (UnauthorizedAccessException uaex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\n[CRITICAL ERROR] Insufficient permissions.");
            Console.Error.WriteLine($"Could not read from '{sourceDirectoryToProcess}' or write to an output folder within '{Path.GetDirectoryName(outputFolderFullPath)}'.");
            Console.Error.WriteLine($"Details: {uaex.Message}");
            Console.ResetColor();
            Environment.ExitCode = 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\n[CRITICAL ERROR] An unexpected error occurred:");
            Console.Error.WriteLine(ex.Message);
            if (ex.InnerException != null)
                Console.Error.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            Console.ResetColor();
            Environment.ExitCode = 1;
        }
        finally
        {
            if (useLegacyExitPrompt && Environment.ExitCode == 0)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }
    }

    static void RunImageConversionLogic(string sourcePath, string outputConvertedFolderFullPath, string initialMessage)
    {
        Console.WriteLine(initialMessage);

        int fileCountInSource = GetFileCountInDirectory(sourcePath, SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Scanning '{sourcePath}' (contains {fileCountInSource} total items at the top level)...");

        string[] imageExtensions = {
            ".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
            ".heic", ".heif",
            ".psd", ".psb",
            ".svg",
            ".3fr", ".ari", ".arw", ".bay", ".crw", ".cr2", ".cr3", ".cap",
            ".dcs", ".dcr", ".dng", ".drf", ".eip", ".erf", ".fff",
            ".gpr", ".iiq", ".k25", ".kdc", ".mdc", ".mef", ".mos",
            ".mrw", ".nef", ".nrw", ".obm", ".orf", ".pef", ".ptx",
            ".pxn", ".r3d", ".raf", ".raw", ".rwl", ".rw2", ".rwz",
            ".sr2", ".srf", ".srw", ".x3f"
        };

        string[] imageFiles = GetFilesWithExtensions(sourcePath, imageExtensions, SearchOption.TopDirectoryOnly);

        Console.WriteLine($"Found {imageFiles.Length} image files to process:");
        PrintFileNames(imageFiles);

        if (imageFiles.Length > 0)
        {
            if (!Directory.Exists(outputConvertedFolderFullPath))
                Directory.CreateDirectory(outputConvertedFolderFullPath);

            // [custom] Commenting these as I don't want outputs in session folder
            // string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            // string sessionFolderPath = Path.Combine(outputConvertedFolderFullPath, timestamp);
            // Directory.CreateDirectory(sessionFolderPath);

            int totalFiles = imageFiles.Length;
            int processedFiles = 0;
            int successCount = 0;
            int errorCount = 0;
            object consoleLock = new object();
            var progressBar = new SimpleConsoleProgressBar();

            Console.WriteLine("\nConverting...");
            Console.CursorVisible = false;
            if (totalFiles > 0) 
                progressBar.Draw(processedFiles, totalFiles);

            Parallel.ForEach(imageFiles, imageFile =>
            {
                bool success = false;
                string currentFileName = Path.GetFileName(imageFile);
                string warningMessage = null; 
                try
                {
                    // [custom] changed sessionFolderPath to outputConvertedFolderFullPath so the ouputs are generated in the ./Converted folder
                    warningMessage = ConvertToJpgOrCopyOptimized(imageFile, outputConvertedFolderFullPath, MaxSizeInMB, MaxDimension);
                    success = true;
                }
                catch (Exception ex)
                {
                    lock (consoleLock)
                    {
                        Console.Error.WriteLine($"\n[ERROR] Processing {currentFileName}: {ex.Message}");
                        if (ex.InnerException != null)
                            Console.Error.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
                    }
                }
                finally
                {
                    Interlocked.Increment(ref processedFiles);
                    if (success)
                        Interlocked.Increment(ref successCount);
                    else
                        Interlocked.Increment(ref errorCount);

                    lock (consoleLock)
                    {
                        if (!string.IsNullOrEmpty(warningMessage))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Error.WriteLine($"\n{warningMessage}");
                            Console.ResetColor();
                        }
                        progressBar.Draw(processedFiles, totalFiles);
                    }
                }
            });

            Console.CursorVisible = true;
            
            if (totalFiles == 0 && !string.IsNullOrEmpty(progressBar.GetCurrentText()))
                 Console.WriteLine();

            Console.WriteLine($"\n--- Conversion Summary ---");
            Console.WriteLine($"Successfully converted: {successCount} file(s)");
            Console.WriteLine($"Failed to convert:    {errorCount} file(s)");
            // [custom] sessionFolderPath => outputConvertedFolderFullPath
            Console.WriteLine($"Output folder:          '{outputConvertedFolderFullPath}'");
        }
        else
            Console.WriteLine($"No image files with supported extensions found in '{sourcePath}'.");
    }

    public static void PrintFileNames(string[] files)
    {
        if (files.Length == 0)
            return;
        for (int i = 0; i < files.Length; i++)
            Console.WriteLine($"- {Path.GetFileName(files[i])}");
        Console.WriteLine();
    }

    static int GetFileCountInDirectory(string folderPath, SearchOption searchOption)
    {
        if (!Directory.Exists(folderPath))
            return 0;
        return Directory.GetFiles(folderPath, "*.*", searchOption).Length;
    }

    static string[] GetFilesWithExtensions(string folderPath, string[] extensions, SearchOption searchOption)
    {
        if (!Directory.Exists(folderPath))
            return Array.Empty<string>();
        return Directory.GetFiles(folderPath, "*.*", searchOption)
                        .Where(file => extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToArray();
    }
    
    // re-wrote ConvertToJpgOrCopyOptimized so it uses two segment binary search to find correct quality in less number of steps than normal linear search
    static string? ConvertToJpgOrCopyOptimized(string imagePath, string outputFolderPath, double targetMaxSizeMB, int targetMaxDimension)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imagePath);
        string outputFileName = Path.Combine(outputFolderPath, fileNameWithoutExtension + ".jpg");

        // Convert target max size from MB to bytes
        long targetMaxSizeBytes = (long)(targetMaxSizeMB * 1024 * 1024);

        // Create a temporary file used to probe image size without overwriting the final output yet
        string tempFile = Path.GetTempFileName();

        FileInfo originalFileInfo = new FileInfo(imagePath);

        // Default to minimum quality; updated during binary search
        int finalQuality = 50;

        bool foundValidQuality = false;

        try
        {
            using (var image = new MagickImage(imagePath))
            {
                // Automatically rotate the image based on its EXIF orientation
                image.AutoOrient();

                // Check if original is already a JPG and meets size & dimension requirements
                bool isOriginalJpg = image.Format == MagickFormat.Jpg || image.Format == MagickFormat.Jpeg;
                if (isOriginalJpg &&
                    originalFileInfo.Length <= targetMaxSizeBytes &&
                    image.Width <= targetMaxDimension &&
                    image.Height <= targetMaxDimension)
                {
                    // If so, simply copy the original image
                    File.Copy(imagePath, outputFileName, true);
                    return null;
                }

                // Resize the image if its width or height exceeds target
                if (image.Width > targetMaxDimension || image.Height > targetMaxDimension)
                {
                    image.Resize(new MagickGeometry((uint)targetMaxDimension, (uint)targetMaxDimension)
                    {
                        IgnoreAspectRatio = false // Keep aspect ratio intact
                    });
                }

                // Always convert final format to JPG
                image.Format = MagickFormat.Jpg;

                // Try different quality ranges, starting from high to low inorder to find the best quality under the size limit
                var ranges = new List<(int Low, int High)>
                {
                    (91, 100), // Try best quality first (most likely will fall under size limit)
                    (50, 90)   // Acceptable quality
                };

                // Loop through each quality range
                foreach (var (lowStart, highStart) in ranges)
                {
                    int low = lowStart;
                    int high = highStart;

                    // Perform binary search to find the highest quality under the size limit
                    while (low <= high)
                    {
                        int mid = (low + high) / 2;

                        image.Quality = (uint)mid;

                        // Save a temporary copy with this quality setting
                        image.Write(tempFile);

                        long tempSize = new FileInfo(tempFile).Length;

                        if (tempSize <= targetMaxSizeBytes)
                        {
                            // If file is under target size, try a higher quality
                            finalQuality = mid;
                            foundValidQuality = true;
                            low = mid + 1;
                        }
                        else
                        {
                            // If file exceeds target, try a lower quality
                            high = mid - 1;
                        }
                    }

                    // If any valid quality found in this range, skip lower ranges
                    if (foundValidQuality)
                        break;
                }

                // Write final image to output using the best quality found
                image.Quality = (uint)finalQuality;
                image.Write(outputFileName);

                // Check final file size
                long finalSize = new FileInfo(outputFileName).Length;
                if (!foundValidQuality || finalSize > targetMaxSizeBytes)
                {
                    // Warn if we couldn't meet the size requirement even at minimum quality
                    return $"[WARN] Best achievable quality for {Path.GetFileName(imagePath)} is {finalQuality} (size: {finalSize / (1024.0 * 1024):F2}MB)";
                }

                return null;
            }
        }
        catch (MagickException ex)
        {
            // Delete incomplete output if a Magick.NET error occurred
            if (File.Exists(outputFileName)) File.Delete(outputFileName);
            throw new Exception($"Processing failed: {ex.Message}", ex);
        }
        finally
        {
            // Always clean up temporary probe file
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}