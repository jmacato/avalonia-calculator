// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CalculatorApp.Services.Settings;

public sealed class JsonSettingsStore : SettingsStoreBase
{
    private const string SettingsFilename = "settings.json";
    private readonly string _path;
    private JsonSettingsStore(string path, AppSettings current) : base(current)
    {
        _path = path;
    }

    public static JsonSettingsStore CreateDefault()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        return Create(Path.Combine(root, "io.github.jmacato.calculator", SettingsFilename));
    }

    public static JsonSettingsStore Create(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        return new JsonSettingsStore(fullPath, Load(fullPath));
    }

    protected override void Persist(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = _path + ".tmp";
        try
        {
            byte[] json = Encoding.UTF8.GetBytes(AppSettingsSerializer.Serialize(settings));
            using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.WriteThrough))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Unable to persist Calculator settings: {0}", exception.Message);
            TryDelete(temporaryPath);
        }
    }

    private static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            return AppSettingsSerializer.Deserialize(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or FormatException)
        {
            BackupCorruptFile(path);
            Trace.TraceWarning("Reset corrupt Calculator settings: {0}", exception.Message);
            return new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Unable to read Calculator settings: {0}", exception.Message);
            return new AppSettings();
        }
    }

    private static void BackupCorruptFile(string path)
    {
        try
        {
            string backupPath = path + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(path, backupPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Unable to back up corrupt Calculator settings: {0}", exception.Message);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Unable to remove temporary Calculator settings: {0}", exception.Message);
        }
    }
}
