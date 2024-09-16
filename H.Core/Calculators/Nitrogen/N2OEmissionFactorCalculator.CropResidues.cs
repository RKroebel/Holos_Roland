﻿using System;
using H.Core.Models;
using H.Core.Models.LandManagement.Fields;

namespace H.Core.Calculators.Nitrogen
{
    public partial class N2OEmissionFactorCalculator
    {
        #region Public Methods
        
        public double CalculateN2OFromCropResidueExports(CropViewItem cropViewItem, Farm farm)
        {
            var emissionFactorForCropResidues = this.GetEmissionFactorForCropResidues(cropViewItem, farm);
            var nitrogenFromExportedCropResidues = _nitrogenCalculator.CalculateCropResidueExportNitrogen(cropViewItem);

            var result = emissionFactorForCropResidues * nitrogenFromExportedCropResidues;

            return result;
        } 

        #endregion
    }
}