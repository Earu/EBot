﻿using Energize.Interfaces.Services;

namespace Energize.Services
{
    public class Service : IService
    {
        public Service(string name, IServiceImplementation inst)
            => this.Instance = inst;

        public IServiceImplementation Instance { get; }
        public string Name { get; }
    }
}
