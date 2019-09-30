using Centaurus.Models;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{

    public abstract class StateManager
    {

        private ApplicationState state;
        public virtual ApplicationState State
        {
            get
            {
                return state;
            }
            set
            {
                if (state != value)
                    lock (this)
                    {
                        if (state != value)
                        {
                            state = value;
                            StateChanged?.Invoke(this, state);
                        }
                    }
            }
        }

        public event EventHandler<ApplicationState> StateChanged;
    }
}
