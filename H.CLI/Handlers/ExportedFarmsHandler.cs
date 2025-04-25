﻿using H.CLI.ComponentKeys;
using H.CLI.Converters;
using H.CLI.FileAndDirectoryAccessors;
using H.CLI.Processors;
using H.CLI.TemporaryComponentStorage;
using H.CLI.UserInput;
using H.Core;
using H.Core.Calculators.Carbon;
using H.Core.Calculators.Nitrogen;
using H.Core.Enumerations;
using H.Core.Models;
using H.Core.Models.LandManagement.Fields;
using H.Core.Providers;
using H.Core.Providers.Climate;
using H.Core.Services.LandManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using H.Core.Services;
using H.Core.Services.Initialization;

namespace H.CLI.Handlers
{
    public class ExportedFarmsHandler
    {
        #region Fields

        private readonly InputHelper _inputHelper = new InputHelper();
        private readonly Storage _storage;
        private readonly FieldProcessor _fieldProcessor;
        private readonly DirectoryHandler _directoryHandler = new DirectoryHandler();
        private readonly ExcelInitializer _excelInitializer = new ExcelInitializer();
        private readonly DirectoryKeys _directoryKeys = new DirectoryKeys();
        private readonly SettingsHandler _settingsHandler;

        private readonly BeefConverter _beefConverter = new BeefConverter();
        private readonly DairyConverter _dairyConverter = new DairyConverter();
        private readonly SwineConverter _swineConverter = new SwineConverter();
        private readonly SheepConverter _sheepConverter = new SheepConverter();
        private readonly PoultryConverter _poultryConverter = new PoultryConverter();
        private readonly OtherLiveStockConverter _otherLiveStockConverter = new OtherLiveStockConverter();


        public string pathToExportedFarm = string.Empty;
        private FieldResultsService _fieldResultsService;

        #endregion

        public ExportedFarmsHandler(FieldResultsService fieldResultsService, IClimateProvider climateProvider, Storage storage)
        {
            if (fieldResultsService != null)
            {
                _fieldResultsService = fieldResultsService; 
            }
            else
            {
                throw new ArgumentNullException(nameof(fieldResultsService));
            }

            if (climateProvider != null)
            {
                _settingsHandler = new SettingsHandler(climateProvider);
            }
            else
            {
                throw new ArgumentNullException(nameof(climateProvider));
            }

            if (storage != null)
            {
                _storage = storage;
            }
            else
            {
                throw new ArgumentNullException(nameof(storage));
            }
            
            _fieldProcessor = new FieldProcessor(fieldResultsService);
        }

        #region Public Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="farmsFolderPath">The root directory that will contain all of the farms</param>
        public List<Farm> Initialize(string farmsFolderPath)
        {
            var pathToExportedFarms = this.PromptUserForLocationOfExportedFarms(farmsFolderPath);
            if (string.IsNullOrWhiteSpace(pathToExportedFarms))
            {
                // User specified no farms to import
                return new List<Farm>();
            }

            var farms = this.GetExportedFarmsFromUserSpecifiedLocation(pathToExportedFarms);

            var inputFilesForAllFarms = new List<string>();
            foreach (var farm in farms)
            {
                // Create input files for all components in farm
                var componentFilesForFarm = this.CreateInputFilesForFarm(farmsFolderPath, farm, null);

                inputFilesForAllFarms.AddRange(componentFilesForFarm);
            }

            Console.WriteLine();
            Console.WriteLine(string.Format(Properties.Resources.InterpolatedTotalFarmsSuccessfullyImported, farms.Count));
            Console.WriteLine(string.Format(Properties.Resources.LabelInputFilesHaveBeenCreatedAndStoredInYourFarmsDirectory, inputFilesForAllFarms.Count));

            return farms;
        }

        public List<string> InitializeWithCLArguements(string farmsFolderPath, CLIArguments argValues)
        {
            var files = Directory.GetFiles(farmsFolderPath);
            var directories = Directory.GetDirectories(farmsFolderPath);
            List<string> generatedFarmFolders = new List<string>();
            string path = string.Empty;

            // If using input folder
            if (argValues.FolderName != string.Empty)
            {
                foreach (var directory in directories)
                {
                    if (argValues.FolderName == Path.GetFileName(directory))
                    {
                        path = directory;
                        argValues.IsFolderNameFound = true;
                        break;
                    }
                }
                if (argValues.IsFolderNameFound)
                {
                    var farms = GetExportedFarmsFromUserSpecifiedLocation(path);

                    foreach (var farm in farms)
                    {
                        if (argValues.PolygonID != "" || argValues.PolygonID != string.Empty)
                        {
                            ChangePolygonID(argValues, farm);
                        }

                        _ = this.CreateInputFilesForFarm(farmsFolderPath, farm, argValues);
                        generatedFarmFolders.Add(farmsFolderPath + @"\" + farm.Name);
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(Properties.Resources.InputFileNotFound, argValues.FolderName);
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

            // If using input file
            if (argValues.FileName != string.Empty)
            {
                // Check files for input farm
                foreach (var file in files)
                {
                    if (argValues.FileName == Path.GetFileName(file))
                    {
                        path = file;
                        argValues.IsFileNameFound = true;
                        break;
                    }
                }
                if (argValues.IsFileNameFound)
                {
                    var farms = _storage.GetFarmsFromExportFile(path);
                    var farmsList = farms.ToList();
                    var exportedFarm = farmsList[0];

                    // PolygonID for climate configuration
                    if (argValues.PolygonID != "" || argValues.PolygonID != string.Empty)
                    {
                        ChangePolygonID(argValues, exportedFarm);
                    }

                    _ = this.CreateInputFilesForFarm(farmsFolderPath, exportedFarm, argValues);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(Properties.Resources.InputFileNotFound, argValues.FileName);
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

            return generatedFarmFolders;
        }

        public void ChangePolygonID(CLIArguments argValues, Farm exportedFarm)
        {
            var polygonID = int.Parse(argValues.PolygonID);
            var geographicDataProvider = new GeographicDataProvider();
            geographicDataProvider.Initialize();
            _settingsHandler.InitializePolygonIDList(geographicDataProvider);

            if (_settingsHandler.PolygonIDList.Contains(polygonID))
            {
                var slcClimateDataProvider = new SlcClimateDataProvider();
                exportedFarm.PolygonId = polygonID;
                exportedFarm.GeographicData = geographicDataProvider.GetGeographicalData(polygonID);
                exportedFarm.ClimateData = slcClimateDataProvider.GetClimateData(polygonID, TimeFrame.NineteenNinetyToTwoThousandSeventeen);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(String.Format(Properties.Resources.NotAValidPolygonID, argValues.PolygonID.ToString()));
                throw new Exception("Not A Valid Polygon ID");
            }
            Console.ResetColor();
        }

        /// <summary>
        /// Create a list of input files based on the components that make up the farm.
        /// </summary>
        public List<string> CreateInputFilesForFarm(string pathToFarmsDirectory, Farm farm, CLIArguments argValues)
        {
            // A list of all the created input files
            var createdFiles = new List<string>();

            // Create a directory for the farm
            var farmDirectoryPath = this.CreateDirectoryStructureForImportedFarm(pathToFarmsDirectory, farm);
            pathToExportedFarm = farmDirectoryPath;

            Console.WriteLine();
            if (argValues != null && argValues.Settings != "")
            {
                CopyUserSettingsFile(pathToFarmsDirectory, argValues, farmDirectoryPath, farm);
            }
            else
            {
                Console.WriteLine(Properties.Resources.LabelCreatingSettingsFile);
                this.CreateSettingsFileForFarm(farmDirectoryPath, farm);
            }

            /*
             * Field components
             */

            if (farm.FieldSystemComponents.Any())
            {
                Console.WriteLine(Properties.Resources.LabelCreatingFieldInputFiles);

                var pathToFieldComponents = farmDirectoryPath + @"\" + Properties.Resources.DefaultFieldsInputFolder;
                var fieldKeys = new FieldKeys();
                foreach (var fieldSystemComponent in farm.FieldSystemComponents)
                {
                    // The input file that gets built based on the field component
                    string inputFile = string.Empty;

                    if (farm.EnableCarbonModelling == false)
                    {
                        // Single year mode field - create input file based on single year view item
                        inputFile = _fieldProcessor.SetTemplateCSVFileBasedOnExportedField(farm: farm,
                            path: pathToFieldComponents,
                            componentKeys: fieldKeys.Keys,
                            fieldSystemComponent: fieldSystemComponent);
                    }
                    else
                    {
                        var detailViewItemsForField = new List<CropViewItem>();

                        // Multi-year mode field, create input based on multiple detail view items
                        var stageState = farm.StageStates.OfType<FieldSystemDetailsStageState>().SingleOrDefault();
                        if (stageState == null)
                        {
                            _fieldResultsService.InitializeStageState(farm);
                            stageState = _fieldResultsService.GetStageState(farm);
                        }

                        detailViewItemsForField = stageState.DetailsScreenViewCropViewItems.Where(x => x.FieldSystemComponentGuid == fieldSystemComponent.Guid).ToList();

                        inputFile = _fieldProcessor.SetTemplateCSVFileBasedOnExportedField(farm: farm,
                            path: pathToFieldComponents,
                            componentKeys: fieldKeys.Keys,
                            fieldSystemComponent: fieldSystemComponent,
                            detailsScreenViewCropViewItems: detailViewItemsForField);
                    }

                    createdFiles.Add(inputFile);

                    Console.WriteLine($@"{farm.Name}: {fieldSystemComponent.Name}");
                }
            }

            /*
             * Beef
             */

            if (farm.BeefCattleComponents.Any())
            {
                Console.WriteLine(Properties.Resources.LabelCreatingBeefCattleInputFiles);

                var pathToBeefCattleComponents = farmDirectoryPath + @"\" + Properties.Resources.DefaultBeefInputFolder;
                foreach (var beefCattleComponent in farm.BeefCattleComponents)
                {
                    var createdInputFile = _beefConverter.SetTemplateCSVFileBasedOnExportedFarm(
                        path: pathToBeefCattleComponents,
                        component: beefCattleComponent,
                        writeToPath: true);

                    createdFiles.Add(createdInputFile);

                    Console.WriteLine($@"{farm.Name}: {beefCattleComponent.Name}");
                }
            }

            /*
             * Dairy
             */

            if (farm.DairyComponents.Any())
            {
                Console.WriteLine(Properties.Resources.LabelCreatingDairyCattleInputFiles);

                var pathToDairyCattleComponents = farmDirectoryPath + @"\" + Properties.Resources.DefaultDairyInputFolder;
                foreach (var dairyComponent in farm.DairyComponents)
                {
                    var createdInputFile = _dairyConverter.SetTemplateCSVFileBasedOnExportedFarm(
                        path: pathToDairyCattleComponents,
                        component: dairyComponent,
                        writeToPath: true);

                    createdFiles.Add(createdInputFile);

                    Console.WriteLine($@"{farm.Name}: {dairyComponent.Name}");
                }
            }

            /*
             * Swine
             */

            if (farm.SwineComponents.Any())
            {
                Console.WriteLine(Properties.Resources.LabelCreatingSwineInputFiles);

                var pathToSwineComponents = farmDirectoryPath + @"\" + Properties.Resources.DefaultSwineInputFolder;
                foreach (var swineComponent in farm.SwineComponents)
                {
                    var createdInputFile = _swineConverter.SetTemplateCSVFileBasedOnExportedFarm(
                        path: pathToSwineComponents,
                        component: swineComponent,
                        writeToPath: true);

                    createdFiles.Add(createdInputFile);

                    Console.WriteLine($@"{farm.Name}: {swineComponent.Name}");
                }
            }

            /*
             * Sheep
             */

            if (farm.SheepComponents.Any())
            {
                Console.WriteLine(Properties.Resources.LabelCreatingSheepInputFiles);

                var pathToSheepComponents = farmDirectoryPath + @"\" + Properties.Resources.DefaultSheepInputFolder;
                foreach (var sheepComponent in farm.SheepComponents)
                {
                    var createdInputFile = _sheepConverter.SetTemplateCSVFileBasedOnExportedFarm(
                        path: pathToSheepComponents,
                        component: sheepComponent,
                        writeToPath: true);

                    createdFiles.Add(createdInputFile);

                    Console.WriteLine($@"{farm.Name}: {sheepComponent.Name}");
                }
            }

            /*
             * Poultry
             */

            if (farm.PoultryComponents.Any())
            {
                Console.WriteLine(Properties.Resources.LabelCreatingPoultryInputFiles);

                var pathToPoultryComponents = farmDirectoryPath + @"\" + Properties.Resources.DefaultPoultryInputFolder;
                foreach (var poultryComponent in farm.PoultryComponents)
                {
                    var createdInputFile = _poultryConverter.SetTemplateCSVFileBasedOnExportedFarm(
                        path: pathToPoultryComponents,
                        component: poultryComponent,
                        writeToPath: true);

                    createdFiles.Add(createdInputFile);

                    Console.WriteLine($@"{farm.Name}: {poultryComponent.Name}");
                }
            }

            /*
             * Other animals
             */

            if (farm.OtherLivestockComponents.Any())
            {
                Console.WriteLine(Properties.Resources.LabelCreatingOtherAnimalsInputFiles);

                var pathToOtherAnimalComponents = farmDirectoryPath + @"\" + Properties.Resources.DefaultOtherLivestockInputFolder;
                foreach (var otherLivestockComponent in farm.OtherLivestockComponents)
                {
                    var createdInputFile = _otherLiveStockConverter.SetTemplateCSVFileBasedOnExportedFarm(
                        path: pathToOtherAnimalComponents,
                        component: otherLivestockComponent,
                        writeToPath: true);

                    createdFiles.Add(createdInputFile);

                    Console.WriteLine($@"{farm.Name}: {otherLivestockComponent.Name}");
                }
            }

            return createdFiles;
        }

        public void CopyUserSettingsFile(string pathToFarmsDirectory, CLIArguments argValues, string farmDirectoryPath, Farm farm)
        {
            bool isSettingsFileFound = false;
            var files = Directory.GetFiles(pathToFarmsDirectory);
            string filePath = string.Empty;
            foreach (var file in files)
            {
                if (argValues.Settings == Path.GetFileName(file))
                {
                    filePath = file;
                    isSettingsFileFound = true;
                }
            }
            if (isSettingsFileFound)
            {
                string newFilePath = Path.Combine(farmDirectoryPath, Path.GetFileName(filePath));
                File.Copy(filePath, newFilePath);
                Console.WriteLine($"Copying {argValues.Settings} to {farmDirectoryPath}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(Properties.Resources.SettingsFileNotFound, argValues.Settings);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(Properties.Resources.LabelCreatingSettingsFile);
                this.CreateSettingsFileForFarm(farmDirectoryPath, farm);
            }
        }

        public string PromptUserForLocationOfExportedFarms(string farmsFolderPath)
        {
            // Ask the user if they have farms that they would like to import from the GUI (they must have already exported the farms to a .json file)
            Console.WriteLine();
            Console.Write(Properties.Resources.LabelWouldYouLikeToImportFarmsFromTheGui);
            Console.WriteLine(Properties.Resources.LabelYesNo);

            var response = Console.ReadLine();
            if (_inputHelper.IsYesResponse(response))
            {
                // Prompt the user for the location of their exported farms
                Console.WriteLine();
                Console.Write(Properties.Resources.LabelAreYourExportedFarmsInCurrentFarmDirectory);
                Console.WriteLine(Properties.Resources.LabelYesNo);
                var response2 = Console.ReadLine();
                if (_inputHelper.IsYesResponse(response2))
                {
                    return farmsFolderPath;
                }
                Console.WriteLine();
                Console.WriteLine(Properties.Resources.LabelWhatIsThePathToYourExportedFarms);
                var pathToExportedFarms = Console.ReadLine();

                return pathToExportedFarms;
            }
            else
            {
                return string.Empty;
            }
        }

        public Farm GetExportedFarmFromUserSpecifiedLocation(string path)
        {
            return new Farm();
        }

        public List<Farm> GetExportedFarmsFromUserSpecifiedLocation(string path)
        {
            Console.WriteLine();
            Console.WriteLine(Properties.Resources.LabelGettingExportedFarms);
            var farms = _storage.GetExportedFarmsFromDirectoryRecursively(path);

            Console.WriteLine(string.Format(Properties.Resources.InterpolatedTotalNumberOfFarmsFound, farms.Count()));

            return farms.ToList();
        }

        #endregion

        #region Private Methods

        private void CreateSettingsFileForFarm(string farmDirectoryPath, Farm farm)
        {
            // Create a settings file based on the default object found in the imported farm
            _directoryHandler.GenerateGlobalSettingsFile(farmDirectoryPath, farm);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pathToFarmsDirectory"></param>
        /// <param name="farm"></param>
        /// <returns>The full path to the directory created for the farm</returns>
        private string CreateDirectoryStructureForImportedFarm(string pathToFarmsDirectory, Farm farm)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));

            // Remove illegal characters from farm name since it will be used to create a folder.
            var cleanedFarmName = r.Replace(farm.Name, "");

            var farmDirectoryPath = pathToFarmsDirectory + @"\" + cleanedFarmName;
            if (Directory.Exists(farmDirectoryPath))
            {
                // Directory already exists, append timestamp to folder to differentiate between existing folder and new folder
                var timestamp = $"__{DateTime.Now.Month}_{DateTime.Now.Day}_{DateTime.Now.Year}_{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}";

                farmDirectoryPath += timestamp;
            }

            Directory.CreateDirectory(farmDirectoryPath);
            _directoryHandler.ValidateComponentDirectories(farmDirectoryPath);

            return farmDirectoryPath;
        }

        #endregion
    }
}
