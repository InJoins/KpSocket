using System;
using System.Collections.Generic;
using System.Text;

namespace KpSocket.Actor.Module
{
    public interface IModule
    {
        bool Enable { get; set; }
        string Name { get; }
    }
}