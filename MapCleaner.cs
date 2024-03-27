using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace DrakiaXYZ.SPTMapCleaner
{
    internal class MapCleaner
    {
        private string _mapFolder;
        private string _assetsFolder;

        private List<string> _errors = new List<string>();
        private Dictionary<string, string> _guidMap = new Dictionary<string, string>();
        private HashSet<string> _processFiles = new HashSet<string>();
        private HashSet<string> _processedFiles = new HashSet<string>();
        private HashSet<string> _saveFiles = new HashSet<string>();
        private Dictionary<string, string> _sceneNames = new Dictionary<string, string>();

        public MapCleaner(string mapFolder)
        {
            _mapFolder = mapFolder;
            _assetsFolder = Path.Join(_mapFolder, "Assets");
            
        }

        public bool RunCleaner()
        {
            if (!ValidateFolder())
            {
                return false;
            }

            BuildMapSceneLookup();
            RenameMapScenes();
            BuildGuidMap();
            ProcessFiles();
            DeleteFiles();
            LogMapScripts();
            ClearScripts();

            return true;
        }

        private void BuildMapSceneLookup()
        {
            // Check if there's a maps.json alongside the exe, used to rename scenes if it exists
            string mapsJsonPath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "maps.json");
            if (File.Exists(mapsJsonPath))
            {
                var mapObject = JsonNode.Parse(File.ReadAllText(mapsJsonPath));
                if (mapObject != null)
                {
                    foreach (var map in mapObject.AsObject())
                    {
                        foreach (var level in map.Value?.AsObject() ?? [])
                        {
                            var sceneFile = level.Value?.ToString() ?? "";
                            sceneFile = sceneFile.Substring(sceneFile.LastIndexOf("/") + 1);
                            _sceneNames[$"{level.Key}.unity"] = sceneFile;
                        }
                    }
                }
            }
        }

        private void RenameMapScenes()
        {
            string scenesFolder = Path.Join(_assetsFolder, "Scenes");
            foreach (var sceneFile in Directory.EnumerateFiles(scenesFolder, "*.*"))
            {
                string lookup = sceneFile.Substring(sceneFile.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                if (lookup.EndsWith(".meta"))
                {
                    lookup = lookup.Substring(0, lookup.Length - 5);
                }

                if (!_sceneNames.ContainsKey(lookup)) continue;

                var newSceneFile = sceneFile.Replace(lookup, _sceneNames[lookup]);
                File.Move(sceneFile, newSceneFile);
            }
        }

        private void BuildGuidMap()
        {
            // First get a map of GUID -> File, so we can look it up while processing references
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var metaFiles = Directory.EnumerateFiles(_assetsFolder, "*.meta", SearchOption.AllDirectories);
            int processedFiles = 0;
            Console.WriteLine($"Processing {metaFiles.Count()} meta files...");
            foreach (var metaFile in metaFiles)
            {
                if (processedFiles % 1000 == 0)
                {
                    Console.Write($"{processedFiles}..");
                }

                var data = deserializer.Deserialize<dynamic>(File.ReadAllText(metaFile));
                _guidMap[data["guid"]] = metaFile.Substring(0, metaFile.Length - 5); // Trim off the .meta ending

                processedFiles++;
            }
            Console.WriteLine();
            Console.WriteLine("Done Processing meta files");
        }

        private void ProcessFiles()
        {
            Console.WriteLine("Processing files...");

            // Add the initial scene files to the _processFiles list
            string scenesFolder = Path.Join(_assetsFolder, "Scenes");
            foreach (var sceneFile in Directory.EnumerateFiles(scenesFolder, "*.unity"))
            {
                _processFiles.Add(sceneFile);
                _saveFiles.Add(sceneFile);
            }

            Regex regex = new Regex("guid: ([a-f0-9]+)");
            while (_processFiles.Count != 0)
            {
                string filePath = _processFiles.First();
                foreach (Match match in regex.Matches(File.ReadAllText(filePath)))
                {
                    string guid = match.Groups[1].Value;
                    string? guidPath = _guidMap.GetValueOrDefault(guid);
                    if (guidPath == null) continue;
                    if (_processedFiles.Contains(guidPath)) continue;

                    _processFiles.Add(guidPath);
                    _saveFiles.Add(guidPath);
                }

                // Remove the head
                _processedFiles.Add(filePath);
                _processFiles.Remove(filePath);
            }

            Console.WriteLine($"Processed {_processedFiles.Count} files");
        }

        private void DeleteFiles()
        {
            Console.WriteLine("Deleting unreferenced files...");
            int deleteCount = 0;
            foreach (var file in Directory.EnumerateFiles(_assetsFolder, "*.*", SearchOption.AllDirectories))
            {
                // Skip anything under "Scripts"
                if (file.ToLower().Contains("scripts")) continue;

                // If file ends in .meta, check for the non-meta version
                string checkFilename = file;
                if (checkFilename.EndsWith(".meta"))
                {
                    checkFilename = checkFilename.Substring(0, checkFilename.Length - 5);
                }

                // If the file is in our keep list, skip
                if (_saveFiles.Contains(checkFilename)) continue;

                File.Delete(file);
                deleteCount++;
            }

            Console.WriteLine($"Deleted {deleteCount} files");
        }

        private void LogMapScripts()
        {
            Console.WriteLine("Map used the following scripts:");

            foreach (var file in _saveFiles)
            {
                if (!file.ToLower().Contains("scripts")) continue;

                Console.WriteLine(file.Substring(_assetsFolder.Length));
            }
        }

        private void ClearScripts()
        {
            Console.WriteLine("Clearing all scripts");

            foreach (var file in Directory.EnumerateFiles(_assetsFolder, "*.cs", SearchOption.AllDirectories))
            {
                File.WriteAllText(file, string.Empty);
            }
        }

        public bool ValidateFolder()
        {
            string projectSettingsFolder = Path.Join(_mapFolder, "ProjectSettings");

            if (!Path.Exists(_assetsFolder))
            {
                _errors.Add("Invalid project folder, missing `Assets`");
                return false;
            }

            if (!Path.Exists(projectSettingsFolder))
            {
                _errors.Add("Invalid project folder, missing `ProjectSettings`");
                return false;
            }

            return true;
        }

        public List<string> GetErrors()
        {
            return _errors;
        }
    }
}
