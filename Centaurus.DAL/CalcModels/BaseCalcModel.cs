using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL
{
    public abstract class BaseCalcModel
    {
        private bool isInserted;
        public bool IsInserted 
        {
            get
            {
                return isInserted;
            }
            set
            {
                if (IsDeleted && value)
                    IsDeleted = false;
                isInserted = value;
            }
        }

        private bool isDeleted;
        public bool IsDeleted
        {
            get
            {
                return isDeleted;
            }
            set
            {
                if (IsInserted && value)
                    IsInserted = false;
                isDeleted = value;
            }
        }
    }
}
