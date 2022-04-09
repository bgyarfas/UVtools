/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */

using System;
using UVtools.Core;
using UVtools.Core.Extensions;
using UVtools.Core.Scripting;

namespace UVtools.ScriptSample;

/// <summary>
/// Change layer properties to random values
/// </summary>
public class ScriptDebandingZSample : ScriptGlobals
{
    readonly ScriptCheckBoxInput CreateEmptyLayerInput = new()
    {
        Label = "Create a first empty layer to overcome printer firmware limitation",
        ToolTip = "Some printers will not respect wait time for the first layer, introducing the problem once again. Use this option to by pass that",
        Value = true
    };

    readonly ScriptNumericalInput<decimal> BottomSafeDebandingHeightInput = new()
    {
        Label = "Safe height for the large debanding time",
        //ToolTip = "Margin in pixels to inset from object edge",
        Unit = "mm",
        Minimum = 0.1m,
        Maximum = 50,
        Increment = 0.5m,
        Value = 1.0m,
        DecimalPlates = 2,
    };

    readonly ScriptNumericalInput<decimal> BottomWaitTimeBeforeCureInput = new()
    {
        Label = "Large debanding wait time before cure",
        ToolTip = "Time to wait before cure a debanding layer",
        Unit = "s",
        Minimum = 5,
        Maximum = 300,
        Increment = 1,
        Value = 60,
        DecimalPlates = 2,
    };

    readonly ScriptNumericalInput<ushort> DebandingHeightTransitionLayersInput = new()
    {
        Label = "Number of layers to transition to normal wait time",
        ToolTip = "Allows a transition from large debanding wait time to normal wait time",
        Unit = "layers",
        Minimum = 0,
        Maximum = 100,
        Increment = 1,
        Value = 20,
    };

    readonly ScriptNumericalInput<decimal> NormalWaitTimeBeforeCureInput = new()
    {
        Label = "Normal wait time before cure",
        ToolTip = "Time to wait before cure a normal layer",
        Unit = "s",
        Minimum = 1,
        Maximum = 300,
        Increment = 1,
        Value = 3,
        DecimalPlates = 2,
    };

    readonly ScriptNumericalInput<decimal> BottomWaitTimeAfterCureInput = new()
    {
        Label = "Bottom wait time after cure",
        ToolTip = "Time to wait after cure a bottom layer",
        Unit = "s",
        Minimum = 0,
        Maximum = 100,
        Increment = 1,
        Value = 5,
        DecimalPlates = 2,
    };

    readonly ScriptNumericalInput<decimal> NormalWaitTimeAfterCureInput = new()
    {
        Label = "Normal wait time after cure",
        ToolTip = "Time to wait after cure a normal layer",
        Unit = "s",
        Minimum = 0,
        Maximum = 100,
        Increment = 1,
        Value = 2,
        DecimalPlates = 2,
    };

    readonly ScriptNumericalInput<decimal> HeightOffsetInput = new()
    {
        Label = "Height offset for the print",
        ToolTip = "Replaces the 'Z Offset' function",
        Unit = "mm",
        Minimum = 0.0m,
        Maximum = 10,
        Increment = 0.05m,
        Value = 0.1m,
        DecimalPlates = 2,
    };

    readonly ScriptNumericalInput<ushort> RepeatBottomLayerInput = new()
    {
        Label = "Repeat bottom layer N times",
        ToolTip = "Repeat the bottom layer N times, does not increase number of times bottom layer exposures",
        Unit = "layers",
        Minimum = 0,
        Maximum = ushort.MaxValue,
        Increment = 1,
        Value = 20
    };

    /// <summary>
    /// Set configurations here, this function trigger just after load a script
    /// </summary>
    public void ScriptInit()
    {
        Script.Name = "Debanding Z with wait time";
        Script.Description = "Applies wait time at certain layers to help layer adhesion and debanding the Z axis.\n" +
                             "Based on the guide: https://bit.ly/3nkXAOa\n";
        Script.Author = "Tiago Conceição";
        Script.Version = new Version(0, 2);
        if (SlicerFile.SupportsGCode) CreateEmptyLayerInput.Value = false;
        Script.UserInputs.Add(CreateEmptyLayerInput);
        Script.UserInputs.Add(BottomSafeDebandingHeightInput);
        Script.UserInputs.Add(DebandingHeightTransitionLayersInput);
        Script.UserInputs.Add(BottomWaitTimeBeforeCureInput);
        Script.UserInputs.Add(NormalWaitTimeBeforeCureInput);
        if(SlicerFile.CanUseBottomWaitTimeAfterCure) Script.UserInputs.Add(BottomWaitTimeAfterCureInput);
        if(SlicerFile.CanUseWaitTimeAfterCure) Script.UserInputs.Add(NormalWaitTimeAfterCureInput);
        Script.UserInputs.Add(HeightOffsetInput);
        Script.UserInputs.Add(RepeatBottomLayerInput);
    }

    /// <summary>
    /// Validate user inputs here, this function trigger when user click on execute
    /// </summary>
    /// <returns>A error message, empty or null if validation passes.</returns>
    public string? ScriptValidate()
    {
        return SlicerFile.CanUseAnyLightOffDelay || SlicerFile.CanUseAnyWaitTimeBeforeCure ? null : "Your printer/file format is not supported.";
    }

    /// <summary>
    /// Execute the script, this function trigger when when user click on execute and validation passes
    /// </summary>
    /// <returns>True if executes successfully to the end, otherwise false.</returns>
    public bool ScriptExecute()
    {
        Progress.Reset("Changing layers", Operation.LayerRangeCount); // Sets the progress name and number of items to process

        if (SlicerFile.CanUseAnyWaitTime)
        {
            SlicerFile.BottomLightOffDelay = 0;
            SlicerFile.LightOffDelay = 0;
            SlicerFile.BottomWaitTimeBeforeCure = (float) BottomWaitTimeBeforeCureInput.Value;
            SlicerFile.WaitTimeBeforeCure = (float)NormalWaitTimeBeforeCureInput.Value;
        }
        else
        {
            SlicerFile.SetBottomLightOffDelay((float)BottomWaitTimeBeforeCureInput.Value);
            SlicerFile.SetNormalLightOffDelay((float)NormalWaitTimeBeforeCureInput.Value);
        }

        if (SlicerFile.CanUseBottomWaitTimeAfterCure) SlicerFile.BottomWaitTimeAfterCure = (float) BottomWaitTimeAfterCureInput.Value;
        if (SlicerFile.CanUseWaitTimeAfterCure) SlicerFile.WaitTimeAfterCure = (float)NormalWaitTimeAfterCureInput.Value;

        if (RepeatBottomLayerInput.Value > 0)
        {
            var repeatLayer_ = SlicerFile.FirstLayer;
            for (int i = 0; i < (int)RepeatBottomLayerInput.Value; i++)
            {
                var repeatLayer = repeatLayer_.Clone();
                SlicerFile.Prepend(repeatLayer);
            }
        }


        decimal transition_distance = (decimal) ((float) DebandingHeightTransitionLayersInput.Value * SlicerFile.LayerHeight);
        float decrement = Math.Max(((float)BottomWaitTimeBeforeCureInput.Value - (float)NormalWaitTimeBeforeCureInput.Value) / ( (float)DebandingHeightTransitionLayersInput.Value + 1), 0f);
        float current_transition = (float)BottomWaitTimeBeforeCureInput.Value;
        foreach (var layer in SlicerFile)
        {
            if((decimal)layer.PositionZ > BottomSafeDebandingHeightInput.Value)
            {
                if ((decimal)layer.PositionZ > BottomSafeDebandingHeightInput.Value + transition_distance ) break;
                current_transition -= decrement;
                layer.SetWaitTimeBeforeCureOrLightOffDelay(current_transition);
            }
            else
            {
                layer.SetWaitTimeBeforeCureOrLightOffDelay((float) BottomWaitTimeBeforeCureInput.Value);
            }
        }

        if (CreateEmptyLayerInput.Value)
        {
            var firstLayer = SlicerFile.FirstLayer;
            if (firstLayer is not null)
            {
                if (firstLayer.NonZeroPixelCount > 1) // First layer is not blank as it seems, lets create one
                {
                    firstLayer = firstLayer.Clone();
                    using var mat = EmguExtensions.InitMat(SlicerFile.Resolution);
                    var pixelPos = firstLayer.BoundingRectangle.Center();
                    mat.SetByte(pixelPos.X, pixelPos.Y,
                        1); // Print a very fade pixel to ignore empty layer detection
                    firstLayer.LayerMat = mat;
                    firstLayer.ExposureTime = SlicerFile.SupportsGCode ? 0 : 0.05f;
                    firstLayer.SetNoDelays();
                    SlicerFile.SuppressRebuildPropertiesWork(() => { SlicerFile.Prepend(firstLayer); });
                }
            }
        }

        foreach (var layer in SlicerFile)
        {
            layer.PositionZ += (float)HeightOffsetInput.Value;
        }

        // return true if not cancelled by user
        return !Progress.Token.IsCancellationRequested;
    }
}