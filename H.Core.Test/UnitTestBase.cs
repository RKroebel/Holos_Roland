﻿using H.Core.Models;
using H.Core.Models.Animals;
using H.Core.Models.Animals.Beef;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using H.Core.Emissions.Results;
using H.Core.Enumerations;
using H.Core.Models.Animals.Dairy;
using H.Core.Models.Infrastructure;
using H.Core.Models.LandManagement.Fields;
using H.Core.Providers.Animals;
using H.Core.Providers.Climate;
using H.Core.Services;
using H.Core.Services.Animals;
using Moq;
using H.Core.Calculators.Carbon;
using H.Core.Calculators.Nitrogen;
using H.Core.Providers;
using H.Core.Services.LandManagement;

namespace H.Core.Test
{
    public abstract class UnitTestBase
    {
        #region Fields

        protected Mock<IFarmResultsService> _mockFarmResultService;
        protected IFarmResultsService _mockFarmResultServiceObject;
        protected Mock<IManureService> _mockManureService;
        protected IManureService _mockManureServiceObject;
        protected Mock<IClimateProvider> _mockClimateProvider;
        protected IClimateProvider _mockClimateProviderObject;
        protected Mock<IAnimalEmissionFactorsProvider> _mockEmissionDataProvider;
        protected IAnimalEmissionFactorsProvider _mockEmissionDataProviderObject;
        protected Mock<IAnimalAmmoniaEmissionFactorProvider> _mockAnimalAmmoniaEmissionFactorProvider;
        protected IAnimalAmmoniaEmissionFactorProvider _mockAnimalAmmoniaEmissionFactorProviderObject;
        protected ClimateProvider _climateProvider;
        protected ICBMSoilCarbonCalculator _iCbmSoilCarbonCalculator;
        protected N2OEmissionFactorCalculator _n2OEmissionFactorCalculator;
        protected IPCCTier2SoilCarbonCalculator _ipcc;
        protected IFieldResultsService _fieldResultsService;
        protected Mock<ISlcClimateProvider> _slcClimateProvider;

        #endregion

        #region Constructors

        static UnitTestBase()
        {
        }

        protected UnitTestBase()
        {
            _mockFarmResultService = new Mock<IFarmResultsService>();
            _mockFarmResultServiceObject = _mockFarmResultService.Object;

            _mockManureService = new Mock<IManureService>();
            _mockManureServiceObject = _mockManureService.Object;

            _mockClimateProvider = new Mock<IClimateProvider>();
            _mockClimateProviderObject = _mockClimateProvider.Object;

            _mockEmissionDataProvider = new Mock<IAnimalEmissionFactorsProvider>();
            _mockEmissionDataProviderObject = _mockEmissionDataProvider.Object;

            _mockAnimalAmmoniaEmissionFactorProvider = new Mock<IAnimalAmmoniaEmissionFactorProvider>();
            _mockAnimalAmmoniaEmissionFactorProviderObject = _mockAnimalAmmoniaEmissionFactorProvider.Object;

            _slcClimateProvider = new Mock<ISlcClimateProvider>();
            _climateProvider = new ClimateProvider(_slcClimateProvider.Object);
            _n2OEmissionFactorCalculator = new N2OEmissionFactorCalculator(_climateProvider);
            _iCbmSoilCarbonCalculator = new ICBMSoilCarbonCalculator(_climateProvider, _n2OEmissionFactorCalculator);
            _ipcc = new IPCCTier2SoilCarbonCalculator(_climateProvider, _n2OEmissionFactorCalculator);

            _fieldResultsService = new Mock<IFieldResultsService>().Object;
        }

        #endregion

        #region Public Methods
        public Storage InitializeStorage()
        {
            var storage = new Storage
            {
                ApplicationData = new ApplicationData
                {
                    GlobalSettings = new GlobalSettings
                    {
                        ActiveFarm = new Farm()
                    }
                }
            };

            return storage;
        }

        public DigestateApplicationViewItem GetTestRawDigestateApplicationViewItem()
        {
            var digestateApplication = new DigestateApplicationViewItem();

            digestateApplication.DateCreated = DateTime.Now.AddDays(1);
            digestateApplication.DigestateState = DigestateState.Raw;
            digestateApplication.MaximumAmountOfDigestateAvailablePerHectare = 100;
            digestateApplication.AmountAppliedPerHectare = 50;
            digestateApplication.AmountOfNitrogenAppliedPerHectare = 50;

            return digestateApplication;
        }

        public DigestateApplicationViewItem GetTestLiquidDigestateApplicationViewItem()
        {
            var digestateApplication = new DigestateApplicationViewItem();

            digestateApplication.DateCreated = DateTime.Now.AddDays(1);
            digestateApplication.DigestateState = DigestateState.LiquidPhase;
            digestateApplication.MaximumAmountOfDigestateAvailablePerHectare = 100;
            digestateApplication.AmountAppliedPerHectare = 50;
            digestateApplication.AmountOfNitrogenAppliedPerHectare = 500;

            return digestateApplication;
        }

        public AnaerobicDigestionComponent GetTestAnaerobicDigestionComponent()
        {
            var component = new AnaerobicDigestionComponent();

            return component;
        }

        public Farm GetTestFarm()
        {
            var farm = new Farm();
            var backgroundingComponent = new BackgroundingComponent();
            var group = new AnimalGroup();
            @group.GroupType = AnimalType.BeefBackgrounderHeifer;

            var dairyComponent = new DairyComponent();
            var cowsGroup = new AnimalGroup();
            cowsGroup.GroupType = AnimalType.DairyLactatingCow;

            var managementPeriod = new ManagementPeriod();
            managementPeriod.Start = DateTime.Now;
            managementPeriod.End = managementPeriod.Start.AddDays(30 * 2);

            managementPeriod.ManureDetails.StateType = ManureStateType.AnaerobicDigester;

            farm.Components.Add(backgroundingComponent);
            farm.Components.Add(dairyComponent);

            backgroundingComponent.Groups.Add(group);
            dairyComponent.Groups.Add(cowsGroup);

            group.ManagementPeriods.Add(managementPeriod);

            /*
             * Manure exports
             */

            farm.ManureExportViewItems.Add(new ManureExportViewItem() { DateOfExport = DateTime.Now, Amount = 1000, AnimalType = AnimalType.Dairy });
            farm.ManureExportViewItems.Add(new ManureExportViewItem() { DateOfExport = DateTime.Now, Amount = 2000, AnimalType = AnimalType.Dairy });

            return farm;
        }

        public FieldSystemDetailsStageState GetFieldStageState()
        {
            var stageState = new FieldSystemDetailsStageState();

            stageState.DetailsScreenViewCropViewItems = new ObservableCollection<CropViewItem>()
            {
                new CropViewItem()
                {
                    Year = 2023,
                }
            };

            return stageState;
        }

        public AnimalComponentBase GetTestGrazingAnimalComponent(FieldSystemComponent fieldSystemComponent)
        {
            var component = new BackgroundingComponent();

            var group = new AnimalGroup();

            component.Groups.Add(group);

            var managementPeriod = new ManagementPeriod();
            group.ManagementPeriods.Add(managementPeriod);

            managementPeriod.HousingDetails = new HousingDetails();
            managementPeriod.HousingDetails.HousingType = HousingType.Pasture;
            managementPeriod.HousingDetails.PastureLocation = fieldSystemComponent;

            return component;
        }

        public AnimalComponentEmissionsResults GetEmptyTestAnimalComponentEmissionsResults()
        {
            var results = new AnimalComponentEmissionsResults();

            var monthsAndDaysData = new MonthsAndDaysData();

            var managementPeriod = new ManagementPeriod();
            managementPeriod.HousingDetails = new HousingDetails();
            monthsAndDaysData.ManagementPeriod = managementPeriod;

            var groupEmissionsByDay = new GroupEmissionsByDay();

            var groupEmissionsByMonth = new GroupEmissionsByMonth(monthsAndDaysData, new List<GroupEmissionsByDay>() { groupEmissionsByDay });

            var animalGroupResults = new AnimalGroupEmissionResults();
            animalGroupResults.GroupEmissionsByMonths = new List<GroupEmissionsByMonth>() { groupEmissionsByMonth };

            results.EmissionResultsForAllAnimalGroupsInComponent = new List<AnimalGroupEmissionResults>() { animalGroupResults };

            return results;
        }

        public AnimalComponentEmissionsResults GetNonEmptyTestBeefCattleAnimalComponentEmissionsResults()
        {
            var results = new AnimalComponentEmissionsResults();
            results.Component = new BackgroundingComponent();

            var monthsAndDaysData = new MonthsAndDaysData();
            monthsAndDaysData.Year = DateTime.Now.Year;

            var managementPeriod = new ManagementPeriod();
            managementPeriod.HousingDetails = new HousingDetails();
            managementPeriod.HousingDetails.HousingType = HousingType.Confined;
            monthsAndDaysData.ManagementPeriod = managementPeriod;

            managementPeriod.ManureDetails.StateType = ManureStateType.AnaerobicDigester;

            var groupEmissionsByDay = new GroupEmissionsByDay()
            {
                AdjustedAmountOfTanInStoredManureOnDay = 100,
                OrganicNitrogenCreatedOnDay = 50,
                TotalVolumeOfManureAvailableForLandApplication = 100,
            };

            var groupEmissionsByMonth = new GroupEmissionsByMonth(monthsAndDaysData, new List<GroupEmissionsByDay>() { groupEmissionsByDay });

            var animalGroupResults = new AnimalGroupEmissionResults();
            animalGroupResults.GroupEmissionsByMonths = new List<GroupEmissionsByMonth>() { groupEmissionsByMonth };

            results.EmissionResultsForAllAnimalGroupsInComponent = new List<AnimalGroupEmissionResults>() { animalGroupResults };

            return results;
        }

        public AnimalComponentEmissionsResults GetTestGrazingBeefCattleAnimalComponentEmissionsResults(FieldSystemComponent fieldSystemComponent)
        {
            var results = new AnimalComponentEmissionsResults();
            results.Component = this.GetTestGrazingAnimalComponent(fieldSystemComponent);

            var animalGroupResults = this.GetTestGrazingBeefCattleAnimalGroupComponentEmissionsResults(fieldSystemComponent);

            results.EmissionResultsForAllAnimalGroupsInComponent = new List<AnimalGroupEmissionResults>() { animalGroupResults };

            return results;
        }

        public AnimalGroupEmissionResults GetTestGrazingBeefCattleAnimalGroupComponentEmissionsResults(FieldSystemComponent fieldSystemComponent)
        {
            var animalGroupResults = new AnimalGroupEmissionResults();

            var groupEmissionsByMonth = this.GetTestGroupEmissionsByMonthForGrazingAnimals(fieldSystemComponent);
            animalGroupResults.GroupEmissionsByMonths = new List<GroupEmissionsByMonth>() { groupEmissionsByMonth };

            return animalGroupResults;
        }

        public GroupEmissionsByMonth GetTestGroupEmissionsByMonthForGrazingAnimals(FieldSystemComponent fieldSystemComponent)
        {
            var monthsAndDaysData = new MonthsAndDaysData();
            monthsAndDaysData.Year = DateTime.Now.Year;

            var managementPeriod = new ManagementPeriod();
            managementPeriod.HousingDetails = new HousingDetails();
            managementPeriod.HousingDetails.HousingType = HousingType.Pasture;
            managementPeriod.HousingDetails.PastureLocation = fieldSystemComponent;

            monthsAndDaysData.ManagementPeriod = managementPeriod;

            managementPeriod.ManureDetails.StateType = ManureStateType.Pasture;

            var groupEmissionsByDay = new GroupEmissionsByDay()
            {
                AdjustedAmountOfTanInStoredManureOnDay = 100,
                OrganicNitrogenCreatedOnDay = 50,
                TotalVolumeOfManureAvailableForLandApplication = 100,
                TotalCarbonUptakeForGroup = 100,
            };

            var groupEmissionsByMonth = new GroupEmissionsByMonth(monthsAndDaysData, new List<GroupEmissionsByDay>() { groupEmissionsByDay });

            return groupEmissionsByMonth;
        }

        public AnimalComponentEmissionsResults GetNonEmptyTestDairyCattleAnimalComponentEmissionsResults()
        {
            var results = new AnimalComponentEmissionsResults();
            results.Component = new DairyComponent();

            var monthsAndDaysData = new MonthsAndDaysData();
            monthsAndDaysData.Year = DateTime.Now.Year;

            var managementPeriod = new ManagementPeriod();
            managementPeriod.HousingDetails = new HousingDetails();
            monthsAndDaysData.ManagementPeriod = managementPeriod;

            var groupEmissionsByDay = new GroupEmissionsByDay()
            {
                AdjustedAmountOfTanInStoredManureOnDay = 200,
                OrganicNitrogenCreatedOnDay = 60,
                TotalVolumeOfManureAvailableForLandApplication = 500,
            };

            var groupEmissionsByMonth = new GroupEmissionsByMonth(monthsAndDaysData, new List<GroupEmissionsByDay>() { groupEmissionsByDay });

            var animalGroupResults = new AnimalGroupEmissionResults();
            animalGroupResults.GroupEmissionsByMonths = new List<GroupEmissionsByMonth>() { groupEmissionsByMonth };

            results.EmissionResultsForAllAnimalGroupsInComponent = new List<AnimalGroupEmissionResults>() { animalGroupResults };

            return results;
        }

        public FieldSystemComponent GetTestFieldComponent()
        {
            var component = new FieldSystemComponent();

            var viewItem = new CropViewItem();
            viewItem.CropType = CropType.Wheat;
            viewItem.Year = DateTime.Now.Year;

            component.CropViewItems.Add(viewItem);

            return component;
        }

        public CropViewItem GetTestCropViewItem()
        {
            var cropViewItem = new CropViewItem();
            cropViewItem.Area = 1;
            cropViewItem.Year = DateTime.Now.Year;

            cropViewItem.ManureApplicationViewItems = new ObservableCollection<ManureApplicationViewItem>();
            cropViewItem.ManureApplicationViewItems.Add(this.GetTestBeefCattleManureApplicationViewItemUsingOnLivestockManure());

            return cropViewItem;
        }

        public ManureApplicationViewItem GetTestBeefCattleManureApplicationViewItemUsingOnLivestockManure()
        {
            var manureApplicationViewItem = new ManureApplicationViewItem();
            manureApplicationViewItem.ManureLocationSourceType = ManureLocationSourceType.Livestock;
            manureApplicationViewItem.AnimalType = AnimalType.BeefBackgrounderHeifer;
            manureApplicationViewItem.AmountOfManureAppliedPerHectare = 50;
            manureApplicationViewItem.AmountOfNitrogenAppliedPerHectare = 100;

            return manureApplicationViewItem;
        }

        public ManureApplicationViewItem GetTestBeefCattleManureApplicationViewItemUsingImportedManure()
        {
            var manureApplicationViewItem = new ManureApplicationViewItem();
            manureApplicationViewItem.ManureLocationSourceType = ManureLocationSourceType.Imported;
            manureApplicationViewItem.AnimalType = AnimalType.BeefBackgrounderHeifer;
            manureApplicationViewItem.AmountOfManureAppliedPerHectare = 50;
            manureApplicationViewItem.AmountOfNitrogenAppliedPerHectare = 50;

            return manureApplicationViewItem;
        }

        public ManureApplicationViewItem GetTestDairyCattleManureApplicationViewItemUsingImportedManure()
        {
            var manureApplicationViewItem = new ManureApplicationViewItem();
            manureApplicationViewItem.ManureLocationSourceType = ManureLocationSourceType.Imported;
            manureApplicationViewItem.AnimalType = AnimalType.DairyHeifers;
            manureApplicationViewItem.AmountOfManureAppliedPerHectare = 333;
            manureApplicationViewItem.AmountOfNitrogenAppliedPerHectare = 50;

            return manureApplicationViewItem;
        }

        #endregion
    }
}