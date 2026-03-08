using Avalonia.Collections;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Eve_Settings_Management.Models;
using Eve_Settings_Management.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Eve_Settings_Management.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private static readonly HttpClient s_httpClient = new();
        private string? backupPath;
        private string? folderPathText;
        private int? progressBarValue;
        private Character? fromSelectedItem;
        private bool takeBackup;

        public MainWindowViewModel()
        {
            CopyCommand = ReactiveCommand.Create(async () =>
            {
                await Task.Run(() => CopyCharacterFiles());
            });

            SelectFolderDialogCommand = ReactiveCommand.Create(async () =>
            {
                if (MainWindow.Instance is null)
                {
                    return;
                }

                string defaultPath = ResolvePath();
                IStorageFolder? startFolder = null;
                if (Directory.Exists(defaultPath))
                {
                    startFolder = await MainWindow.Instance.StorageProvider.TryGetFolderFromPathAsync(defaultPath);
                }

                var result = await MainWindow.Instance.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    AllowMultiple = false,
                    SuggestedStartLocation = startFolder
                });

                var selectedFolder = result.FirstOrDefault();
                if (selectedFolder is not null)
                {
                    FolderPathText = selectedFolder.Path.LocalPath;
                    if (!string.IsNullOrWhiteSpace(FolderPathText))
                    {
                        await Task.Run(() => GetFiles(FolderPathText));
                    }
                }
            });

            BackupFolderDialogCommand = ReactiveCommand.Create(async () =>
            {
                if (MainWindow.Instance is null || string.IsNullOrWhiteSpace(backupPath) || !Directory.Exists(backupPath))
                {
                    return;
                }

                var startFolder = await MainWindow.Instance.StorageProvider.TryGetFolderFromPathAsync(backupPath);
                if (startFolder is not null)
                {
                    await MainWindow.Instance.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                    {
                        AllowMultiple = false,
                        SuggestedStartLocation = startFolder,
                        Title = "Backup folder"
                    });
                }
            });

            _ = InitializeDefaultFolderAsync();
        }

        public ICommand CopyCommand { get; }
        public ICommand SelectFolderDialogCommand { get; }
        public ICommand BackupFolderDialogCommand { get; }

        public string? FolderPathText
        {
            get => folderPathText;
            set => this.RaiseAndSetIfChanged(ref folderPathText, value);
        }

        public int? ProgressBarValue
        {
            get => progressBarValue;
            set => this.RaiseAndSetIfChanged(ref progressBarValue, value);
        }

        public AvaloniaList<object> ToSelectedItems { get; set; } = new AvaloniaList<object>();

        public AvaloniaList<Character> CopyFromCollection { get; } = new AvaloniaList<Character>();

        public AvaloniaList<Character> CopyToCollection { get; } = new AvaloniaList<Character>();

        public Character? FromSelectedItem
        {
            get => fromSelectedItem;
            set => this.RaiseAndSetIfChanged(ref fromSelectedItem, value);
        }

        public bool TakeBackup
        {
            get => takeBackup;
            set => this.RaiseAndSetIfChanged(ref takeBackup, value);
        }

        public async Task CopyCharacterFiles()
        {
            string dateNow = DateTime.Now.ToString("dd-MM-yyyy-(hh-mm-ss)");
            string settingsBackup = $"backup/settings_Backup{dateNow}";
            ProgressBarValue = 0;
            if (backupPath is not null && ToSelectedItems.Count > 0 && FromSelectedItem is not null)
            {
                foreach (var (item, character) in from Character? item in ToSelectedItems
                                                  let character = FromSelectedItem
                                                  select (item, character))
                {
                    if (character.CharacterName != item.CharacterName)
                    {
                        string? fileName = Path.GetFileName(item.CharacterFilePath);
                        if (fileName is not null)
                        {
                            await Task.Run(() =>
                            {
                                if (TakeBackup)
                                {
                                    DirectoryInfo backUpDirectory = Directory.CreateDirectory(Path.Combine(path1: backupPath,
                                                                            path2: settingsBackup));
                                    string backupFilePath = Path.Combine(backUpDirectory.FullName, fileName);
                                    try
                                    {
                                        if (item.CharacterFilePath is not null)
                                        {
                                            File.Copy(sourceFileName: item.CharacterFilePath,
                                                      backupFilePath,
                                                      true);
                                        }
                                    }
                                    catch (IOException copyError)
                                    {
                                        Debug.WriteLine(copyError.Message);
                                    }
                                }

                                try
                                {
                                    if (character.CharacterFilePath is not null && item.CharacterFilePath is not null)
                                    {
                                        File.Copy(character.CharacterFilePath, item.CharacterFilePath, true);
                                    }
                                }
                                catch (IOException copyError)
                                {
                                    Debug.WriteLine(copyError.Message);
                                }

                                Debug.WriteLine($"Name:{character.CharacterName} ID:{character.CharacterId} copied to Name:{item.CharacterName} ID:{item.CharacterId}");
                                Debug.WriteLine($"From {character.CharacterFilePath}");
                                Debug.WriteLine($"To {item.CharacterFilePath}");
                                Debug.WriteLine("------------------------------------------------------------------");
                            });
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Passed {item.CharacterName} as source");
                        Debug.WriteLine("------------------------------------------------------------------");
                    }

                    ProgressBarValue += 100 / ToSelectedItems.Count + 1;
                    Debug.WriteLine($"Percent: {progressBarValue}");
                    await Task.Delay(TimeSpan.FromMilliseconds(300));
                }
            }
        }

        public async Task GetFiles(string dir)
        {
            await Task.Run(async () =>
            {
                ClearCollection();
                if (!Directory.Exists(dir))
                {
                    return;
                }

                var characterEntries = Directory
                    .EnumerateFiles(dir, "core_char_*", SearchOption.TopDirectoryOnly)
                    .Select(path => new { Path = path, Id = PathToID(path) })
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id) && item.Id.All(char.IsDigit))
                    .GroupBy(item => item.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToList();

                var semaphore = new SemaphoreSlim(8);
                var tasks = characterEntries.Select(async entry =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await GetCharacter(entry.Id, entry.Path);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            });
        }

        public string ResolvePath()
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string eveFolder = $"{localData}\\CCP\\EVE\\";
            if (!Directory.Exists(eveFolder))
            {
                return eveFolder;
            }

            string? tranqFolder = Directory
                .EnumerateDirectories(eveFolder)
                .FirstOrDefault(path =>
                    Path.GetFileName(path).Contains("_eve_sharedcache_tq_tranquility", StringComparison.OrdinalIgnoreCase));

            if (tranqFolder is null)
            {
                return eveFolder;
            }

            backupPath = tranqFolder;
            var settingsFolders = Directory
                .EnumerateDirectories(tranqFolder)
                .Where(path => Path.GetFileName(path).StartsWith("settings_", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (settingsFolders.Count == 0)
            {
                return tranqFolder;
            }

            string? defaultFolder = settingsFolders.FirstOrDefault(path =>
                Path.GetFileName(path).Contains("default", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(defaultFolder))
            {
                return defaultFolder;
            }

            return settingsFolders
                .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                .First();
        }

        private static async Task<JObject?> JsonHandler(string url)
        {
            try
            {
                Debug.WriteLine(url);
                HttpResponseMessage response = await s_httpClient.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();
                JObject? jsonToObject = JsonConvert.DeserializeObject<JObject>(responseBody);
                return jsonToObject;
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine($"Failed to connect ESI'{e}'");
                return null;
            }
        }

        private static string PathToID(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            const string pattern = "[0-9]+";
            Match m = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);
            return m.Value;
        }

        public static async Task<Stream> LoadPotraitBitmapAsync(string characterid)
        {
            string url = $"https://images.evetech.net/characters/{characterid}/portrait?tenant=tranquility&size=64";
            Debug.WriteLine(url);
            var data = await s_httpClient.GetByteArrayAsync(url);
            return new MemoryStream(data);
        }

        public static async Task<Bitmap> LoadPotrait(string characterid)
        {
            await using var imageStream = await LoadPotraitBitmapAsync(characterid);
            return await Task.Run(() => Bitmap.DecodeToWidth(imageStream, 64));
        }

        private void AddCharacter(Character character)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CopyFromCollection.Add(character);
                CopyToCollection.Add(character);
            });
        }

        private void ClearCollection()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CopyFromCollection.Clear();
                CopyToCollection.Clear();
            });
        }

        private async Task GetCharacter(string characterid, string characterfilepath)
        {
            Debug.WriteLine(characterid);
            JObject? json = await JsonHandler($"https://esi.evetech.net/latest/characters/{characterid}/?datasource=tranquility");
            if (json is not null && string.Equals(json["error"]?.ToString(), "Character has been deleted!", StringComparison.Ordinal))
            {
                return;
            }

            string characterName = json?["name"]?.ToString() ?? $"Character {characterid}";
            Character character = new()
            {
                CharacterName = characterName,
                CharacterId = characterid,
                CharacterFilePath = characterfilepath
            };
            AddCharacter(character);
        }

        private async Task InitializeDefaultFolderAsync()
        {
            string defaultPath = ResolvePath();
            FolderPathText = defaultPath;
            if (!string.IsNullOrWhiteSpace(defaultPath) && Directory.Exists(defaultPath))
            {
                await GetFiles(defaultPath);
            }
        }
    }
}
