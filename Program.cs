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
    
    static string? ConvertToJpgOrCopyOptimized(string imagePath, string outputFolderPath, double targetMaxSizeMB, int targetMaxDimension)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imagePath);
        string outputFileName = Path.Combine(outputFolderPath, fileNameWithoutExtension + ".jpg");
        long targetMaxSizeBytes = (long)(targetMaxSizeMB * 1024 * 1024);

        FileInfo originalFileInfo = new FileInfo(imagePath);

        try
        {
            using (var image = new MagickImage(imagePath))
            {
                image.AutoOrient();
                string originalExtension = Path.GetExtension(imagePath).ToLowerInvariant();
                bool isOriginalJpg = (image.Format == MagickFormat.Jpg || image.Format == MagickFormat.Jpeg);

                if (isOriginalJpg &&
                    originalFileInfo.Length <= targetMaxSizeBytes &&
                    image.Width <= targetMaxDimension &&
                    image.Height <= targetMaxDimension)
                {
                    File.Copy(imagePath, outputFileName, true);
                    return null;
                }

                if (image.Width > targetMaxDimension || image.Height > targetMaxDimension)
                {
                    image.Resize(new MagickGeometry((uint)targetMaxDimension, (uint)targetMaxDimension)
                    {
                        IgnoreAspectRatio = false
                    });
                }

                image.Format = MagickFormat.Jpg;

                const int minQuality = 50;

                for (int currentQuality = 100; currentQuality >= minQuality; currentQuality--)
                {
                    image.Quality = (uint)currentQuality;
                    image.Write(outputFileName);

                    FileInfo outputFileInfo = new FileInfo(outputFileName);
                    if (outputFileInfo.Length <= targetMaxSizeBytes)
                        return null;
                }

                FileInfo finalAttemptFileInfo = new FileInfo(outputFileName);
                string warningMessage =
                    $"[WARN] File '{Path.GetFileName(imagePath)}': Could not achieve target size {targetMaxSizeMB}MB even at quality {minQuality}. " +
                    $"File saved with quality {minQuality}, final size: {(double)finalAttemptFileInfo.Length / (1024 * 1024):F2}MB.";
                return warningMessage;
            }
        }
        catch (MagickException ex)
        {
            if (File.Exists(outputFileName))
            {
                try
                {
                    File.Delete(outputFileName);
                }
                catch { }
            }
            throw new Exception($"ImageMagick error processing '{Path.GetFileName(imagePath)}': {ex.Message}", ex);
        }
        catch (Exception)
        {
            if (File.Exists(outputFileName))
            {
                try
                {
                    File.Delete(outputFileName);
                }
                catch { }
            }
            throw;
        }
    }
}