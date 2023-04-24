using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Eve_Settings_Management.Models;
using Eve_Settings_Management.Views;
using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Eve_Settings_Management.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private static readonly HttpClient s_httpClient = new();
        private string? backupPath;
        private string? folderPathText;
        private int? progressValue;

        public MainWindowViewModel()
        {
            CopyCommand = ReactiveCommand.Create(async () =>
            {
                await Task.Run(() => CopyCharacterFiles());
            });

            SelectFolderDialogCommand = ReactiveCommand.Create(async () =>
            {
                OpenFolderDialog folderDialog = new()
                {
                    Directory = ResolvePath()
                };
                if (MainWindow.Instance is not null)
                {
                    string? result = await folderDialog.ShowAsync(MainWindow.Instance);

                    if (result != string.Empty)
                    {
                        FolderPathText = result;
                        if (result != null)
                        {
                            await Task.Run(() => GetFiles(result));
                        }
                    }
                }
            });

            BackupFolderDialogCommand = ReactiveCommand.Create(async () =>
            {
                OpenFileDialog fileDialog = new OpenFileDialog();
                fileDialog.Directory = backupPath;
                if (MainWindow.Instance is not null)
                {
                    await Task.Run(() => fileDialog.ShowAsync(MainWindow.Instance));
                }
            });
        }

        public ICommand CopyCommand { get; }
        public ICommand SelectFolderDialogCommand { get; }
        public ICommand BackupFolderDialogCommand { get; }

        public string? FolderPathText
        {
            get => folderPathText;
            set => this.RaiseAndSetIfChanged(ref folderPathText, value);
        }

        public int? ProgressValue
        {
            get => progressValue;
            set => this.RaiseAndSetIfChanged(ref progressValue, value);
        }

        public AvaloniaList<object>? ToSelectedItems { get; set; }
        private AvaloniaList<Character> CopyFromCollection { get; set; } = new AvaloniaList<Character>();
        private AvaloniaList<Character> CopyToCollection { get; set; } = new AvaloniaList<Character>();
        private Character? FromSelectedItem { get; set; }
        private bool TakeBackup { get; set; }

        public async Task CopyCharacterFiles()
        {
            //Copy character files
            string dateNow = DateTime.Now.ToString("dd-MM-yyyy-(hh-mm-ss)");
            string settingsBackup = $"backup/settings_Backup{dateNow}";
            ProgressValue = 0;
            if (backupPath != null && ToSelectedItems != null && FromSelectedItem != null)
            {
                foreach (var (item, character) in from Character? item in ToSelectedItems
                                                  let character = FromSelectedItem
                                                  select (item, character))
                {
                    if (character.CharacterName != item.CharacterName)
                    {
                        string? fileName = Path.GetFileName(item.CharacterFilePath);
                        if (fileName != null)
                        {
                            await Task.Run(() =>
                            {
                                if (TakeBackup)
                                {
                                    DirectoryInfo? backUpDirectory = Directory.CreateDirectory(Path.Combine(path1: backupPath,
                                                                            path2: settingsBackup));
                                    string? backupFilePath = System.IO.Path.Combine(backUpDirectory.FullName, fileName);
                                    try
                                    {
                                        if (item.CharacterFilePath != null)
                                            File.Copy(sourceFileName: item.CharacterFilePath,
                                                       backupFilePath,
                                                       true);
                                    }
                                    catch (IOException copyError)
                                    {
                                        Debug.WriteLine(copyError.Message);
                                    }
                                }
                                try
                                {
                                    if (character.CharacterFilePath != null && item.CharacterFilePath != null)
                                        File.Copy(character.CharacterFilePath, item.CharacterFilePath, true);
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
                    ProgressValue += 102 / ToSelectedItems.Count;
                    await Task.Delay(TimeSpan.FromMilliseconds(300));
                }
            }
        }

        public async Task GetFiles(string dir)
        {
            await Task.Run(async () =>
            {
                //Gets character files from folder
                ClearCollection();
                string[] characterFiles = Directory.GetFiles(dir);
                foreach (string characterFilePath in characterFiles)
                {
                    //Looks for core_char files
                    string characterFile = System.IO.Path.GetFileName(characterFilePath);
                    if (characterFile.StartsWith("core_char_"))
                    {
                        string characterID = PathToID(characterFile);
                        if (!string.IsNullOrEmpty(characterID))
                        {
                            if (characterID.All(char.IsDigit))
                            {
                                if (characterID != "_")
                                {
                                    await GetCharacter(characterID, characterFilePath);
                                }
                            }
                            else
                            {
                                Debug.WriteLine("No id found, skipped.");
                            }
                        }
                        else
                        {
                            Debug.WriteLine("No id found, skipped.");
                        }
                    }
                }
            });
        }

        public string ResolvePath()
        {
            //Navigates to ccp\eve in localappdata
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string eveFolder = $"{localData}\\CCP\\EVE\\";
            string[] eveFolderList = Directory.GetDirectories(eveFolder);
            foreach (string eveFolderItem in eveFolderList)
            {
                //Looks for tranquility foldername varies by eve install location
                if (eveFolderItem.EndsWith("_eve_sharedcache_tq_tranquility"))
                {
                    string tranqFolder = Path.Combine(eveFolder,
                                                      eveFolderItem);
                    string[] tranqFolderList = Directory.GetDirectories(tranqFolder);
                    backupPath = tranqFolder;
                    foreach (string tranqFolderItem in tranqFolderList)
                    {
                        //Looks for settings_Default folder
                        if (tranqFolderItem.EndsWith("Default"))
                        {
                            string settingsPath = Path.Combine(tranqFolder,
                                                               tranqFolderItem);
                            return settingsPath;
                        }
                    }
                    //If default forlder not found direct to tranq folder
                    return tranqFolder;
                }
            }
            //If neither default or tranq folder not found return eve folder
            return eveFolder;
        }

        private static async Task<dynamic?> JsonHandler(string url)
        {
            HttpClient client = new HttpClient();
            try
            {
                Debug.WriteLine(url);
                HttpResponseMessage response = await client.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();
                dynamic? jsonToObject = JsonConvert.DeserializeObject(responseBody);
                return jsonToObject;
            }
            catch (System.Net.WebException e)
            {
                Debug.WriteLine($"Failed to connect ESI'{e}'");
                return null;
            }
        }

        private static string PathToID(string filePath)
        {
            //Parses filepath to id
            string fileName = System.IO.Path.GetFileName(filePath);
            string pattern = "[0-9]+";
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
            dynamic? json = await JsonHandler($"https://esi.evetech.net/latest/characters/{characterid}/?datasource=tranquility");
            if (json.error != "Character has been deleted!")
            {
                string? characterName = json.name;
                Character character = new()
                {
                    CharacterName = characterName,
                    CharacterId = characterid,
                    CharacterFilePath = characterfilepath,
                    CharacterPotrait = await LoadPotrait(characterid)
                };
                AddCharacter(character);
            }
        }
    }
}