using System;
using System.Collections.Generic;
using System.Linq;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Service.Base
{
    public abstract class EntityBase
    {
        protected IEnumerable<IEntity> GetAllEntities(IRootDeviceInterface rootDeviceInterface)
        {
            var devices = GetAllDevices(rootDeviceInterface);

            if (!devices?.Any() ?? true)
            {
                return null;
            }

            var services = GetAllServices(rootDeviceInterface);

            if (!services?.Any() ?? true)
            {
                return devices;
            }
            else
            {
                return devices.Concat(services.Select(s => s as IEntity));
            }
        }

        protected IEnumerable<IEntity> GetEntities(
            IRootDeviceInterface rootDeviceInterface,
            IMSearch mSearchReq)
        {
            IEnumerable<IEntity> entities;

            switch (mSearchReq.ST.StSearchType)
            {
                case STType.All:
                    entities = GetAllEntities(rootDeviceInterface);
                    break;
                case STType.RootDeviceSearch:
                    entities = new List<IEntity>
                    {
                        rootDeviceInterface.RootDeviceConfiguration
                    };
                    break;
                case STType.UIIDSearch:
                    entities = GetAllDevices(rootDeviceInterface)?
                        .Where(d => d.DeviceUUID == mSearchReq.ST.DeviceUUID);
                    break;
                case STType.ServiceTypeSearch:
                case STType.DomainServiceSearch:
                    entities = ServiceEntitiesMatchingSearch(rootDeviceInterface, mSearchReq);
                    break;
                case STType.DeviceTypeSearch:
                case STType.DomainDeviceSearch:
                    entities = DevicesEntitiesMatchingSearch(rootDeviceInterface, mSearchReq);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!entities?.Any() ?? false)
            {
                return null;
            }

            return entities;
        }

        private IEnumerable<IEntity> DevicesEntitiesMatchingSearch(IRootDeviceInterface rootDeviceInterface, IMSearch mSearchReq) => 
                GetAllDevices(rootDeviceInterface)?
                    .Where(s =>
                    {
                        if (mSearchReq.ST.StSearchType == STType.DeviceTypeSearch
                            || mSearchReq.ST.StSearchType == STType.All)
                        {
                            return true;
                        }

                        if (mSearchReq.ST.StSearchType == STType.DomainServiceSearch)
                        {
                            return s.Domain == mSearchReq.ST.Domain;
                        }

                        return false;
                    })
                    .Where(s => s.Version <= mSearchReq.ST.Version)
                    .Append(rootDeviceInterface.RootDeviceConfiguration);


        private IEnumerable<IEntity> ServiceEntitiesMatchingSearch(IRootDeviceInterface rootDeviceInterface, IMSearch mSearchReq) =>
            GetAllServices(rootDeviceInterface)?
                .Where(s =>
                {
                    if (mSearchReq.ST.StSearchType == STType.ServiceTypeSearch
                        || mSearchReq.ST.StSearchType == STType.All)
                    {
                        return true;
                    }

                    if (mSearchReq.ST.StSearchType == STType.DomainServiceSearch)
                    {
                        return s.Domain == mSearchReq.ST.Domain;
                    }

                    return false;
                })
                .Where(s => s.Version <= mSearchReq.ST.Version);

        protected IEnumerable<IServiceConfiguration> GetAllServices(IRootDeviceInterface rootDeviceInterface)
        {
            if (rootDeviceInterface is null)
            {
                return null;
            }

            if (!rootDeviceInterface.RootDeviceConfiguration?.EmbeddedDevices?.Any() ?? true)
            {
                return rootDeviceInterface?.RootDeviceConfiguration?.Services;
            }
            else
            {
                return rootDeviceInterface?.RootDeviceConfiguration?.Services
                    .Concat(rootDeviceInterface.RootDeviceConfiguration?.EmbeddedDevices?
                        .SelectMany(embeddedDevice => embeddedDevice?.Services));
            }
        }

        protected IEnumerable<IDeviceConfiguration> GetAllDevices(IRootDeviceInterface rootDeviceInterface)
        {
            if (rootDeviceInterface is null)
            {
                return null;
            }

            if (!rootDeviceInterface.RootDeviceConfiguration?.EmbeddedDevices?.Any() ?? true)
            {
                var deviceList = new List<IDeviceConfiguration>
                {
                    rootDeviceInterface.RootDeviceConfiguration
                };

                return deviceList;
            }
            else
            {
                return rootDeviceInterface?.RootDeviceConfiguration?.EmbeddedDevices
                    .Append(rootDeviceInterface.RootDeviceConfiguration);
            }
        }
    }
}
