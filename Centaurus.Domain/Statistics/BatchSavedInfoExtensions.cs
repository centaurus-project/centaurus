﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class BatchSavedInfoExtensions
    {
        public static Centaurus.Models.BatchSavedInfo ToBatchSavedInfoModel(this BatchSavedInfo batchSavedInfo)
        {
            if (batchSavedInfo == null)
                throw new ArgumentNullException(nameof(batchSavedInfo));

            return new Centaurus.Models.BatchSavedInfo
            {
                EffectsCount = batchSavedInfo.EffectsCount,
                QuantaCount = batchSavedInfo.QuantaCount,
                ElapsedMilliseconds = batchSavedInfo.ElapsedMilliseconds,
                SavedAt = batchSavedInfo.SavedAt.Ticks
            };
        }

        public static BatchSavedInfo FromModel(this Centaurus.Models.BatchSavedInfo batchSavedInfo)
        {
            if (batchSavedInfo == null)
                throw new ArgumentNullException(nameof(batchSavedInfo));

            return new BatchSavedInfo
            {
                EffectsCount = batchSavedInfo.EffectsCount,
                QuantaCount = batchSavedInfo.QuantaCount,
                ElapsedMilliseconds = batchSavedInfo.ElapsedMilliseconds,
                SavedAt = new DateTime(batchSavedInfo.SavedAt, DateTimeKind.Utc)
            };
        }
    }
}
