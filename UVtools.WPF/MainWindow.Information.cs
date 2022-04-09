﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MessageBox.Avalonia.Enums;
using UVtools.Core;
using UVtools.Core.Layers;
using UVtools.Core.Objects;
using UVtools.Core.SystemOS;
using UVtools.WPF.Extensions;
using UVtools.WPF.Structures;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using Helpers = UVtools.WPF.Controls.Helpers;

namespace UVtools.WPF;

public partial class MainWindow
{
    public RangeObservableCollection<SlicerProperty> SlicerProperties { get; } = new();
    public DataGrid PropertiesGrid;
    public DataGrid CurrentLayerGrid;

    private uint _visibleThumbnailIndex;
    private Bitmap _visibleThumbnailImage;
    private RangeObservableCollection<ValueDescription> _currentLayerProperties = new();

    public RangeObservableCollection<ValueDescription> CurrentLayerProperties
    {
        get => _currentLayerProperties;
        set => RaiseAndSetIfChanged(ref _currentLayerProperties, value);
    }

    public void InitInformation()
    {
        PropertiesGrid = this.Find<DataGrid>(nameof(PropertiesGrid));
        CurrentLayerGrid = this.Find<DataGrid>(nameof(CurrentLayerGrid));
        PropertiesGrid.KeyUp += GridOnKeyUp;
        CurrentLayerGrid.KeyUp += GridOnKeyUp;
        /*CurrentLayerGrid.BeginningEdit += (sender, e) =>
        {
            if (e.Row.DataContext is StringTag stringTag)
            {
                if (e.Column.DisplayIndex == 0
                    || e.Row.DataContext.ToString() != nameof(LayerCache.Layer.ExposureTime)
                    && e.Row.DataContext.ToString() != nameof(LayerCache.Layer.LightPWM)
                )
                {
                    e.Cancel = true;
                }
            }
            else
            {
                e.Cancel = true;
            }
        };
        CurrentLayerGrid.RowEditEnding += (sender, e) =>
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (!(e.Row.DataContext is StringTag stringTag)) return;
            if (float.TryParse(stringTag.TagString, out var result)) return;
            e.Cancel = true;
        };
        CurrentLayerGrid.RowEditEnded += (sender, e) =>
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (!(e.Row.DataContext is StringTag stringTag)) return;
            switch (stringTag.Content)
            {
                //case nameof(LayerCache.)
            }
        };*/

    }

    private void GridOnKeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    dataGrid.SelectedItems.Clear();
                    break;
                case Key.Multiply:
                    foreach (var item in dataGrid.Items)
                    {
                        if (dataGrid.SelectedItems.Contains(item))
                            dataGrid.SelectedItems.Remove(item);
                        else
                            dataGrid.SelectedItems.Add(item);
                    }

                    break;
            }
        }
    }

    #region Thumbnails
    public uint VisibleThumbnailIndex
    {
        get => _visibleThumbnailIndex;
        set
        {
            if (value == 0)
            {
                RaiseAndSetIfChanged(ref _visibleThumbnailIndex, value);
                RaisePropertyChanged(nameof(ThumbnailCanGoPrevious));
                RaisePropertyChanged(nameof(ThumbnailCanGoNext));
                VisibleThumbnailImage = null;
                return;
            }

            if (!IsFileLoaded) return;
            var index = value - 1;
            if (index >= SlicerFile.CreatedThumbnailsCount) return;
            if (SlicerFile.Thumbnails[index] is null || SlicerFile.Thumbnails[index].IsEmpty) return;
            if (!RaiseAndSetIfChanged(ref _visibleThumbnailIndex, value)) return;

            VisibleThumbnailImage = SlicerFile.Thumbnails[index].ToBitmap();
            RaisePropertyChanged(nameof(ThumbnailCanGoPrevious));
            RaisePropertyChanged(nameof(ThumbnailCanGoNext));
        }
    }

    public bool ThumbnailCanGoPrevious => SlicerFile is not null && _visibleThumbnailIndex > 1;
    public bool ThumbnailCanGoNext => SlicerFile is not null && _visibleThumbnailIndex < SlicerFile.CreatedThumbnailsCount;

    public void ThumbnailGoPrevious()
    {
        if (!ThumbnailCanGoPrevious) return;
        VisibleThumbnailIndex--;
    }

    public void ThumbnailGoNext()
    {
        if (!ThumbnailCanGoNext) return;
        VisibleThumbnailIndex++;
    }

    public Bitmap VisibleThumbnailImage
    {
        get => _visibleThumbnailImage;
        set
        {
            RaiseAndSetIfChanged(ref _visibleThumbnailImage, value);
            RaisePropertyChanged(nameof(VisibleThumbnailResolution));
        }
    }

    public string VisibleThumbnailResolution => _visibleThumbnailImage is null ? null : $"{{Width: {_visibleThumbnailImage.Size.Width}, Height: {_visibleThumbnailImage.Size.Height}}}";

    public async void OnClickThumbnailSave()
    {
        if (SlicerFile is null) return;
        if (SlicerFile.Thumbnails[_visibleThumbnailIndex - 1] is null)
        {
            return; // This should never happen!
        }
        var dialog = new SaveFileDialog
        {
            Filters = Helpers.PngFileFilter,
            Directory = Path.GetDirectoryName(SlicerFile.FileFullPath),
            InitialFileName = $"{Path.GetFileNameWithoutExtension(SlicerFile.FileFullPath)}_thumbnail{_visibleThumbnailIndex}.png"
        };

        var filepath = await dialog.ShowAsync(this);

        if (!string.IsNullOrEmpty(filepath))
        {
            SlicerFile.Thumbnails[_visibleThumbnailIndex - 1].Save(filepath);
        }
    }

    public async void OnClickThumbnailImport()
    {
        if (_visibleThumbnailIndex <= 0) return;
        if (SlicerFile.Thumbnails[_visibleThumbnailIndex - 1] is null)
        {
            return; // This should never happen!
        }

        var dialog = new OpenFileDialog
        {
            Filters = Helpers.ImagesFileFilter,
            AllowMultiple = false
        };

        var filepath = await dialog.ShowAsync(this);

        if (filepath is null || filepath.Length <= 0) return;
        uint i = _visibleThumbnailIndex - 1;
        SlicerFile.SetThumbnail((int)i, filepath[0]);
        //VisibleThumbnailImage = SlicerFile.Thumbnails[i].ToBitmap();
        SlicerFile.RequireFullEncode = true;
        CanSave = true;
    }

    public void RefreshThumbnail()
    {
        if (_visibleThumbnailIndex <= 0) return;
        uint i = _visibleThumbnailIndex - 1;
        if (SlicerFile.Thumbnails?[i] is null)
        {
            return; 
        }
        VisibleThumbnailImage = SlicerFile.Thumbnails[i].ToBitmap();
    }
    #endregion

    #region Slicer Properties

    public async void OnClickPropertiesSaveFile()
    {
        if (SlicerFile?.Configs is null) return;

        var dialog = new SaveFileDialog
        {
            Filters = Helpers.IniFileFilter,
            Directory = Path.GetDirectoryName(SlicerFile.FileFullPath),
            InitialFileName = $"{Path.GetFileNameWithoutExtension(SlicerFile.FileFullPath)}_properties.ini"
        };

        var file = await dialog.ShowAsync(this);

        if (string.IsNullOrEmpty(file)) return;

        try
        {
            using TextWriter tw = new StreamWriter(file);
            foreach (var config in SlicerFile.Configs)
            {
                var type = config.GetType();
                tw.WriteLine($"[{type.Name}]");
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.Name.Equals("Item")) continue;
                    var value = property.GetValue(config);
                    switch (value)
                    {
                        case null:
                            continue;
                        case IList list:
                            tw.WriteLine($"{property.Name} = {list.Count}");
                            break;
                        default:
                            tw.WriteLine($"{property.Name} = {value}");
                            break;
                    }
                }
                tw.WriteLine();
            }
            tw.Close();
        }
        catch (Exception e)
        {
            await this.MessageBoxError(e.ToString(), "Error occur while save properties");
            return;
        }

        var result = await this.MessageBoxQuestion(
            "Properties save was successful. Do you want open the file in the default editor?",
            "Properties save complete");
        if (result != ButtonResult.Yes) return;

        SystemAware.StartProcess(file);
    }

    public void OnClickPropertiesSaveClipboard()
    {
        if (SlicerFile?.Configs is null) return;
        var sb = new StringBuilder();
        foreach (var config in SlicerFile.Configs)
        {
            var type = config.GetType();
            sb.AppendLine($"[{type.Name}]");
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name.Equals("Item")) continue;
                var value = property.GetValue(config);
                switch (value)
                {
                    case null:
                        continue;
                    case IList list:
                        sb.AppendLine($"{property.Name} = {list.Count}");
                        break;
                    default:
                        sb.AppendLine($"{property.Name} = {value}");
                        break;
                }
            }

            sb.AppendLine();
        }

        Application.Current.Clipboard.SetTextAsync(sb.ToString());
    }

    public void RefreshProperties()
    {
        SlicerProperties.Clear();
        if (SlicerFile.Configs is null) return;
        foreach (var config in SlicerFile.Configs)
        {
            var type = config.GetType();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name.Equals("Item")) continue;
                var value = property.GetValue(config);
                switch (value)
                {
                    case null:
                        continue;
                    case IList list:
                        SlicerProperties.Add(new SlicerProperty(property.Name, list.Count.ToString(),
                            config.GetType().Name));
                        break;
                    default:
                        SlicerProperties.Add(
                            new SlicerProperty(property.Name, value.ToString(), config.GetType().Name));
                        break;
                }
            }
        }
    }
    #endregion

    #region Current Layer

    public void RefreshCurrentLayerData()
    {
            
        var layer = LayerCache.Layer;
        CurrentLayerProperties.Clear();
        CurrentLayerProperties.Add(new ValueDescription($"{layer.Index}", nameof(layer.Index)));
        CurrentLayerProperties.Add(new ValueDescription($"{Layer.ShowHeight(layer.LayerHeight)}mm", nameof(layer.LayerHeight)));
        CurrentLayerProperties.Add(new ValueDescription($"{Layer.ShowHeight(layer.PositionZ)}mm", nameof(layer.PositionZ)));
        CurrentLayerProperties.Add(new ValueDescription(layer.IsBottomLayer.ToString(), nameof(layer.IsBottomLayer)));
        CurrentLayerProperties.Add(new ValueDescription(layer.IsModified.ToString(), nameof(layer.IsModified)));

        if (SlicerFile.CanUseExposureTime)
            CurrentLayerProperties.Add(new ValueDescription($"{layer.ExposureTime:F2}s", nameof(layer.ExposureTime)));

        if (SlicerFile.SupportPerLayerSettings)
        {
            if (SlicerFile.CanUseLayerLiftHeight)
                CurrentLayerProperties.Add(new ValueDescription($"{layer.LiftHeight.ToString(CultureInfo.InvariantCulture)}mm @ {layer.LiftSpeed.ToString(CultureInfo.InvariantCulture)}mm/min", nameof(layer.LiftHeight)));
            if (SlicerFile.CanUseLayerLiftHeight2)
                CurrentLayerProperties.Add(new ValueDescription($"{layer.LiftHeight2.ToString(CultureInfo.InvariantCulture)}mm @ {layer.LiftSpeed2.ToString(CultureInfo.InvariantCulture)}mm/min", nameof(layer.LiftHeight2)));

            if (SlicerFile.CanUseLayerRetractSpeed)
                CurrentLayerProperties.Add(new ValueDescription($"{layer.RetractHeight.ToString(CultureInfo.InvariantCulture)}mm @ {layer.RetractSpeed}mm/min", nameof(layer.RetractHeight)));
            if (SlicerFile.CanUseLayerRetractHeight2)
                CurrentLayerProperties.Add(new ValueDescription($"{layer.RetractHeight2.ToString(CultureInfo.InvariantCulture)}mm @ {layer.RetractSpeed2}mm/min", nameof(layer.RetractHeight2)));

            if (SlicerFile.CanUseLayerLightOffDelay || SlicerFile.CanUseAnyLightOffDelay)
                CurrentLayerProperties.Add(new ValueDescription($"{layer.LightOffDelay}s", nameof(layer.LightOffDelay)));

            if (SlicerFile.CanUseLayerWaitTimeBeforeCure || SlicerFile.CanUseAnyWaitTimeBeforeCure)
                CurrentLayerProperties.Add(new ValueDescription($"{layer.WaitTimeBeforeCure}/{layer.WaitTimeAfterCure}/{layer.WaitTimeAfterLift}s", "WaitTimes:"));

            if (SlicerFile.CanUseLayerLightPWM)
                CurrentLayerProperties.Add(new ValueDescription(layer.LightPWM.ToString(), nameof(layer.LightPWM)));
        }
        var materialMillilitersPercent = layer.MaterialMillilitersPercent;
        if (!float.IsNaN(materialMillilitersPercent))
        {
            CurrentLayerProperties.Add(new ValueDescription($"{layer.MaterialMilliliters}ml ({materialMillilitersPercent:F2}%)", nameof(layer.MaterialMilliliters)));
        }

    }
    #endregion
}