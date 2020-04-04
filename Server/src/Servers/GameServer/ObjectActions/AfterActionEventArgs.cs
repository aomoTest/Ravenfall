﻿using Shinobytes.Ravenfall.RavenNet.Models;
using System;

namespace GameServer.ObjectActions
{
    public class AfterActionEventArgs : EventArgs
    {
        public Player Player { get; }
        public SceneObject Object { get; }
        public AfterActionEventArgs(Player player, SceneObject obj)
        {
            this.Player = player;
            this.Object = obj;
        }
    }
}