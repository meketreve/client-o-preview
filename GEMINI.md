# Project Overview

This is a Windows Presentation Foundation (WPF) application written in C#. Its main purpose is to allow users to preview other windows in live thumbnail formats. It uses the Desktop Window Manager (DWM) Thumbnail API to create the live previews.

The project is structured into the following main directories:
- `csharp/`: Contains C# source code for the application.
- `Models/`: Contains data models for the application.
- `Services/`: Contains services used by the application, such as the `WindowEnumerator` service.
- `Views/`: Contains the XAML views and code-behind files for the application's user interface.
- `Native/`: Contains P/Invoke calls to native Windows APIs.

# Building and Running

## Running the application
To run the application in a development environment, use the following command:

```powershell
dotnet run
```

## Building the application
To build the application for release, use the following command:

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
```

The output will be located in `bin/Release/net8.0-windows/win-x64/publish/ClientOPreview.exe`.

# Development Conventions

- The project follows standard C# and WPF conventions.
- XAML is used for the user interface, with the code-behind in C#.
- The Model-View-ViewModel (MVVM) pattern seems to be partially applied, with `Views`, `Models`, and `Services` directories.
- The application settings are stored in `%APPDATA%/client-o-preview/settings.json`.
