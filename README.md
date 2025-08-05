# What's different in this fork?

-   **Implemented CLI arguments to support custom max resolution and max size.**

    So you can use this tool to upload images to other Image DB sites with different image requirements. (eg. [cinematerial](https://www.cinematerial.com/) has 5000px & 7.5MB as requirements)

    -   **Usage**
        -   `msdbc 5000px` will set the max resolution to 5000px instead of default 7500px.
        -   `msdbc 6MB` will set the max size to 6.0MB instead of default 7.5MB.
        -   `msdbc 4000px 3MB` or `msdbc 3MB 4000px`, argument position doesn't matter.

-   **Uses two segment binary search** to determine best quality between 100 to 50. eg: quality 75 takes just **3** encodings instead of **25** 🤯.
-   **Handles unsupported image orientations** (EXIF orientation) even for files that meets requirements.
-   The ouput image is always less than `7.5MB` thus no file size errors.
-   Images are directly created inside **`Converted`** folder instead of **`Converted/yyyy-MM-dd_HH-mm-ss`**.

# MSDBConverter

[![Version](https://img.shields.io/github/v/release/niccoloc0/MSDBConverter?color=%230567ff&label=Latest%20Release&style=for-the-badge)](https://github.com/niccoloc0/MSDBConverter/releases/latest)
![GitHub Downloads (specific asset, all releases)](https://img.shields.io/github/downloads/niccoloc0/MSDBConverter/MSDBConverter.exe?label=Total%20Downloads&style=for-the-badge)

Have you ever found yourself spending considerable time converting/compressing and individually checking a large batch of images to ensure they meet [MovieStillsDB](https://www.moviestillsdb.com/)'s standards before uploading? If your answer is yes, then this tool is tailor-made for you!

This tool efficiently converts and compresses multiple images simultaneously to adhere to MovieStillsDB's standards. It resizes images exceeding 7500 pixels in width or height while maintaining the original aspect ratio. Additionally, it compresses files larger than 7.5 MB without compromising quality and seamlessly converts various formats, including jpeg, png, tif, tiff, and raw files, to jpg, ensuring they're ready for hassle-free uploads.

## Usage

Download the [latest release](https://github.com/niccoloc0/MSDBConverter/releases) of the tool (MSDBConverter.exe) and place it in a folder of your choice. When you run the program, a 'ToConvert' folder will be created. Place the images you want to convert inside this folder and run the program again. The converted files will be placed in a newly created 'Converted' folder, which will contain another folder named with the current date and time. Inside this final folder, you'll find the converted images.

## Local Build command

```sh
dotnet publish MSDBConverter.sln -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:AssemblyName=msdbc
```

## Support

Feel free to provide feedback on the tool by sending a private message to my MovieStillsDB account.
