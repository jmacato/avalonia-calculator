// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//
// Utils.cpp
//

  // #include  "pch.h"

  // #include  <winmeta.h>

  // #include  "Utils.h"
  // #include  "Common/AppResourceProvider.h"
  // #include  "Common/ExpressionCommandSerializer.h"
  // #include  "Common/ExpressionCommandDeserializer.h"

using CalculatorApp;
using CalculatorApp.ViewModel.Common;
using concurrency;
using Graphing.Renderer;


using Utils;
using Windows.ApplicationModel.Resources;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Storage;

void Utils.IFTPlatformException(HRESULT hr)
{
    if (FAILED(hr))
    {
        Platform.Exception  exception = new  Platform.Exception(hr);
        throw(exception);
    }
}

double Utils.GetDoubleFromstring(string input)
{
    constexpr  char  unWantedChars[] = { ' ', ',', 8234, 8235, 8236, 8237 };
    string ws = RemoveUnwantedCharsFromString(input, unWantedChars);
    return stod(ws);
}

void Utils.RunOnUIThreadNonblocking(function<void()>&& function,  CoreDispatcher currentDispatcher)
{
    if (currentDispatcher != null)
    {
        var task = create_task(currentDispatcher.RunAsync(CoreDispatcherPriority.Normal, new  DispatchedHandler([function]() { function(); })));
    }
}

// Returns if the last character of a string is the target  char  bool Utils.IsLastCharacterTarget( string const& input,   char  target)
{
    return !input.empty() && input.back() == target;
}

DateTime Utils.GetUniversalSystemTime()
{
    SYSTEMTIME sysTime = {};
    GetSystemTime(&sysTime);

    FILETIME sysTimeAsFileTime = {};
    SystemTimeToFileTime(&sysTime, &sysTimeAsFileTime);

    ULARGE_INTEGER ularge;
    ularge.HighPart = sysTimeAsFileTime.dwHighDateTime;
    ularge.LowPart = sysTimeAsFileTime.dwLowDateTime;

    DateTime result;
    result.UniversalTime = ularge.QuadPart;
    return result;
}

bool Utils.IsDateTimeOlderThan(DateTime dateTime, const long duration)
{
    DateTime now = Utils.GetUniversalSystemTime();
    return dateTime.UniversalTime + duration < now.UniversalTime;
}

task<void> Utils.WriteFileToFolder(IStorageFolder folder, String fileName, String contents, CreationCollisionOption collisionOption)
{
    if (folder == null)
    {
        co_return;
    }

    StorageFile file = co_await folder.CreateFileAsync(fileName, collisionOption);
    if (file == null)
    {
        co_return;
    }

    co_await FileIO.WriteTextAsync(file, contents);
}

task<String> Utils.ReadFileFromFolder(IStorageFolder folder, String fileName)
{
    if (folder == null)
    {
        co_return null;
    }

    StorageFile file = co_await folder.GetFileAsync(fileName);
    if (file == null)
    {
        co_return null;
    }

    String contents = co_await FileIO.ReadTextAsync(file);
    co_return contents;
}

bool Utils.AreColorsEqual(const Color& color1, const Color& color2)
{
    return ((color1.A == color2.A)
         && (color1.R == color2.R)
         && (color1.G == color2.G)
         && (color1.B == color2.B));
}

String^ Utils.Trim(String^ value)
{
    if (!value)
    {
        return null;
    }

    string trimmed = value.Data();
    Trim(trimmed);
    return new  String(trimmed.c_str());
}

void Utils.Trim(string& value)
{
    TrimFront(value);
    TrimBack(value);
}

void Utils.TrimFront(string& value)
{
    value.erase(value.begin(), find_if(value.cbegin(), value.cend(), [](int ch){
        return !isspace(ch);
    }));
}

void Utils.TrimBack(string& value)
{
    value.erase(find_if(value.crbegin(), value.crend(), [](int ch) {
        return !isspace(ch);
    }).base(), value.end());
}

bool operator==(const Color& color1, const Color& color2)
{
    return equal_to<Color>()(color1, color2);
}

bool operator!=(const Color& color1, const Color& color2)
{
    return !(color1 == color2);
}


bool CalculatorApp.ViewModel.Common.Utilities.AreColorsEqual(Windows.UI.Color color1, Windows.UI.Color color2)
{
    return Utils.AreColorsEqual(color1, color2);
}


bool CalculatorApp.ViewModel.Common.Utilities.GetIntegratedDisplaySize(double* size)
{
    if (SUCCEEDED(.GetIntegratedDisplaySize(size)))
        return true;
    return false;
}

