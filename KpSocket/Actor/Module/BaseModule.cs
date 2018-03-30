using System;
using System.Collections.Generic;
using System.Text;

namespace KpSocket.Actor.Module
{
    public abstract class BaseModule : IModule
    {
        public bool Enable
        {
            get;
            set;
        } = true;

        public string Name
        {
            get { return this.GetType().Name; }
        }
    }
}