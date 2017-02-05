# SSDP Library for UPnP version 2.0 

[![NuGet Badge](https://buildstats.info/nuget/SSDP.UPnP.PCL)](https://github.com/1iveowl/SSDP.UPnP.PCL)

[![.NET Standard](http://img.shields.io/badge/.NET_Standard-v1.2-green.svg)](https://docs.microsoft.com/da-dk/dotnet/articles/standard/library) 

[![System.Reactive](http://img.shields.io/badge/Rx-v3.1.1-ff69b4.svg)](http://reactivex.io/) 

[![UPnP](http://img.shields.io/badge/UPnP_Device_Architecture-v2.0-blue.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)

## Why This Library
There are other SSDP Libraries available, so why this library?

This library support the v2.0 version of the UPnP Arhitecture. Most other libraries are only for UPnP v1.1.

This library is created for [Reactive Extensions](http://reactivex.io/ "Reactive Extensions"). As SSDP deals with a stream of messages continuously coming in, Rx IMHO provides a much more elegant programming paradigm than what exists already. Sure the Rx learning curve can feel a bit steep at first, but it is worth the effort.

This library is created for .NET Standard 1.2 making it modern and ready for the future. Also, the library seeks to balance broad compatibility with simplicity by supporting only the most recent platforms - i.e. iOS, Android, UWP, .NET Core 1.0+ and .NET 4.5.1+ and Mono. So no support for older versions of Windows Phone or Silver Light for this library.

This project is based on [SocketLite.PCL](https://github.com/1iveowl/SocketLite.PCL) for cross platform TCP sockets support, that uses the "Bait and Switch" pattern. To read about "Bait and Switch" I can recoomend reading this great short blog post: [The Bait and Switch PCL Trick](http://log.paulbetts.org/the-bait-and-switch-pcl-trick/).
## Getting Started is Easy

#### Using Statements
Using Statements assumed in the following code examples:
```csharp
using ISimpleHttpServer.Service;
using SimpleHttpServer.Service;

using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Service;

using SSDP.Console.Test.NET.Model;
using SSDP.UPnP.PCL.Service;
```

#### Listening for UPnP Traffic
The SSDP Library needs a HttpListener which is to be dependency injected into the Device and/or ControlPoint instances later. For a better understanding what a Device and a ControlPoint is in a UPnP context see the [UPnP Architecture documentation](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf). 
```csharp
IHttpListener HttpListener = new HttpListener(timeout:TimeSpan.FromSeconds(30));

// Get an available network interface
var comm = new CommunicationsInterface(); //
var allComms = comm.GetAllInterfaces();
var networkComm = allComms.FirstOrDefault(x => x.GatewayAddress != null);
```
Make the HttpListener start listening to the relevant ports as specified in the  [UPnP Architecture documentation](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)). 

```csharp
await HttpListener.StartTcpRequestListener(1900, networkComm); 
await HttpListener.StartTcpResponseListener(1901, networkComm);
await HttpListener.StartUdpMulticastListener("239.255.255.250", 1900, networkComm);
await HttpListener.StartUdpListener(1900, networkComm);
```
> **Important Note**: On Windows you might want to stop the "SSDP Service". My experience is that if you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will get through to the HttpListener.

#### Creating a UPnP Device
Create a UPnP Device and start listen to M-SEARCH Requests like this:
```csharp
device = new Device(HttpListener);

var MSearchRequestSubscribe = device.MSearchObservable.Subscribe(
	req =>
	{
		// Insert your code here for dealing with the incoming request.
	}
```
#### Creating a UPnP ControlPoint
Create a UPnP ControlPoint and start listen to M-SEARCH Responses and NOTIFY messages like this:
```csharp
controlPoint = new ControlPoint(HttpListener);
    
var MSearchresponseSubscribe = controlPoint.MSearchResponseObservable
    .Subscribe(
    res =>
    {
    	//Insert your code to handle M-SEARCH Reponses here
    }
    
var notifySubscribe = controlPoint.NotifyObservable
    .Subscribe(
    n =>
    {
    	// Insert your code to handle NOTIFY messages here
    }        
```
#### Broadcasting a M-SEARCH Multicast Request
In UPnP a ControlPoint can ask all the devices on the local network to report back about themselves by broadcasting a multicast M-SEARCH Request. Here is how that might look:
```csharp
var mSearchMessage = new MSearch
{
	SearchCastMethod = CastMethod.Multicast,
	CPFN = "myFriendlyNameOfThisControlPoint",
	HostIp = "239.255.255.250", 	// Must be this IP - i.e. this is a multicast 
	HostPort = 1900, 			   // Must be port 1900 as defined in th specification
	MX = TimeSpan.FromSeconds(1),
	ST = "upnp:rootdevice",
	UserAgent = new UserAgent
	{
		OperatingSystem = "Windows",
		OperatingSystemVersion = "10.0",
		ProductName = "SSDP.UPNP.PCL",
		ProductVersion = "0.9",
		UpnpMajorVersion = "2",
		UpnpMinorVersion = "0",
	}
};

await controlPoint.SendMSearch(mSearchMessage);
```

Notice that you must create your own instance of the MSearch class and that it must implement the `IMSearchRequest` Interface. It could be as simple as below or it could be more complex. You could even give the class a different name. It all depends on your needs. Only requirement is that it implements the interface `IMSearchRequest`:

```csharp
internal class MSearch : IMSearchRequest
{
    public bool InvalidRequest { get; } = false;
    public string HostIp { get; internal set; }
    public int HostPort { get; internal set; }
    public IDictionary<string, string> Headers { get; internal set; }
    public CastMethod SearchCastMethod { get; internal set; }
    public string MAN { get; internal set; }
    public TimeSpan MX { get; internal set; }
    public string ST { get; internal set; }
    public IUserAgent UserAgent { get; internal set; }
    public string CPFN { get; internal set; }
    public string CPUUID { get; internal set; }
    public string TCPPORT { get; internal set; }        
}

internal class UserAgent : IUserAgent
{
    public string FullString { get; internal set; }
    public string OperatingSystem { get; internal set; }
    public string OperatingSystemVersion { get; internal set; }
    public string ProductName { get; internal set; }
    public string ProductVersion { get; internal set; }
    public string UpnpMajorVersion { get; internal set; }
    public string UpnpMinorVersion { get; internal set; }
    public bool IsUpnp2 { get; internal set; }
}

```

For details about what a multicast M-SEARCH Request is and how to use it: see the [UPnP Architecture documentation](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)). 
