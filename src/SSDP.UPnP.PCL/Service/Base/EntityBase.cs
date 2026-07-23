using System;
using System.Collections.Generic;
using System.Linq;
using SSDP.UPnP.PCL.Enum;
using SSDP.UPnP.PCL.Interfaces.Model;

namespace SSDP.UPnP.PCL.Service.Base
{
    public abstract class EntityBase
    {
        protected IEnumerable<IEntity> GetAllEntities(IRootDeviceInterface rootDeviceInterface)
        {
            var devices = GetAllDevices(rootDeviceInterface);
            var services = GetAllServices(rootDeviceInterface);

            return devices.Cast<IEntity>().Concat(services);
        }

        protected IEnumerable<IEntity> GetEntities(
            IRootDeviceInterface rootDeviceInterface,
            IMSearch mSearchReq)
        {
            switch (mSearchReq.ST.StSearchType)
            {
                case STType.All:
                    return GetAllEntities(rootDeviceInterface);
                case STType.RootDeviceSearch:
                    return new List<IEntity>
                    {
                        rootDeviceInterface.RootDeviceConfiguration
                    };
                case STType.UIIDSearch:
                    return GetAllDevices(rootDeviceInterface)
                        .Where(d => d.DeviceUUID == mSearchReq.ST.DeviceUUID);
                case STType.ServiceTypeSearch:
                case STType.DomainServiceSearch:
                    return GetAllServices(rootDeviceInterface)
                        .Where(service => IsMatch(service, mSearchReq.ST));
                case STType.DeviceTypeSearch:
                case STType.DomainDeviceSearch:
                    return GetAllDevices(rootDeviceInterface)
                        .Where(device => IsMatch(device, mSearchReq.ST));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mSearchReq));
            }
        }

        // A search matches an entity when the type name matches, the domain matches
        // (schemas-upnp-org searches match entities without a vendor domain), and the
        // entity's version is at least the requested version (UDA 2.0 backwards
        // compatibility rule: a device must respond to searches for any version it
        // supersedes).
        private static bool IsMatch(IEntity entity, IST st)
        {
            if (entity.TypeName != st.TypeName)
            {
                return false;
            }

            if (entity.Version < st.Version)
            {
                return false;
            }

            switch (st.StSearchType)
            {
                case STType.DeviceTypeSearch:
                case STType.ServiceTypeSearch:
                    return string.IsNullOrEmpty(entity.Domain);
                case STType.DomainDeviceSearch:
                case STType.DomainServiceSearch:
                    return entity.Domain == st.Domain;
                default:
                    return false;
            }
        }

        protected IEnumerable<IServiceConfiguration> GetAllServices(IRootDeviceInterface rootDeviceInterface)
        {
            if (rootDeviceInterface?.RootDeviceConfiguration is null)
            {
                return Enumerable.Empty<IServiceConfiguration>();
            }

            var rootServices = rootDeviceInterface.RootDeviceConfiguration.Services
                               ?? Enumerable.Empty<IServiceConfiguration>();

            var embeddedServices = (rootDeviceInterface.RootDeviceConfiguration.EmbeddedDevices
                                    ?? Enumerable.Empty<IDeviceConfiguration>())
                .SelectMany(embeddedDevice => embeddedDevice?.Services ?? Enumerable.Empty<IServiceConfiguration>());

            return rootServices.Concat(embeddedServices);
        }

        protected IEnumerable<IDeviceConfiguration> GetAllDevices(IRootDeviceInterface rootDeviceInterface)
        {
            if (rootDeviceInterface?.RootDeviceConfiguration is null)
            {
                return Enumerable.Empty<IDeviceConfiguration>();
            }

            var embeddedDevices = rootDeviceInterface.RootDeviceConfiguration.EmbeddedDevices
                                  ?? Enumerable.Empty<IDeviceConfiguration>();

            return embeddedDevices
                .Where(device => device is not null)
                .Append(rootDeviceInterface.RootDeviceConfiguration);
        }

        // Resolves the device that owns an entity: a device owns itself; a service is
        // owned by the device whose Services collection contains it. Needed to build
        // spec-compliant USNs ("uuid:<device-UUID>::<entity URI>") for service entities.
        protected IDeviceConfiguration GetOwnerDevice(IRootDeviceInterface rootDeviceInterface, IEntity entity)
        {
            if (entity is IDeviceConfiguration deviceConfiguration)
            {
                return deviceConfiguration;
            }

            return GetAllDevices(rootDeviceInterface)
                       .FirstOrDefault(device => device.Services?.Contains(entity) ?? false)
                   ?? rootDeviceInterface.RootDeviceConfiguration;
        }
    }
}
