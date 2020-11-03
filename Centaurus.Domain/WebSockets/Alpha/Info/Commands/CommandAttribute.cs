using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandAttribute : Attribute
    {
        public CommandAttribute(string commandName)
        {
            Command = commandName;
        }

        public string Command { get; }
    }
}