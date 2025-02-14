﻿using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Markup.Xaml;
using Emgu.CV;
using UVtools.Core;
using UVtools.Core.SystemOS;
using UVtools.WPF.Controls;

namespace UVtools.WPF.Windows;

public class AboutWindow : WindowEx
{
    public string Software => About.Software;
    public string Version => $"Version: {App.VersionStr} {RuntimeInformation.ProcessArchitecture}";
    public string Copyright => App.AssemblyCopyright;
    public string Company => App.AssemblyCompany;
    public string License => About.License;
    public string Description => App.AssemblyDescription;
    public string OpenCVBuildInformation => CvInvoke.BuildInformation;
    public string LoadedAssemblies 
    {
        get
        {
            var sb = new StringBuilder();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i].GetName();
                sb.AppendLine($"{(i + 1).ToString().PadLeft(assemblies.Length.ToString().Length, '0')}: {assembly.Name}, Version={assembly.Version}");
            }

            return sb.ToString();
        }
    }

    public string OSDescription => $"{RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}";

    public string RuntimeDescription => RuntimeInformation.RuntimeIdentifier;

    public string FrameworkDescription => RuntimeInformation.FrameworkDescription;
    public string AvaloniaUIDescription => typeof(AvaloniaObject).Assembly.GetName().Version.ToString(3);

    public string OpenCVVersion
    {
        get
        {
            var match = Regex.Match(CvInvoke.BuildInformation, @"(?:Version control:\s*)(\S*)");
            if (!match.Success) return "Not found!";
            var index = match.Groups[1].Value.LastIndexOf('-');
            if (index < 0) return match.Groups[1].Value;
            return match.Groups[1].Value[..index];
            //return match.Groups[1].Value;
        }
    }

    public string ProcessorName => SystemAware.GetProcessorName();

    public int ProcessorCount => Environment.ProcessorCount;

    public string MemoryRAMDescription
    {
        get
        {
            var memory = SystemAware.GetMemoryStatus();
            if (memory.ullTotalPhys == 0)
            {
                return "Unknown";
            }

            var factor = Math.Pow(1024, 3);
            return $"{(memory.ullTotalPhys-memory.ullAvailPhys) / factor:F2} / {memory.ullTotalPhys / factor:F2} GB";
        }
    }

    public int ScreenCount => Screens.ScreenCount;
    //public string ScreenResolution => $"{Screens.Primary.Bounds.Width} x {Screens.Primary.Bounds.Height} @ {Screens.Primary.PixelDensity*100}%";
    //public string WorkingArea => $"{Screens.Primary.WorkingArea.Width} x {Screens.Primary.WorkingArea.Height}";
    //public string RealWorkingArea => $"{App.MaxWindowSize.Width} x {App.MaxWindowSize.Height}";

    public string ScreensDescription
    {
        get
        {
            var result = new StringBuilder();
            for (int i = 0; i < Screens.All.Count; i++)
            {
                var onScreen = Screens.ScreenFromVisual(App.MainWindow);
                var screen = Screens.All[i];
                result.AppendLine($"{i+1}: {screen.Bounds.Width} x {screen.Bounds.Height} @ {screen.PixelDensity * 100}%" + 
                                  (screen.Primary ? " (Primary)" : string.Empty) +
                                  (onScreen == screen ? " (On this)" : string.Empty)                        
                );
                result.AppendLine($"    WA: {screen.WorkingArea.Width} x {screen.WorkingArea.Height}    UA: {Math.Round(screen.WorkingArea.Width / screen.PixelDensity)} x {Math.Round(screen.WorkingArea.Height / screen.PixelDensity)}");
            }
            return result.ToString().TrimEnd();
        }
    }

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
        Title = $"About {About.SoftwareWithVersion}";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void OpenLicense() => SystemAware.OpenBrowser(About.LicenseUrl);

    private string GetEssentialInformation()
    {
        var message = new StringBuilder();
        message.AppendLine($"{About.SoftwareWithVersion}");
        message.AppendLine($"Operative system: {OSDescription}");
        message.AppendLine($"Processor: {ProcessorName}");
        message.AppendLine($"Processor cores: {ProcessorCount}");
        message.AppendLine($"Memory RAM: {MemoryRAMDescription}");
        message.AppendLine($"Runtime: {RuntimeDescription}");
        message.AppendLine($"Framework: {FrameworkDescription}");
        message.AppendLine($"AvaloniaUI: {AvaloniaUIDescription}");
        message.AppendLine($"OpenCV: {OpenCVVersion}");
        message.AppendLine();
        message.AppendLine("Sreens, resolution, working area, usable area:");
        message.AppendLine(ScreensDescription);
        message.AppendLine();
        message.AppendLine($"Path: {App.ApplicationPath}");
        return message.ToString();
    }

    public void CopyEssentialInformation()
    {
        Application.Current.Clipboard.SetTextAsync(GetEssentialInformation());
    }
        

    public void CopyOpenCVInformationToClipboard()
    {
        Application.Current.Clipboard.SetTextAsync(CvInvoke.BuildInformation);
    }

    public void CopyLoadedAssembliesToClipboard()
    {
        Application.Current.Clipboard.SetTextAsync(LoadedAssemblies);
    }

    public async void CopyInformationToClipboard()
    {
        var message = new StringBuilder();
        message.Append(GetEssentialInformation());
        message.AppendLine(CvInvoke.BuildInformation);
        message.AppendLine("Loaded Assemblies:");
        message.AppendLine(LoadedAssemblies);
        await Application.Current.Clipboard.SetTextAsync(message.ToString());
    }
}